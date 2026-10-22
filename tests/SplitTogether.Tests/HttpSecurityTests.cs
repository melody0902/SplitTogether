using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace SplitTogether.Tests;

public class SecurityFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseEnvironment("Production")
            .ConfigureAppConfiguration(
                (_, c) =>
                    c.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            {
                                "DataProtection:KeyPath",
                                Path.Combine(Path.GetTempPath(), "SplitTogether-test-keys")
                            },
                            { "Authentication:Google:ClientId", "" },
                            { "Authentication:Google:ClientSecret", "" },
                        }
                    )
            )
            .ConfigureServices(s =>
                s.AddAuthentication(o => o.DefaultAuthenticateScheme = "Test")
                    .AddScheme<AuthenticationSchemeOptions, HeaderIdentity>("Test", _ => { })
            );
    }
}

public class HeaderIdentity(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(
            Request.Headers.ContainsKey("X-Test-Identity")
                ? AuthenticateResult.Success(
                    new AuthenticationTicket(
                        new ClaimsPrincipal(
                            new ClaimsIdentity(
                                new[]
                                {
                                    new Claim(
                                        ClaimTypes.NameIdentifier,
                                        "00000000-0000-0000-0000-000000000001"
                                    ),
                                },
                                Scheme.Name
                            )
                        ),
                        Scheme.Name
                    )
                )
                : AuthenticateResult.NoResult()
        );
}

public class HttpSecurityTests
{
    [Fact]
    public async Task ConfigurationAndUnauthenticatedApi()
    {
        await using var f = new SecurityFactory();
        using var c = f.CreateClient(
            new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false }
        );
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/auth/config")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
        var google = await c.GetAsync("/api/auth/google?returnUrl=https://evil.example");
        Assert.Equal("/login?error=not_configured", google.Headers.Location!.ToString());
    }

    [Fact]
    public async Task CsrfRejectsMissingAndIncorrectAcceptsValid()
    {
        await using var f = new SecurityFactory();
        using var c = f.CreateClient(
            new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false }
        );
        c.DefaultRequestHeaders.Add("X-Test-Identity", "yes");
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await c.PostAsync("/api/auth/logout", null)).StatusCode
        );
        c.DefaultRequestHeaders.Add("X-XSRF-TOKEN", "incorrect");
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await c.PostAsync("/api/auth/logout", null)).StatusCode
        );
        c.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        var response = await c.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        c.DefaultRequestHeaders.Add(
            "X-XSRF-TOKEN",
            json.RootElement.GetProperty("token").GetString()
        );
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await c.PostAsync("/api/auth/logout", null)).StatusCode
        );
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            x => x.Contains("secure", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public async Task ProductionHasNoTestingLoginRoute()
    {
        await using var f = new SecurityFactory();
        using var c = f.CreateClient(
            new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false }
        );
        Assert.False((await c.PostAsync("/api/testing/login", null)).IsSuccessStatusCode);
    }
}
