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
    private readonly RequestThrottleOptions _options;
    private readonly RequestUser _requestUser;
    private readonly ThrottleEventLogService _throttleEventLogService;

    public RequestThrottleService(
        MemoryCache cache,
        RequestThrottleOptions options,
        IHttpContextAccessor httpContextAccessor,
        RequestUser requestUser,
        ThrottleEventLogService throttleEventLogService)
    {
        _cache = cache;
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
            GetEndpointRequestsPerMinute(endpoint) ?? _options.DefaultRequestsPerMinute,
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
            return _options.DefaultRequestsPerMinute;
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
        var resolvedRules = GetRules(profile, identifier)
            .Where(rule => rule.Limit > 0)
            .Select(rule =>
            {
                var cacheKey = RequestThrottleKeyHelper.CreateKey(rule.KeyType, rule.KeyParts);
                return new ResolvedThrottleRule(rule, cacheKey, GetState(cacheKey, rule.Window));
            })
            .ToList();

        ExecuteWithLocks(resolvedRules, () =>
        {
            var exceededRule = resolvedRules.FirstOrDefault(rule => rule.State.Count + 1 > rule.Rule.Limit);
            if (exceededRule != null)
            {
                var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((exceededRule.State.ExpiresOn - DateTimeOffset.UtcNow).TotalSeconds));
                exceededThrottle = new ExceededThrottleResult(exceededRule.Rule, retryAfterSeconds);
                return;
            }

            foreach (var rule in resolvedRules)
            {
                rule.State.Count++;
            }
        });

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

    private static void ExecuteWithLocks(
        IReadOnlyCollection<ResolvedThrottleRule> resolvedRules,
        Action action)
    {
        var acquiredLocks = new Stack<object>();

        try
        {
            foreach (var state in resolvedRules
                         .OrderBy(rule => rule.CacheKey, StringComparer.Ordinal)
                         .Select(rule => rule.State))
            {
                Monitor.Enter(state.SyncRoot);
                acquiredLocks.Push(state.SyncRoot);
            }

            action();
        }
        finally
        {
            while (acquiredLocks.Count > 0)
            {
                Monitor.Exit(acquiredLocks.Pop());
            }
        }
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
                    RequestThrottleKeyType.FileAccessUser, _options.FileAccessPerUserLimit,
                    TimeSpan.FromMinutes(_options.FileAccessWindowMinutes),
                    normalizedIdentifier,
                    [$"user:{_requestUser.Id}"],
                    "Too many file access attempts. Limit is {limit} across all files per {window}."),
                new(profile,
                    RequestThrottleKeyType.FileAccess, _options.FileAccessPerUserPerFileLimit,
                    TimeSpan.FromMinutes(_options.FileAccessWindowMinutes),
                    normalizedIdentifier,
                    [$"user:{_requestUser.Id}", $"file:{normalizedIdentifier}"],
                    "Too many file access attempts. Limit is {limit} for the same file per {window}.")
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
    private sealed record ResolvedThrottleRule(ThrottleRule Rule, string CacheKey, CounterState State);

    private sealed record ThrottleRule(
        ThrottleProfile Profile,
        RequestThrottleKeyType KeyType,
        int Limit,
        TimeSpan Window,
        string? NormalizedIdentifier,
        string?[] KeyParts,
        string? TooManyRequestsMessageTemplate = null)
    {
        public string TooManyRequestsMessage =>
            TooManyRequestsMessageTemplate?
                .Replace("{limit}", Limit.ToString())
                .Replace("{window}", GetWindowDescription(Window))
            ?? $"Too many attempts. Limit is {Limit} per {GetWindowDescription(Window)}.";

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
