using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.String;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Users.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Authentication.Controllers;

[Route("api/authentication")]
public class AuthenticationController : PlanarianControllerBase<AuthenticationService>
{
    private const int MaxLoginAttemptsPerMinute = 5;
    private static readonly TimeSpan LoginAttemptWindow = TimeSpan.FromMinutes(1);

    private readonly MemoryCache _cache;
    private readonly UserService _userService;

    public AuthenticationController(
        RequestUser requestUser,
        TokenService tokenService,
        AuthenticationService service,
        UserService userService,
        MemoryCache cache) :
        base(requestUser, tokenService, service)
    {
        _userService = userService;
        _cache = cache;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<string>> Login([FromBody] UserLoginVm values, CancellationToken cancellationToken)
    {
        var cacheKey = GetLoginRateLimitKey(values.EmailAddress);
        var rateLimitState = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = LoginAttemptWindow;
            return new LoginRateLimitState();
        })!;

        lock (rateLimitState.SyncRoot)
        {
            if (rateLimitState.Attempts >= MaxLoginAttemptsPerMinute)
                throw ApiExceptionDictionary.TooManyRequests("Too many login attempts. Try again in a minute.");

            rateLimitState.Attempts++;
        }

        var token = await Service.AuthenticateEmailPassword(values.EmailAddress, values.Password);
        _cache.Remove(cacheKey);

        return new JsonResult(token);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await Service.Logout(HttpContext);
        return new OkResult();
    }

    private string GetLoginRateLimitKey(string? emailAddress)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var normalizedEmail = emailAddress?.Trim().ToLowerInvariant() ?? string.Empty;
        return $"login-rate-limit:{ipAddress}:{normalizedEmail}";
    }

    private sealed class LoginRateLimitState
    {
        public int Attempts { get; set; }
        public object SyncRoot { get; } = new();
    }
}
