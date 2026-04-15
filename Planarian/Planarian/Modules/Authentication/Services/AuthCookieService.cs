using Microsoft.AspNetCore.Http;
using Planarian.Library.Options;

namespace Planarian.Modules.Authentication.Services;

public class AuthCookieService
{
    public const string AuthCookieName = "planarian_auth";
    public const string AntiforgeryCookieName = "planarian_csrf";
    public const string RequestTokenHeaderName = "X-XSRF-TOKEN";

    private readonly AuthOptions _authOptions;

    public AuthCookieService(AuthOptions authOptions)
    {
        _authOptions = authOptions;
    }

    public void SetAuthCookie(HttpContext httpContext, string token, bool rememberMe)
    {
        var options = BuildCookieOptions(httpOnly: true);
        if (rememberMe)
        {
            options.Expires = DateTimeOffset.UtcNow.AddSeconds(_authOptions.JwtExpiryDurationSeconds);
        }
        // Without an explicit expiration, the browser treats this as a session cookie.

        httpContext.Response.Cookies.Append(AuthCookieName, token, options);
    }

    public void ClearAuthCookie(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(AuthCookieName, BuildCookieOptions(httpOnly: true));
    }

    public void ClearAntiforgeryCookies(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(AntiforgeryCookieName, BuildCookieOptions(httpOnly: true));
    }

    private static CookieOptions BuildCookieOptions(bool httpOnly)
    {
        return new CookieOptions
        {
            HttpOnly = httpOnly,
            IsEssential = true,
            Path = "/",
            SameSite = SameSiteMode.None,
            Secure = true
        };
    }
}
