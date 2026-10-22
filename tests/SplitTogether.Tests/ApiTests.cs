using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SplitTogether.Data;
using SplitTogether.TestSupport;
using Xunit;

namespace SplitTogether.Tests;

public class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SPLIT_TEST_CONNECTION")))
            Skip =
                "Requires dedicated PostgreSQL via SPLIT_TEST_CONNECTION; no in-memory substitute.";
    }
}

public class SqlFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseEnvironment("Testing")
            .ConfigureAppConfiguration(
                (_, config) =>
                    config.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            {
                                "ConnectionStrings:Default",
                                TestDatabase.Validate(
                                    Environment.GetEnvironmentVariable("SPLIT_TEST_CONNECTION")
                                )
                            },
                        }
                    )
            )
            .ConfigureServices(s => s.AddSingleton<IStartupFilter, TestLoginStartupFilter>());
    }

    public async Task<HttpClient> User(string name)
    {
        using (var scope = Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDb>().Database.MigrateAsync();
        }
        var client = CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
            }
        );
        (
            await client.PostAsJsonAsync("/api/testing/login", new { name })
        ).EnsureSuccessStatusCode();
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", csrf.GetProperty("token").GetString());
        return client;
    }
}

[Collection("PostgreSQL")]
public class ApiTests
{
    static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    static async Task<string> NewGroup(HttpClient c) =>
        (
            await Json(
                await c.PostAsJsonAsync(
                    "/api/groups",
                    new { name = "測試旅行 " + Guid.NewGuid(), description = "" }
                )
            )
        )
            .GetProperty("id")
            .GetString()!;

    static async Task<string> Invite(HttpClient c, string g) =>
        (await Json(await c.PostAsJsonAsync($"/api/groups/{g}/invitations", new { })))
            .GetProperty("token")
            .GetString()!;

    static object Expense(
        string payer,
        string[] members,
        Guid key,
        long amount = 90000,
        Guid? version = null
    ) =>
        new
        {
            title = "聚餐",
            amountMinor = amount,
            date = "2026-09-25",
            payerId = payer,
            splitMode = "Equal",
            splits = members.Select(memberId => new { memberId, amountMinor = 0 }),
            note = "",
            clientRequestId = key,
            version,
        };

    [SqlFact]
    public async Task ScenarioPermissionsIdempotencyAndVersion()
    {
        await using var f = new SqlFactory();
        using var a = await f.User("A");
        using var b = await f.User("B");
        using var c = await f.User("C");
        using var outsider = await f.User("Outsider");
        var g = await NewGroup(a);
        var token = await Invite(a, g);
        foreach (var user in new[] { b, c })
            (
                await user.PostAsJsonAsync($"/api/invitations/{token}/accept", new { })
            ).EnsureSuccessStatusCode();
        var ms = await a.GetFromJsonAsync<JsonElement>($"/api/groups/{g}/members");
        string Id(string name) =>
            ms.EnumerateArray()
                .Single(x => x.GetProperty("name").GetString() == name)
                .GetProperty("id")
                .GetString()!;
        var ai = Id("A");
        var bi = Id("B");
        var ci = Id("C");
        var key = Guid.NewGuid();
        var expense = Expense(ai, [ai, bi, ci], key);
        var responses = await Task.WhenAll(
            a.PostAsJsonAsync($"/api/groups/{g}/expenses", expense),
            a.PostAsJsonAsync($"/api/groups/{g}/expenses", expense)
        );
        var first = await Json(responses[0]);
        var second = await Json(responses[1]);
        Assert.Equal(first.GetProperty("id").GetString(), second.GetProperty("id").GetString());
        Assert.Equal(
            HttpStatusCode.Conflict,
            (
                await a.PostAsJsonAsync($"/api/groups/{g}/expenses", Expense(ai, [ai], key, 1))
            ).StatusCode
        );
        (
            await b.PostAsJsonAsync(
                $"/api/groups/{g}/expenses",
                Expense(bi, [bi, ci], Guid.NewGuid(), 30000)
            )
        ).EnsureSuccessStatusCode();
        var snapshot = await a.GetFromJsonAsync<JsonElement>($"/api/groups/{g}/balances");
        Assert.Equal(120000, snapshot.GetProperty("totalMinor").GetInt64());
        var nets = snapshot
            .GetProperty("members")
            .EnumerateArray()
            .ToDictionary(
                x => x.GetProperty("memberId").GetString()!,
                x => x.GetProperty("netMinor").GetInt64()
            );
        Assert.Equal(60000, nets[ai]);
        Assert.Equal(-15000, nets[bi]);
        Assert.Equal(-45000, nets[ci]);
        var p1 = new
        {
            fromId = bi,
            toId = ai,
            amountMinor = 15000,
            date = "2026-09-25",
            note = "",
            clientRequestId = Guid.NewGuid(),
        };
        var paid = await Json(await b.PostAsJsonAsync($"/api/groups/{g}/settlements", p1));
        Assert.Equal(
            paid.GetProperty("id").GetString(),
            (await Json(await b.PostAsJsonAsync($"/api/groups/{g}/settlements", p1)))
                .GetProperty("id")
                .GetString()
        );
        (
            await c.PostAsJsonAsync(
                $"/api/groups/{g}/settlements",
                new
                {
                    fromId = ci,
                    toId = ai,
                    amountMinor = 45000,
                    date = "2026-09-25",
                    note = "",
                    clientRequestId = Guid.NewGuid(),
                }
            )
        ).EnsureSuccessStatusCode();
        snapshot = await a.GetFromJsonAsync<JsonElement>($"/api/groups/{g}/balances");
        Assert.All(
            snapshot.GetProperty("members").EnumerateArray(),
            x => Assert.Equal(0, x.GetProperty("netMinor").GetInt64())
        );
        var sid = paid.GetProperty("id").GetString();
        (
            await b.PostAsJsonAsync($"/api/groups/{g}/settlements/{sid}/void", new { })
        ).EnsureSuccessStatusCode();
        (
            await b.PostAsJsonAsync($"/api/groups/{g}/settlements/{sid}/void", new { })
        ).EnsureSuccessStatusCode();
        snapshot = await a.GetFromJsonAsync<JsonElement>($"/api/groups/{g}/balances");
        Assert.Equal(
            15000,
            snapshot
                .GetProperty("members")
                .EnumerateArray()
                .Single(x => x.GetProperty("memberId").GetString() == ai)
                .GetProperty("netMinor")
                .GetInt64()
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await outsider.GetAsync($"/api/groups/{g}/expenses")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await b.PostAsJsonAsync($"/api/groups/{g}/invitations", new { })).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await a.DeleteAsync($"/api/groups/{g}/members/{bi}")).StatusCode
        );
        var eid = first.GetProperty("id").GetString();
        var detail = await a.GetFromJsonAsync<JsonElement>($"/api/groups/{g}/expenses/{eid}");
        var version = detail.GetProperty("version").GetGuid();
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (
                await b.PutAsJsonAsync(
                    $"/api/groups/{g}/expenses/{eid}",
                    Expense(ai, [ai, bi, ci], Guid.NewGuid(), 90000, version)
                )
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Conflict,
            (
                await a.PutAsJsonAsync(
                    $"/api/groups/{g}/expenses/{eid}",
                    Expense(ai, [ai, bi, ci], Guid.NewGuid(), 90000, Guid.NewGuid())
                )
            ).StatusCode
        );
        var other = await NewGroup(a);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await a.GetAsync($"/api/groups/{other}/expenses/{eid}")).StatusCode
        );
        a.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await a.PostAsJsonAsync($"/api/groups/{g}/settlements", p1)).StatusCode
        );
    }

    [SqlFact]
    public async Task ConcurrentInviteAndRemovalRace()
    {
        await using var f = new SqlFactory();
        using var a = await f.User("Owner");
        using var b = await f.User("Joiner");
        var g = await NewGroup(a);
        var token = await Invite(a, g);
        var joins = await Task.WhenAll(
            b.PostAsJsonAsync($"/api/invitations/{token}/accept", new { }),
            b.PostAsJsonAsync($"/api/invitations/{token}/accept", new { })
        );
        Assert.All(joins, x => x.EnsureSuccessStatusCode());
        var ms = await a.GetFromJsonAsync<JsonElement>($"/api/groups/{g}/members");
        Assert.Equal(2, ms.GetArrayLength());
        var member = ms.EnumerateArray()
            .Single(x => x.GetProperty("name").GetString() == "Joiner")
            .GetProperty("id")
            .GetString()!;
        var results = await Task.WhenAll(
            a.DeleteAsync($"/api/groups/{g}/members/{member}"),
            b.PostAsJsonAsync(
                $"/api/groups/{g}/expenses",
                Expense(member, [member], Guid.NewGuid())
            )
        );
        Assert.False(results.All(x => x.IsSuccessStatusCode));
        Assert.Contains(results, x => x.IsSuccessStatusCode);
    }

    [SqlFact]
    public async Task ConstraintsAndTransactionRollback()
    {
        await using var f = new SqlFactory();
        using var a = await f.User("Constraints");
        var g = Guid.Parse(await NewGroup(a));
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var member = await db.GroupMembers.SingleAsync(x => x.GroupId == g);
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            db.GroupMembers.Add(new() { GroupId = g, UserId = member.UserId });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            await tx.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.GroupMembers.CountAsync(x => x.GroupId == g));
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            db.GroupMembers.Add(new() { GroupId = g, UserId = Guid.NewGuid() });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            await tx.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.GroupMembers.CountAsync(x => x.GroupId == g));
    }

    [Fact]
    public void TestDatabaseGuard()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TestDatabase.Validate("Host=localhost;Database=production;Username=postgres")
        );
        Assert.Throws<InvalidOperationException>(() => TestDatabase.Validate(null));
    }
}
