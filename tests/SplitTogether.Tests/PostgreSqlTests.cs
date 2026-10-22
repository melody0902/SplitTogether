using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitTogether.Data;
using SplitTogether.Models;
using SplitTogether.Services;
using Xunit;

namespace SplitTogether.Tests;

// Serialize schema setup across classes; individual tests still exercise parallel requests.
[Collection("PostgreSQL")]
public class PostgreSqlTests
{
    [SqlFact]
    public async Task ConcurrentFirstLoginsResolveToOneIdentity()
    {
        await using var factory = new SqlFactory();
        using var bootstrap = await factory.User("Identity test bootstrap");
        var providerId = Guid.NewGuid().ToString();
        async Task<Guid> Login()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var user = await scope
                .ServiceProvider.GetRequiredService<ExternalUserService>()
                .Resolve("Google", providerId, "同一個使用者", null, null, CancellationToken.None);
            return user.Id;
        }
        var ids = await Task.WhenAll(Login(), Login(), Login(), Login());
        Assert.Single(ids.Distinct());
        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Equal(1, await db.ExternalLogins.CountAsync(x => x.ProviderUserId == providerId));
    }

    [SqlFact]
    public async Task PostgreSqlPreservesBigintDateUtcAndConcurrency()
    {
        await using var factory = new SqlFactory();
        using var bootstrap = await factory.User("PostgreSQL types bootstrap");
        await using var first = factory.Services.CreateAsyncScope();
        await using var second = factory.Services.CreateAsyncScope();
        var db = first.ServiceProvider.GetRequiredService<AppDb>();
        var other = second.ServiceProvider.GetRequiredService<AppDb>();
        var user = new User { Name = "型別測試" };
        var group = new Group { Name = "型別測試群組", OwnerId = user.Id };
        var member = new GroupMember { GroupId = group.Id, UserId = user.Id };
        db.AddRange(user, group, member);
        await db.SaveChangesAsync();
        var expense = new Expense
        {
            GroupId = group.Id,
            PayerId = member.Id,
            CreatedBy = user.Id,
            UpdatedBy = user.Id,
            Title = "大額測試",
            AmountMinor = LedgerMath.MaxAmount,
            Date = new DateOnly(2028, 2, 29),
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync();
        var roundTrip = await other.Expenses.SingleAsync(x => x.Id == expense.Id);
        Assert.Equal(LedgerMath.MaxAmount, roundTrip.AmountMinor);
        Assert.Equal(new DateOnly(2028, 2, 29), roundTrip.Date);
        Assert.Equal(DateTimeKind.Utc, roundTrip.CreatedAt.Kind);
        var stale = await other.Groups.SingleAsync(x => x.Id == group.Id);
        group.Name = "第一個修改";
        group.Version = Guid.NewGuid();
        await db.SaveChangesAsync();
        stale.Name = "不應覆寫";
        stale.Version = Guid.NewGuid();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync());
        await using var tx = await db.Database.BeginTransactionAsync();
        expense.AmountMinor = -1;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        await tx.RollbackAsync();
    }
}
