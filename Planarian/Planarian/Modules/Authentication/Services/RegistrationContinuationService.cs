using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace Planarian.Modules.Authentication.Services;

public sealed class RegistrationContinuationService
{
    public const string CookieName = "__Host-planarian_registration_continue";

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private const string ProtectionPurpose = "Planarian.Authentication.RegistrationContinuation.v1";
    private readonly ITimeLimitedDataProtector _protector;

    public RegistrationContinuationService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider
            .CreateProtector(ProtectionPurpose)
            .ToTimeLimitedDataProtector();
    }

    public void Issue(HttpContext httpContext, string emailAddress)
    {
        var value = _protector.Protect(NormalizeEmail(emailAddress), Lifetime);
        httpContext.Response.Cookies.Append(
            CookieName,
            value,
            PlanarianCookieOptions.CreateHttpOnlyEssential(Lifetime));
    }

    public bool TryConsume(HttpContext httpContext, string emailAddress)
    {
        var protectedValue = httpContext.Request.Cookies[CookieName];
        if (string.IsNullOrWhiteSpace(protectedValue)) return false;

        try
        {
            var protectedEmail = _protector.Unprotect(protectedValue);
            if (!string.Equals(protectedEmail, NormalizeEmail(emailAddress), StringComparison.Ordinal))
                return false;

            Clear(httpContext);
            return true;
        }
        catch (CryptographicException)
        {
            Clear(httpContext);
            return false;
        }
    }

    private static string NormalizeEmail(string emailAddress) => emailAddress.Trim().ToLowerInvariant();

    private static void Clear(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(CookieName, PlanarianCookieOptions.CreateHttpOnlyEssential());
    }
}
