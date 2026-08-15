using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Planarian.Modules.Authentication.Services;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Email;

public sealed class RegistrationContinuationServiceTests
{
    private static RegistrationContinuationService CreateService() =>
        new(new EphemeralDataProtectionProvider());

    [Fact]
    public void IssueCreatesShortLivedSecureHttpOnlyCookieSeparateFromAuthentication()
    {
        var service = CreateService();
        var context = new DefaultHttpContext();

        service.Issue(context, "user@example.com");

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.StartsWith("__Host-", RegistrationContinuationService.CookieName);
        Assert.StartsWith($"{RegistrationContinuationService.CookieName}=", setCookie);
        Assert.DoesNotContain($"{AuthCookieService.AuthCookieName}=", setCookie);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=1800", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IssueEmbedsThirtyMinuteServerExpiration()
    {
        var provider = new EphemeralDataProtectionProvider();
        var service = new RegistrationContinuationService(provider);
        var context = new DefaultHttpContext();
        var beforeIssue = DateTimeOffset.UtcNow;

        service.Issue(context, "user@example.com");

        var protectedValue = GetCookiePair(context).Split('=', 2)[1];
        var inspector = provider
            .CreateProtector("Planarian.Authentication.RegistrationContinuation.v1")
            .ToTimeLimitedDataProtector();
        _ = inspector.Unprotect(protectedValue, out var expiration);
        var afterIssue = DateTimeOffset.UtcNow;
        Assert.InRange(expiration, beforeIssue.AddMinutes(30), afterIssue.AddMinutes(30));
    }

    [Fact]
    public void TryConsumeAcceptsMatchingCookieAndDeletesIt()
    {
        var service = CreateService();
        var issuedContext = new DefaultHttpContext();
        service.Issue(issuedContext, "User@Example.com");
        var context = CreateRequestContext(GetCookiePair(issuedContext));

        var consumed = service.TryConsume(context, "user@example.com");

        Assert.True(consumed);
        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.StartsWith($"{RegistrationContinuationService.CookieName}=;", setCookie);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryConsumeRejectsDifferentEmailWithoutDestroyingValidContinuation()
    {
        var service = CreateService();
        var issuedContext = new DefaultHttpContext();
        service.Issue(issuedContext, "user@example.com");
        var context = CreateRequestContext(GetCookiePair(issuedContext));

        var consumed = service.TryConsume(context, "other@example.com");

        Assert.False(consumed);
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public void TryConsumeRejectsTamperedCookieAndDeletesIt()
    {
        var service = CreateService();
        var issuedContext = new DefaultHttpContext();
        service.Issue(issuedContext, "user@example.com");
        var cookiePair = GetCookiePair(issuedContext);
        var separatorIndex = cookiePair.IndexOf('=');
        var valueStart = separatorIndex + 1;
        var tamperIndex = valueStart + ((cookiePair.Length - valueStart) / 2);
        var replacement = cookiePair[tamperIndex] == 'A' ? 'B' : 'A';
        var tamperedCookiePair = cookiePair[..tamperIndex] + replacement + cookiePair[(tamperIndex + 1)..];
        var context = CreateRequestContext(tamperedCookiePair);

        var consumed = service.TryConsume(context, "user@example.com");

        Assert.False(consumed);
        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.StartsWith($"{RegistrationContinuationService.CookieName}=;", setCookie);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetCookiePair(DefaultHttpContext context) =>
        context.Response.Headers.SetCookie.ToString().Split(';', 2)[0];

    private static DefaultHttpContext CreateRequestContext(string cookiePair)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = cookiePair;
        return context;
    }
}
