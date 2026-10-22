using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SplitTogether.Data;
using SplitTogether.Models;

namespace SplitTogether.TestSupport;

public static class TestDatabase
{
    public static string Validate(string? connection)
    {
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException(
                "Set SPLIT_TEST_CONNECTION to a dedicated PostgreSQL test database."
            );
        var b = new NpgsqlConnectionStringBuilder(connection);
        if (
            string.IsNullOrEmpty(b.Database)
            || !b.Database.StartsWith("splittogether_test_", StringComparison.Ordinal)
            || b.Database.Length <= "splittogether_test_".Length
        )
            throw new InvalidOperationException(
                "Test database name must start with splittogether_test_ and have a suffix. Never use production data."
            );
        return connection;
    }
}

// This source is compiled only into test assemblies; it is absent from the production API.
public class TestLoginStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
            if (env.EnvironmentName != "Testing")
                throw new InvalidOperationException("Test login requires Testing environment.");
            app.Use(
                async (ctx, following) =>
                {
                    if (ctx.Request.Path != "/api/testing/login")
                    {
                        await following();
                        return;
                    }
                    if (ctx.Request.Method != "POST")
                    {
                        ctx.Response.StatusCode = 405;
                        return;
                    }
                    var body = await ctx.Request.ReadFromJsonAsync<Login>();
                    if (body == null || body.Name.Length is < 1 or > 100)
                    {
                        ctx.Response.StatusCode = 400;
                        return;
                    }
                    var db = ctx.RequestServices.GetRequiredService<AppDb>();
                    var user = new User { Name = body.Name };
                    db.Users.Add(user);
                    await db.SaveChangesAsync();
                    await ctx.SignInAsync(
                        "App",
                        new ClaimsPrincipal(
                            new ClaimsIdentity(
                                new[]
                                {
                                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                                    new Claim(ClaimTypes.Name, user.Name),
                                },
                                "App"
                            )
                        )
                    );
                    await ctx.Response.WriteAsJsonAsync(new { user.Id, user.Name });
                }
            );
            next(app);
        };

    record Login(string Name);
}
