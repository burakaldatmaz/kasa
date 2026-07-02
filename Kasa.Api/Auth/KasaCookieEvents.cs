using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Kasa.Api.Auth;

/// <summary>
/// Cookie doğrulama: ticket'taki oturum damgası güncel değilse (logout sonrası) reddedilir.
/// API olduğumuz için challenge/forbid yönlendirmeleri 302 yerine 401/403 döner.
/// </summary>
public sealed class KasaCookieEvents(SessionStamp stamp) : CookieAuthenticationEvents
{
    public const string StampClaim = "kasa:stamp";

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var claim = context.Principal?.FindFirst(StampClaim)?.Value;
        if (claim is null || claim != await stamp.GetAsync())
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
