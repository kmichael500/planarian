using Microsoft.AspNetCore.Http;
using Planarian.Library.Options;
using Planarian.Modules.Authentication.Services;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Email;

public sealed class AuthCookieSecurityTests
{
    [Fact]
    public void SecuritySensitiveCookieNamesUseHostPrefix()
    {
        Assert.Equal("__Host-planarian_auth", AuthCookieService.AuthCookieName);
        Assert.Equal("__Host-planarian_csrf", AuthCookieService.AntiforgeryCookieName);
        Assert.StartsWith("__Host-", RegistrationContinuationService.CookieName);
    }

    [Fact]
    public void AuthCookieUsesHostCompatibleSecurityAttributes()
    {
        var service = new AuthCookieService(new AuthOptions());
        var context = new DefaultHttpContext();

        service.SetAuthCookie(context, "token", rememberMe: false);

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.StartsWith($"{AuthCookieService.AuthCookieName}=", setCookie);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", setCookie, StringComparison.OrdinalIgnoreCase);
    }
}
