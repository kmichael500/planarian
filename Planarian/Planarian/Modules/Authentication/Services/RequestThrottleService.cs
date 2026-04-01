using System.Threading.RateLimiting;
using Planarian.Model.Database.Entities;
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
    private readonly ApplicationEventLogService _applicationEventLogService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RateLimitOptions _rateLimitOptions;
    private readonly RequestThrottleOptions _options;
    private readonly RequestUser _requestUser;
    
    public RequestThrottleService(
        MemoryCache cache,
        ApplicationEventLogService applicationEventLogService,
        RateLimitOptions rateLimitOptions,
        RequestThrottleOptions options,
        IHttpContextAccessor httpContextAccessor,
        RequestUser requestUser)
    {
        _cache = cache;
        _applicationEventLogService = applicationEventLogService;
        _rateLimitOptions = rateLimitOptions;
        _options = options;
        _httpContextAccessor = httpContextAccessor;
        _requestUser = requestUser;
    }
    
    #endregion
    
    #region Login Throttling

    public async Task EnsureLoginAllowed(string? email)
    {
        await EnsureAllowed(ThrottleOperation.Login, ApplicationEventScope.LoginThrottled, email);
    }

    public void ClearLoginFailures(string? email)
    {
        Clear(
            ThrottleOperation.Login,
            ApplicationEventScope.LoginThrottled,
            email,
            [RequestThrottleKeyType.LoginEmail]);
    }

    #endregion

    #region Password Reset Throttling

    public async Task EnsurePasswordResetAllowed(string? email)
    {
        await EnsureAllowed(ThrottleOperation.PasswordReset, ApplicationEventScope.PasswordResetThrottled, email);
    }

    #endregion

    #region File Access Throttling

    public async Task EnsureFileAccessAllowed(string fileId)
    {
        await EnsureAllowed(
            ThrottleOperation.FileAccess,
            ApplicationEventScope.FileAccessThrottled,
            fileId,
            countAttemptOnSuccess: true);
    }

    #endregion

    #region Counted Events

    internal async Task CountAttempts(
        ThrottleOperation operation,
        ApplicationEventScope scope,
        string? identifier,
        object? data = null)
    {
        foreach (var rule in GetRules(operation, scope, identifier))
        {
            var state = GetState(RequestThrottleKeyHelper.CreateKey(rule.KeyType, rule.KeyParts), rule.Window);
            lock (state.SyncRoot)
            {
                state.Count++;
            }

            await AggregateEventAsync(rule, state, data);
        }
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
        var permitLimit = Math.Max(1, requestsPerMinute);
        var partitionKey = GetEndpointPartitionKey(httpContext);

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            QueueLimit = 0,
            Window = TimeSpan.FromMinutes(1),
            AutoReplenishment = true
        });
    }

    public async Task RecordEndpointRateLimitHit(HttpContext httpContext)
    {
        var partitionKey = GetEndpointPartitionKey(httpContext);
        var window = GetWindow(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        var aggregationKey = RequestThrottleKeyHelper.CreateAggregationKey(
            ApplicationEventType.EndpointRateLimitThrottled,
            RequestThrottleKeyType.EndpointRateLimit,
            partitionKey,
            window.StartedOn,
            ApplicationEventScope.EndpointRateLimitThrottled);

        await _applicationEventLogService.UpsertAsync(
            ApplicationEventCategory.Security,
            ApplicationEventType.EndpointRateLimitThrottled,
            aggregationKey,
            window.StartedOn,
            window.EndsOn,
            ApplicationEventScope.EndpointRateLimitThrottled);
    }

    private int GetEndpointRequestsPerMinute(Endpoint? endpoint)
    {
        var endpointRateLimit = endpoint?.Metadata.GetMetadata<RateLimitAttribute>();
        return endpointRateLimit?.RequestsPerMinute ?? _rateLimitOptions.DefaultRequestsPerMinute;
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

    private async Task EnsureAllowed(
        ThrottleOperation operation,
        ApplicationEventScope scope,
        string? identifier,
        bool countAttemptOnSuccess = false)
    {
        foreach (var rule in GetRules(operation, scope, identifier))
        {
            if (rule.Limit <= 0)
            {
                continue;
            }

            var state = GetState(RequestThrottleKeyHelper.CreateKey(rule.KeyType, rule.KeyParts), rule.Window);
            int? retryAfter = null;

            lock (state.SyncRoot)
            {
                if (state.Count >= rule.Limit)
                {
                    retryAfter = Math.Max(1, (int)Math.Ceiling((state.ExpiresOn - DateTimeOffset.UtcNow).TotalSeconds));
                }
                else if (countAttemptOnSuccess)
                {
                    state.Count++;
                }
            }

            if (retryAfter == null)
            {
                continue;
            }

            await AggregateEventAsync(rule, state);

            throw ApiExceptionDictionary.TooManyRequests(rule.TooManyRequestsMessage, retryAfter.Value);
        }
    }

    private void Clear(
        ThrottleOperation operation,
        ApplicationEventScope scope,
        string? identifier,
        IEnumerable<RequestThrottleKeyType>? keyTypes = null)
    {
        HashSet<RequestThrottleKeyType>? filter = keyTypes?.ToHashSet();

        foreach (var rule in GetRules(operation, scope, identifier))
        {
            if (filter != null && !filter.Contains(rule.KeyType))
            {
                continue;
            }

            _cache.Remove(RequestThrottleKeyHelper.CreateKey(rule.KeyType, rule.KeyParts));
        }
    }

    private IReadOnlyList<ThrottleRule> GetRules(
        ThrottleOperation operation,
        ApplicationEventScope scope,
        string? identifier)
    {
        var normalizedIdentifier = NormalizeIdentifier(operation, identifier);
        var ipAddress = RequestThrottleKeyHelper.GetClientIpAddress(_httpContextAccessor.HttpContext);

        IReadOnlyList<ThrottleRule> rules = (operation, scope) switch
        {
            (ThrottleOperation.Login, ApplicationEventScope.LoginFailureEmailDoesNotExist) =>
            [
                new(operation, ApplicationEventType.LoginFailure, ApplicationEventScope.LoginFailureEmailDoesNotExist,
                    RequestThrottleKeyType.LoginIp, _options.LoginIpLimit,
                    TimeSpan.FromMinutes(_options.LoginWindowMinutes), normalizedIdentifier, [ipAddress]),
                new(operation, ApplicationEventType.LoginFailure, ApplicationEventScope.LoginFailureEmailDoesNotExist,
                    RequestThrottleKeyType.LoginEmail, _options.LoginEmailLimit,
                    TimeSpan.FromMinutes(_options.LoginWindowMinutes), normalizedIdentifier, [normalizedIdentifier])
            ],
            (ThrottleOperation.Login, ApplicationEventScope.LoginFailureInvalidPassword) =>
            [
                new(operation, ApplicationEventType.LoginFailure, ApplicationEventScope.LoginFailureInvalidPassword,
                    RequestThrottleKeyType.LoginIp, _options.LoginIpLimit,
                    TimeSpan.FromMinutes(_options.LoginWindowMinutes), normalizedIdentifier, [ipAddress]),
                new(operation, ApplicationEventType.LoginFailure, ApplicationEventScope.LoginFailureInvalidPassword,
                    RequestThrottleKeyType.LoginEmail, _options.LoginEmailLimit,
                    TimeSpan.FromMinutes(_options.LoginWindowMinutes), normalizedIdentifier, [normalizedIdentifier])
            ],
            (ThrottleOperation.Login, ApplicationEventScope.LoginFailureUnconfirmedEmail) =>
            [
                new(operation, ApplicationEventType.LoginFailure, ApplicationEventScope.LoginFailureUnconfirmedEmail,
                    RequestThrottleKeyType.LoginIp, _options.LoginIpLimit,
                    TimeSpan.FromMinutes(_options.LoginWindowMinutes), normalizedIdentifier, [ipAddress]),
                new(operation, ApplicationEventType.LoginFailure, ApplicationEventScope.LoginFailureUnconfirmedEmail,
                    RequestThrottleKeyType.LoginEmail, _options.LoginEmailLimit,
                    TimeSpan.FromMinutes(_options.LoginWindowMinutes), normalizedIdentifier, [normalizedIdentifier])
            ],
            (ThrottleOperation.Login, ApplicationEventScope.LoginThrottled) =>
            [
                new(operation, ApplicationEventType.LoginThrottled, ApplicationEventScope.LoginThrottled,
                    RequestThrottleKeyType.LoginIp, _options.LoginIpLimit,
                    TimeSpan.FromMinutes(_options.LoginWindowMinutes), normalizedIdentifier, [ipAddress]),
                new(operation, ApplicationEventType.LoginThrottled, ApplicationEventScope.LoginThrottled,
                    RequestThrottleKeyType.LoginEmail, _options.LoginEmailLimit,
                    TimeSpan.FromMinutes(_options.LoginWindowMinutes), normalizedIdentifier, [normalizedIdentifier])
            ],
            (ThrottleOperation.PasswordReset, ApplicationEventScope.PasswordResetRequested) =>
            [
                new(operation, ApplicationEventType.PasswordResetRequested, ApplicationEventScope.PasswordResetRequested,
                    RequestThrottleKeyType.PasswordResetIp, _options.PasswordResetIpLimit,
                    TimeSpan.FromMinutes(_options.PasswordResetWindowMinutes), normalizedIdentifier, [ipAddress]),
                new(operation, ApplicationEventType.PasswordResetRequested, ApplicationEventScope.PasswordResetRequested,
                    RequestThrottleKeyType.PasswordResetEmail, _options.PasswordResetEmailLimit,
                    TimeSpan.FromMinutes(_options.PasswordResetWindowMinutes), normalizedIdentifier, [normalizedIdentifier])
            ],
            (ThrottleOperation.PasswordReset, ApplicationEventScope.PasswordResetThrottled) =>
            [
                new(operation, ApplicationEventType.PasswordResetThrottled, ApplicationEventScope.PasswordResetThrottled,
                    RequestThrottleKeyType.PasswordResetIp, _options.PasswordResetIpLimit,
                    TimeSpan.FromMinutes(_options.PasswordResetWindowMinutes), normalizedIdentifier, [ipAddress]),
                new(operation, ApplicationEventType.PasswordResetThrottled, ApplicationEventScope.PasswordResetThrottled,
                    RequestThrottleKeyType.PasswordResetEmail, _options.PasswordResetEmailLimit,
                    TimeSpan.FromMinutes(_options.PasswordResetWindowMinutes), normalizedIdentifier, [normalizedIdentifier])
            ],
            (ThrottleOperation.FileAccess, ApplicationEventScope.FileAccessThrottled) =>
            [
                new(operation, ApplicationEventType.FileAccessThrottled, ApplicationEventScope.FileAccessThrottled,
                    RequestThrottleKeyType.FileAccess, _options.FileAccessPerUserPerFileLimit,
                    TimeSpan.FromMinutes(_options.FileAccessWindowMinutes), normalizedIdentifier,
                    [$"user:{_requestUser.Id}", $"file:{normalizedIdentifier}"])
            ],
            _ => []
        };

        if (rules.Count == 0)
        {
            throw new InvalidOperationException(
                $"No throttle rules are defined for operation '{operation}' and scope '{scope}'.");
        }

        return rules;
    }

    #endregion

    #region Rule Resolution

    private CounterState GetState(string key, TimeSpan window)
    {
        return _cache.GetOrCreate(key, entry =>
        {
            var startedOn = DateTime.UtcNow;
            var expiresOn = new DateTimeOffset(startedOn.Add(window));
            entry.AbsoluteExpiration = expiresOn;
            return new CounterState(startedOn, expiresOn);
        })!;
    }

    #endregion

    #region Event Logging

    private async Task AggregateEventAsync(
        ThrottleRule rule,
        CounterState state,
        object? data = null)
    {
        var partition = RequestThrottleKeyHelper.CreateKeyValue(rule.KeyParts);
        var aggregationKey = RequestThrottleKeyHelper.CreateAggregationKey(
            rule.EventType,
            rule.KeyType,
            partition,
            state.StartedOn,
            rule.Scope);

        await _applicationEventLogService.UpsertAsync(
            ApplicationEventCategory.Security,
            rule.EventType,
            aggregationKey,
            state.StartedOn,
            state.ExpiresOn.UtcDateTime,
            rule.Scope,
            rule.NormalizedIdentifier,
            SerializeData(data));
    }

    #endregion

    #region Helpers

    private static string NormalizeIdentifier(ThrottleOperation operation, string? identifier)
    {
        return operation switch
        {
            ThrottleOperation.Login or ThrottleOperation.PasswordReset => identifier?.Trim().ToLowerInvariant() ?? string.Empty,
            ThrottleOperation.FileAccess => identifier?.Trim() ?? string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
    }

    private static WindowRange GetWindow(DateTime utcNow, TimeSpan window)
    {
        var windowTicks = window.Ticks;
        var startedOnTicks = utcNow.Ticks - (utcNow.Ticks % windowTicks);
        var startedOn = new DateTime(startedOnTicks, DateTimeKind.Utc);
        return new WindowRange(startedOn, startedOn.Add(window));
    }

    private static string? SerializeData(object? data)
    {
        return data == null ? null : System.Text.Json.JsonSerializer.Serialize(data);
    }

    private sealed class CounterState
    {
        public CounterState(DateTime startedOn, DateTimeOffset expiresOn)
        {
            StartedOn = startedOn;
            ExpiresOn = expiresOn;
        }

        public int Count { get; set; }
        public DateTime StartedOn { get; }
        public DateTimeOffset ExpiresOn { get; }
        public object SyncRoot { get; } = new();
    }

    private sealed record WindowRange(DateTime StartedOn, DateTime EndsOn);

    #endregion

    #region Nested Types

    internal enum ThrottleOperation
    {
        Login,
        PasswordReset,
        FileAccess
    }

    private sealed record ThrottleRule(
        ThrottleOperation Operation,
        ApplicationEventType EventType,
        ApplicationEventScope Scope,
        RequestThrottleKeyType KeyType,
        int Limit,
        TimeSpan Window,
        string NormalizedIdentifier,
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
