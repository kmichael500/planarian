using System.Threading.RateLimiting;
using Planarian.Model.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Planarian.Library.Exceptions;
using Planarian.Shared.Attributes;
using Planarian.Shared.Options;
using Planarian.Shared.Services;

namespace Planarian.Modules.Authentication.Services;

public class RequestThrottleService
{
    #region Constructor/Fields

    private readonly MemoryCache _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RateLimitOptions _rateLimitOptions;
    private readonly RequestThrottleOptions _options;
    private readonly RequestUser _requestUser;
    private readonly ThrottleEventLogService _throttleEventLogService;

    public RequestThrottleService(
        MemoryCache cache,
        RateLimitOptions rateLimitOptions,
        RequestThrottleOptions options,
        IHttpContextAccessor httpContextAccessor,
        RequestUser requestUser,
        ThrottleEventLogService throttleEventLogService)
    {
        _cache = cache;
        _rateLimitOptions = rateLimitOptions;
        _options = options;
        _httpContextAccessor = httpContextAccessor;
        _requestUser = requestUser;
        _throttleEventLogService = throttleEventLogService;
    }

    #endregion

    #region Endpoint Rate Limiting

    public RateLimitPartition<string> GetEndpointRateLimitPartition(HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();
        if (endpoint == null)
        {
            return RateLimitPartition.GetNoLimiter("no-endpoint");
        }

        var requestsPerMinute = GetEndpointRequestsPerMinute(endpoint);
        if (requestsPerMinute == null)
        {
            return RateLimitPartition.GetNoLimiter(GetEndpointPartitionKey(httpContext));
        }

        var permitLimit = Math.Max(1, requestsPerMinute.Value);
        var partitionKey = GetEndpointPartitionKey(httpContext);

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            QueueLimit = 0,
            Window = TimeSpan.FromMinutes(1),
            AutoReplenishment = true
        });
    }

    public Task RecordEndpointRateLimitHit(HttpContext httpContext, int? retryAfterSeconds = null, CancellationToken cancellationToken = default)
    {
        var endpoint = httpContext.GetEndpoint();

        return _throttleEventLogService.TryWriteAsync(
            ThrottleProfile.EndpointRateLimit,
            RequestThrottleKeyType.EndpointRateLimit,
            null,
            GetEndpointRequestsPerMinute(endpoint) ?? _rateLimitOptions.DefaultRequestsPerMinute,
            TimeSpan.FromMinutes(1),
            retryAfterSeconds ?? 60,
            httpContext,
            cancellationToken);
    }

    private int? GetEndpointRequestsPerMinute(Endpoint? endpoint)
    {
        var throttleAttribute = endpoint?.Metadata.GetMetadata<ThrottleAttribute>();
        if (throttleAttribute == null)
        {
            return _rateLimitOptions.DefaultRequestsPerMinute;
        }

        return throttleAttribute.RequestsPerMinute > 0
            ? throttleAttribute.RequestsPerMinute
            : null;
    }

    private string GetEndpointPartitionKey(HttpContext httpContext)
    {
        var endpointKey = RequestThrottleKeyHelper.CreateKeyValue([
            httpContext.Request.Method,
            RequestThrottleKeyHelper.GetRequestPathKey(httpContext)
        ]);

        if (_requestUser.IsAuthenticated)
        {
            return RequestThrottleKeyHelper.CreateKey(RequestThrottleKeyType.EndpointRateLimit, [$"user:{_requestUser.Id}", endpointKey]);
        }

        var ipAddress = RequestThrottleKeyHelper.GetClientIpAddress(httpContext);
        return RequestThrottleKeyHelper.CreateKey(RequestThrottleKeyType.EndpointRateLimit, [$"ip:{ipAddress}", endpointKey]);
    }

    #endregion

    #region Throttle Enforcement

    public async Task CountAttempt(
        ThrottleProfile profile,
        string? identifier)
    {
        EnsureEndpointIsThrottleMarked(profile);

        ExceededThrottleResult? exceededThrottle = null;

        foreach (var rule in GetRules(profile, identifier))
        {
            if (rule.Limit <= 0)
            {
                continue;
            }

            var state = GetState(RequestThrottleKeyHelper.CreateKey(rule.KeyType, rule.KeyParts), rule.Window);
            int count;

            lock (state.SyncRoot)
            {
                state.Count++;
                count = state.Count;
            }

            if (count <= rule.Limit || exceededThrottle != null)
            {
                continue;
            }

            var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((state.ExpiresOn - DateTimeOffset.UtcNow).TotalSeconds));
            exceededThrottle = new ExceededThrottleResult(rule, retryAfterSeconds);
        }

        if (exceededThrottle != null)
        {
            await _throttleEventLogService.TryWriteAsync(
                exceededThrottle.Rule.Profile,
                exceededThrottle.Rule.KeyType,
                exceededThrottle.Rule.NormalizedIdentifier,
                exceededThrottle.Rule.Limit,
                exceededThrottle.Rule.Window,
                exceededThrottle.RetryAfterSeconds,
                _httpContextAccessor.HttpContext);
            throw ApiExceptionDictionary.TooManyRequests(
                exceededThrottle.Rule.TooManyRequestsMessage,
                exceededThrottle.RetryAfterSeconds);
        }

        return;
    }

    private void EnsureEndpointIsThrottleMarked(ThrottleProfile expectedProfile)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var endpoint = httpContext?.GetEndpoint();
        var requestPath = RequestThrottleKeyHelper.GetRequestPathKey(httpContext);

        if (endpoint == null)
        {
            throw new InvalidOperationException(
                $"Throttle profile '{expectedProfile}' requires a routed endpoint for request path '{requestPath}'.");
        }

        var throttleAttribute = endpoint.Metadata.GetMetadata<ThrottleAttribute>();
        if (throttleAttribute == null)
        {
            throw new InvalidOperationException(
                $"Endpoint '{requestPath}' must declare [Throttle] before using RequestThrottleService for profile '{expectedProfile}'.");
        }

        if (throttleAttribute.RequestsPerMinute > 0)
        {
            throw new InvalidOperationException(
                $"Endpoint '{requestPath}' must use bare [Throttle] when using RequestThrottleService for profile '{expectedProfile}'.");
        }
    }

    private IReadOnlyList<ThrottleRule> GetRules(
        ThrottleProfile profile,
        string? identifier)
    {
        var normalizedIdentifier = NormalizeIdentifier(profile, identifier);

        IReadOnlyList<ThrottleRule> rules = profile switch
        {
            ThrottleProfile.Login =>
            [
                new(profile,
                    RequestThrottleKeyType.LoginIp, _options.LoginIpLimit,
                    TimeSpan.FromMinutes(_options.LoginWindowMinutes), normalizedIdentifier, [RequestThrottleKeyHelper.GetClientIpAddress(_httpContextAccessor.HttpContext)]),
                new(profile,
                    RequestThrottleKeyType.LoginEmail, _options.LoginEmailLimit,
                    TimeSpan.FromMinutes(_options.LoginWindowMinutes), normalizedIdentifier, [normalizedIdentifier])
            ],
            ThrottleProfile.PasswordReset =>
            [
                new(profile,
                    RequestThrottleKeyType.PasswordResetIp, _options.PasswordResetIpLimit,
                    TimeSpan.FromMinutes(_options.PasswordResetWindowMinutes), normalizedIdentifier, [RequestThrottleKeyHelper.GetClientIpAddress(_httpContextAccessor.HttpContext)]),
                new(profile,
                    RequestThrottleKeyType.PasswordResetEmail, _options.PasswordResetEmailLimit,
                    TimeSpan.FromMinutes(_options.PasswordResetWindowMinutes), normalizedIdentifier, [normalizedIdentifier])
            ],
            ThrottleProfile.FileAccess =>
            [
                new(profile,
                    RequestThrottleKeyType.FileAccess, _options.FileAccessPerUserPerFileLimit,
                    TimeSpan.FromMinutes(_options.FileAccessWindowMinutes),
                    normalizedIdentifier,
                    [$"user:{_requestUser.Id}", $"file:{normalizedIdentifier}"])
            ],
            _ => []
        };

        if (rules.Count == 0)
        {
            throw new InvalidOperationException(
                $"No throttle rules are defined for profile '{profile}'.");
        }

        return rules;
    }

    #endregion

    #region Counter State

    private CounterState GetState(string key, TimeSpan window)
    {
        return _cache.GetOrCreate(key, entry =>
        {
            var expiresOn = new DateTimeOffset(DateTime.UtcNow.Add(window));
            entry.AbsoluteExpiration = expiresOn;
            return new CounterState(expiresOn);
        })!;
    }

    #endregion

    #region Helpers

    private static string NormalizeIdentifier(ThrottleProfile profile, string? identifier)
    {
        return profile switch
        {
            ThrottleProfile.Login or ThrottleProfile.PasswordReset => identifier?.Trim().ToLowerInvariant() ?? string.Empty,
            ThrottleProfile.FileAccess => identifier?.Trim() ?? string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
        };
    }

    private sealed class CounterState
    {
        public CounterState(DateTimeOffset expiresOn)
        {
            ExpiresOn = expiresOn;
        }

        public int Count { get; set; }
        public DateTimeOffset ExpiresOn { get; }
        public object SyncRoot { get; } = new();
    }

    #endregion

    #region Types

    private sealed record ExceededThrottleResult(ThrottleRule Rule, int RetryAfterSeconds);

    private sealed record ThrottleRule(
        ThrottleProfile Profile,
        RequestThrottleKeyType KeyType,
        int Limit,
        TimeSpan Window,
        string? NormalizedIdentifier,
        string?[] KeyParts)
    {
        public string TooManyRequestsMessage =>
            $"Too many attempts. Limit is {Limit} per {GetWindowDescription(Window)}.";

        private static string GetWindowDescription(TimeSpan window)
        {
            if (window.TotalDays >= 1 && window.TotalDays == Math.Truncate(window.TotalDays))
            {
                var days = (int)window.TotalDays;
                return days == 1 ? "1 day" : $"{days} days";
            }

            if (window.TotalHours >= 1 && window.TotalHours == Math.Truncate(window.TotalHours))
            {
                var hours = (int)window.TotalHours;
                return hours == 1 ? "1 hour" : $"{hours} hours";
            }

            if (window.TotalMinutes >= 1 && window.TotalMinutes == Math.Truncate(window.TotalMinutes))
            {
                var minutes = (int)window.TotalMinutes;
                return minutes == 1 ? "1 minute" : $"{minutes} minutes";
            }

            var seconds = Math.Max(1, (int)Math.Ceiling(window.TotalSeconds));
            return seconds == 1 ? "1 second" : $"{seconds} seconds";
        }
    }

    #endregion
}
