using System.Security.Claims;
using Kasa.Api.Auth;
using Kasa.Api.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Kasa.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (
            HttpContext http,
            LoginRateLimiter limiter,
            SessionStamp stamp,
            IConfiguration config,
            ILoggerFactory loggerFactory,
            LoginRequest request) =>
        {
            var logger = loggerFactory.CreateLogger("Kasa.Auth");
            var ip = http.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor";

            // Kilitliyken parola hiç kontrol edilmez: doğru parola da 429 alır.
            if (limiter.IsLockedOut(out var lockedUntil))
            {
                logger.LogWarning("Login reddedildi: kilit aktif (IP: {Ip}, kilit bitişi: {LockedUntil:O}).",
                    ip, lockedUntil);
                return Results.Json(new ErrorResponse("Çok fazla hatalı deneme. 15 dakika sonra tekrar deneyin."),
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            var hash = config["PASSWORD_HASH"];
            if (string.IsNullOrEmpty(hash))
            {
                logger.LogError("PASSWORD_HASH yapılandırılmamış; login devre dışı.");
                return Results.Json(new ErrorResponse("Sunucu yapılandırması eksik."),
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            bool valid;
            try
            {
                valid = !string.IsNullOrEmpty(request.Password) && BCrypt.Net.BCrypt.Verify(request.Password, hash);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PASSWORD_HASH bcrypt formatında çözülemedi; login devre dışı.");
                return Results.Json(new ErrorResponse("Sunucu yapılandırması hatalı."),
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            if (!valid)
            {
                var (failures, lockStarted) = limiter.RegisterFailure();
                if (lockStarted is { } until)
                    logger.LogWarning("Hatalı parola (IP: {Ip}), {Failures}. deneme — kilit başladı, bitiş: {LockedUntil:O}.",
                        ip, failures, until);
                else
                    logger.LogWarning("Hatalı parola (IP: {Ip}), deneme {Failures}/{Max}.",
                        ip, failures, LoginRateLimiter.MaxFailures);
                return Results.Json(new ErrorResponse("Parola hatalı."),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            limiter.Reset();
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "kasa"), new Claim(KasaCookieEvents.StampClaim, await stamp.GetAsync())],
                CookieAuthenticationDefaults.AuthenticationScheme);
            // IsPersistent: cookie 30 gün kalıcı; süre ve sliding davranışı Program.cs'teki
            // cookie options'tan gelir.
            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true });

            logger.LogInformation("Başarılı giriş (IP: {Ip}).", ip);
            return Results.NoContent();
        }).AllowAnonymous();

        group.MapPost("/logout", async (HttpContext http, SessionStamp stamp) =>
        {
            // Damga döner: bu cihazdaki cookie silinir, kopyalanmış eski cookie'ler de geçersizleşir.
            await stamp.RotateAsync();
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        // Frontend'in oturum kontrolü: 200 → oturum açık, 401 → login sayfasına.
        group.MapGet("/me", () => Results.Ok(new MeResponse(true)));
    }
}
