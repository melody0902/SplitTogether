using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SplitTogether.Data;
using SplitTogether.Models;
using SplitTogether.Services;

namespace SplitTogether.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(AppDb db, IConfiguration config, ExternalUserService users)
    : ControllerBase
{
    static string ReturnPath(string? value) =>
        value == "/groups"
        || value != null
            && System.Text.RegularExpressions.Regex.IsMatch(value, @"^/invite/[a-f0-9]{64}$")
            ? value!
            : "/groups";

    [HttpGet("config")]
    public object Config() =>
        new
        {
            googleEnabled = !string.IsNullOrWhiteSpace(config["Authentication:Google:ClientId"])
                && !string.IsNullOrWhiteSpace(config["Authentication:Google:ClientSecret"]),
        };

    [HttpGet("google")]
    public IActionResult Google([FromQuery] string? returnUrl)
    {
        if (
            string.IsNullOrWhiteSpace(config["Authentication:Google:ClientId"])
            || string.IsNullOrWhiteSpace(config["Authentication:Google:ClientSecret"])
        )
            return Redirect("/login?error=not_configured");
        return Challenge(
            new AuthenticationProperties
            {
                RedirectUri =
                    "/api/auth/complete?returnUrl=" + Uri.EscapeDataString(ReturnPath(returnUrl)),
            },
            "Google"
        );
    }

    [HttpGet("complete")]
    public async Task<IActionResult> Complete([FromQuery] string? returnUrl, CancellationToken ct)
    {
        var result = await HttpContext.AuthenticateAsync("External");
        var external = result.Principal;
        var id = external?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!result.Succeeded || external is null || string.IsNullOrEmpty(id))
            return Redirect("/login?error=google_failed");
        var user = await users.Resolve(
            "Google",
            id,
            external!.FindFirstValue(ClaimTypes.Name) ?? "使用者",
            external.FindFirstValue(ClaimTypes.Email),
            external.FindFirstValue("urn:google:picture"),
            ct
        );
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
            },
            "App"
        );
        await HttpContext.SignInAsync("App", new ClaimsPrincipal(identity));
        await HttpContext.SignOutAsync("External");
        return LocalRedirect(ReturnPath(returnUrl));
    }

    [Authorize, HttpGet("me")]
    public async Task<object> Me(CancellationToken ct)
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await db
            .Users.Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Picture,
            })
            .SingleAsync(ct);
    }

    [HttpGet("csrf")]
    public object Csrf([FromServices] IAntiforgery af)
    {
        var t = af.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append(
            "XSRF-TOKEN",
            t.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
            }
        );
        return new { token = t.RequestToken };
    }

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("App");
        Response.Cookies.Delete("XSRF-TOKEN");
        return NoContent();
    }
}
