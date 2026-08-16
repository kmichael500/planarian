using Microsoft.AspNetCore.Http;
using Planarian.Library.Options;
using Planarian.Modules.Authentication.Services;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Authentication;

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

    [Fact]
    public void RememberMeControlsWhetherTheAuthCookiePersistsBeyondTheBrowserSession()
    {
        var service = new AuthCookieService(new AuthOptions { JwtExpiryDurationSeconds = 3600 });
        var sessionContext = new DefaultHttpContext();
        var persistentContext = new DefaultHttpContext();

        service.SetAuthCookie(sessionContext, "session-token", rememberMe: false);
        service.SetAuthCookie(persistentContext, "persistent-token", rememberMe: true);

        var sessionCookie = sessionContext.Response.Headers.SetCookie.ToString();
        var persistentCookie = persistentContext.Response.Headers.SetCookie.ToString();
        Assert.DoesNotContain("expires=", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", persistentCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClearingAuthCookieUsesTheSameHostCompatibleCookieScope()
    {
        var service = new AuthCookieService(new AuthOptions());
        var context = new DefaultHttpContext();

        service.ClearAuthCookie(context);

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.StartsWith($"{AuthCookieService.AuthCookieName}=;", setCookie);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", setCookie, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void ClearingAntiforgeryCookieUsesTheSameHostCompatibleCookieScope()
    {
        var service = new AuthCookieService(new AuthOptions());
        var context = new DefaultHttpContext();

        service.ClearAntiforgeryCookies(context);

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.StartsWith($"{AuthCookieService.AntiforgeryCookieName}=;", setCookie);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", setCookie, StringComparison.OrdinalIgnoreCase);
    }

}
