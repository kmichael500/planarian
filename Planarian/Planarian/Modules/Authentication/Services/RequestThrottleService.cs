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
        var normalizedEmail = NormalizeEmail(email);
        await EnsureAllowed(
            ThrottleOperation.Login,
            normalizedEmail,
            ApplicationEventType.LoginThrottled);
    }

    public async Task RecordLoginFailure(string? email, LoginFailureReason reason)
    {
        var normalizedEmail = NormalizeEmail(email);
        var message = reason switch
        {
            LoginFailureReason.EmailDoesNotExist => "Email does not exist",
            LoginFailureReason.InvalidPassword => "Password is invalid",
            LoginFailureReason.UnconfirmedEmail => "Email is not confirmed",
            _ => "Login failed"
        };

        await RecordFailure(
            ThrottleOperation.Login,
            normalizedEmail,
            ApplicationEventType.LoginFailure,
            message,
            new LoginFailureEventData(reason));
    }

    public void ClearLoginFailures(string? email)
    {
        var normalizedEmail = NormalizeEmail(email);

        _cache.Remove(RequestThrottleKeyHelper.CreateKey(RequestThrottleKeyType.LoginEmail, [normalizedEmail]));
    }

    #endregion

    #region Password Reset Throttling

    public async Task EnsurePasswordResetAllowed(string? email)
    {
        await EnsureAllowed(
            ThrottleOperation.PasswordReset,
            NormalizeEmail(email),
            ApplicationEventType.PasswordResetThrottled);
    }

    public async Task RecordPasswordResetRequested(string? email, string message)
    {
        await RecordFailure(
            ThrottleOperation.PasswordReset,
            NormalizeEmail(email),
            ApplicationEventType.PasswordResetRequested,
            message);
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
            window.StartedOn);

        await _applicationEventLogService.UpsertAsync(
            ApplicationEventCategory.Security,
            ApplicationEventType.EndpointRateLimitThrottled,
            aggregationKey,
            window.StartedOn,
            window.EndsOn,
            message: "Rate limit exceeded. Please try again later.");
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
        string normalizedIdentifier,
        ApplicationEventType eventType)
    {
        foreach (var rule in GetRules(operation, normalizedIdentifier))
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
            }

            if (retryAfter == null)
            {
                continue;
            }

            await AggregateEventAsync(rule, state, eventType, normalizedIdentifier, rule.Message);

            throw ApiExceptionDictionary.TooManyRequests(rule.Message, retryAfter.Value);
        }
    }

    private async Task RecordFailure(
        ThrottleOperation operation,
        string normalizedIdentifier,
        ApplicationEventType eventType,
        string message,
        object? data = null)
    {
        foreach (var rule in GetRules(operation, normalizedIdentifier))
        {
            var state = GetState(RequestThrottleKeyHelper.CreateKey(rule.KeyType, rule.KeyParts), rule.Window);
            lock (state.SyncRoot)
            {
                state.Count++;
            }

            await AggregateEventAsync(rule, state, eventType, normalizedIdentifier, message, data);
        }
    }

    private void Clear(ThrottleOperation operation, string normalizedIdentifier)
    {
        foreach (var rule in GetRules(operation, normalizedIdentifier))
        {
            _cache.Remove(RequestThrottleKeyHelper.CreateKey(rule.KeyType, rule.KeyParts));
        }
    }

    private IReadOnlyList<ThrottleRule> GetRules(ThrottleOperation operation, string normalizedIdentifier)
    {
        var ipAddress = RequestThrottleKeyHelper.GetClientIpAddress(_httpContextAccessor.HttpContext);

        return operation switch
        {
            ThrottleOperation.Login =>
            [
                new ThrottleRule(RequestThrottleKeyType.LoginIp, _options.LoginIpLimit,
                    TimeSpan.FromMinutes(_options.LoginWindowMinutes),
                    "Too many login attempts from this IP address.", [ipAddress]),
                new ThrottleRule(RequestThrottleKeyType.LoginEmail, _options.LoginEmailLimit,
                    TimeSpan.FromMinutes(_options.LoginWindowMinutes),
                    "Too many login attempts for this email address.", [normalizedIdentifier])
            ],
            ThrottleOperation.PasswordReset =>
            [
                new ThrottleRule(RequestThrottleKeyType.PasswordResetIp, _options.PasswordResetIpLimit,
                    TimeSpan.FromMinutes(_options.PasswordResetWindowMinutes),
                    "Too many password reset requests from this IP address.", [ipAddress]),
                new ThrottleRule(RequestThrottleKeyType.PasswordResetEmail, _options.PasswordResetEmailLimit,
                    TimeSpan.FromMinutes(_options.PasswordResetWindowMinutes),
                    "Too many password reset requests for this email address.", [normalizedIdentifier])
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
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
        ApplicationEventType eventType,
        string normalizedIdentifier,
        string message,
        object? data = null)
    {
        var partition = RequestThrottleKeyHelper.CreateKeyValue(rule.KeyParts);
        var aggregationKey = RequestThrottleKeyHelper.CreateAggregationKey(
            eventType,
            rule.KeyType,
            partition,
            state.StartedOn);

        await _applicationEventLogService.UpsertAsync(
            ApplicationEventCategory.Security,
            eventType,
            aggregationKey,
            state.StartedOn,
            state.ExpiresOn.UtcDateTime,
            normalizedIdentifier,
            message,
            SerializeData(data));
    }

    #endregion

    #region Helpers

    private static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? string.Empty;
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

    private enum ThrottleOperation
    {
        Login,
        PasswordReset
    }

    private sealed record ThrottleRule(
        RequestThrottleKeyType KeyType,
        int Limit,
        TimeSpan Window,
        string Message,
        string?[] KeyParts);
    private sealed record LoginFailureEventData(LoginFailureReason Reason);

    #endregion
}
