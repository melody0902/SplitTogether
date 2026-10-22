using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SplitTogether.Data;
using SplitTogether.Services;

var application = AppBootstrap.Build(args);
if (Environment.GetEnvironmentVariable("SPLIT_SEED_DEMO") == "1")
{
    await using var scope = application.Services.CreateAsyncScope();
    await DemoSeeder.Run(
        scope.ServiceProvider.GetRequiredService<AppDb>(),
        application.Environment.EnvironmentName
    );
}
else
    application.Run();

public partial class Program { }

public static class AppBootstrap
{
    public static WebApplication Build(
        string[] args,
        Action<WebApplicationBuilder>? configure = null
    )
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                Args = args,
                ApplicationName = typeof(Program).Assembly.FullName,
            }
        );
        // Never log request paths: invitation URLs contain secrets.
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.Logging.AddFilter(
            "Microsoft.EntityFrameworkCore.Database.Command",
            LogLevel.Warning
        );
        builder.Services.AddDbContext<AppDb>(o =>
            o.UseNpgsql(
                builder.Configuration.GetConnectionString("Default")
                    ?? "Host=localhost;Port=5432;Database=splittogether;Username=split_app",
                pg => pg.SetPostgresVersion(18, 0)
            )
        );
        builder.Services.AddScoped<GroupAccess>();
        builder.Services.AddScoped<ExternalUserService>();
        builder.Services.AddControllers(o => o.Filters.Add<CsrfFilter>());
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi();
        builder.Services.AddAntiforgery(o =>
        {
            o.HeaderName = "X-XSRF-TOKEN";
            o.Cookie.Name = "SplitTogether.Antiforgery";
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });
        var keys = builder.Configuration["DataProtection:KeyPath"];
        if (!string.IsNullOrWhiteSpace(keys))
            builder
                .Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keys))
                .SetApplicationName("SplitTogether");
        var auth = builder
            .Services.AddAuthentication(o =>
            {
                o.DefaultScheme = "App";
                o.DefaultChallengeScheme = "App";
            })
            .AddCookie(
                "App",
                o =>
                {
                    o.Cookie.Name = "SplitTogether.Session";
                    o.Cookie.HttpOnly = true;
                    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    o.Cookie.SameSite = SameSiteMode.Lax;
                    o.ExpireTimeSpan = TimeSpan.FromHours(12);
                    o.SlidingExpiration = false;
                    o.Events = new CookieAuthenticationEvents
                    {
                        OnRedirectToLogin = c =>
                        {
                            c.Response.StatusCode = 401;
                            return Task.CompletedTask;
                        },
                        OnRedirectToAccessDenied = c =>
                        {
                            c.Response.StatusCode = 403;
                            return Task.CompletedTask;
                        },
                    };
                }
            )
            .AddCookie(
                "External",
                o =>
                {
                    o.Cookie.Name = "SplitTogether.External";
                    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                }
            );
        if (
            !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Google:ClientId"])
            && !string.IsNullOrWhiteSpace(
                builder.Configuration["Authentication:Google:ClientSecret"]
            )
        )
            auth.AddGoogle(o =>
            {
                o.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
                o.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
                o.SignInScheme = "External";
                o.CallbackPath = "/signin-google";
                o.SaveTokens = false;
                o.ClaimActions.MapJsonKey("urn:google:picture", "picture");
                o.Events.OnRemoteFailure = c =>
                {
                    c.HandleResponse();
                    c.Response.Redirect("/login?error=google_failed");
                    return Task.CompletedTask;
                };
            });
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.AddPolicy(
                "invites",
                ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 30,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                        }
                    )
            );
        });
        configure?.Invoke(builder);
        var app = builder.Build();
        app.Use(
            async (ctx, next) =>
            {
                try
                {
                    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
                    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
                    await next();
                }
                catch (Exception e)
                {
                    var (status, code, message) = e switch
                    {
                        BusinessException b => (b.Status, b.Code, b.Message),
                        DbUpdateConcurrencyException => (409, "conflict", "資料已變更，請重新整理"),
                        OverflowException => (400, "amount_overflow", "金額超出可處理範圍"),
                        _ => (500, "server_error", "服務暫時無法處理，請稍後再試"),
                    };
                    if (status == 500)
                        app.Logger.LogError(
                            "Request failed: {Type}; trace {Trace}",
                            e.GetType().Name,
                            ctx.TraceIdentifier
                        );
                    ctx.Response.StatusCode = status;
                    await ctx.Response.WriteAsJsonAsync(
                        new ProblemDetails
                        {
                            Status = status,
                            Title = message,
                            Extensions = { ["code"] = code, ["traceId"] = ctx.TraceIdentifier },
                        }
                    );
                }
            }
        );
        app.UseStatusCodePages(async c =>
        {
            if (c.HttpContext.Request.Path.StartsWithSegments("/api"))
                await c.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = c.HttpContext.Response.StatusCode,
                        Title = "請求無法完成",
                        Extensions = { ["code"] = "http_" + c.HttpContext.Response.StatusCode },
                    }
                );
        });
        if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Testing")
            app.UseForwardedHeaders(
                new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedProto,
                    ForwardLimit = 1,
                }
            );
        else
            app.UseHttpsRedirection();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        if (app.Environment.IsDevelopment())
            app.MapOpenApi();
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
        app.MapGet(
            "/api/health/ready",
            async (AppDb db, CancellationToken ct) =>
                await db.Database.CanConnectAsync(ct)
                    ? Results.Ok(new { status = "ready" })
                    : Results.StatusCode(503)
        );
        app.MapFallbackToFile("index.html");
        return app;
    }
}
