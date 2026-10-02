using Microsoft.AspNetCore.Http;

namespace Planarian.Modules.Authentication.Services;

internal static class PlanarianCookieOptions
{
    public static CookieOptions CreateHttpOnlyEssential(TimeSpan? maxAge = null)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            MaxAge = maxAge,
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Secure = true
        };
    }

    public static void ConfigureHttpOnlyEssential(CookieBuilder cookie)
    {
        cookie.HttpOnly = true;
        cookie.IsEssential = true;
        cookie.Path = "/";
        cookie.SameSite = SameSiteMode.Lax;
        cookie.SecurePolicy = CookieSecurePolicy.Always;
    }
}
