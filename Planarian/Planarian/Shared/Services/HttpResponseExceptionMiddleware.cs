using System.Text.Json;
using System.Text.Json.Serialization;
using Planarian.Library.Exceptions;
using Planarian.Library.Options;

namespace Planarian.Shared.Services;

public class HttpResponseExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ServerOptions _serverOptions;

    public HttpResponseExceptionMiddleware(RequestDelegate next, ServerOptions serverOptions)
    {
        _next = next;
        _serverOptions = serverOptions;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException e)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteErrorResponseAsync(context, e.StatusCode, e.Message, e.ErrorCode, e.Data, e.Headers, e.ShowContactInfo, _serverOptions);
        }
        catch (Exception e)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            var error = new ApiErrorResponse(
                $"There was an unexpected issue! This is likely a bug with Planarian. Please contact {_serverOptions.SupportName} at {_serverOptions.SupportEmail}.",
                ApiExceptionType.UnexpectedIssue);

#if DEBUG

            error = new ApiErrorResponse($"There was an unexpected issue: {e.Message}",
                ApiExceptionType.UnexpectedIssue);

#endif

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteErrorResponseAsync(context, context.Response.StatusCode, error.Message, error.ErrorCode, error.Data);
        }
    }

    public static JsonSerializerOptions JsonSerializationOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task WriteErrorResponseAsync(
        HttpContext context,
        int statusCode,
        string message,
        ApiExceptionType errorCode,
        object? data = null,
        IEnumerable<KeyValuePair<string, string>>? headers = null,
        bool showContactInfo = false,
        ServerOptions? serverOptions = null)
    {
        var finalMessage = message;
        if (showContactInfo && serverOptions != null)
        {
            finalMessage =
                $"{message} If you believe this was in error, please contact {serverOptions.SupportName} at {serverOptions.SupportEmail}.";
        }

        var error = new ApiErrorResponse(finalMessage, errorCode)
        {
            Data = data,
            ShowContactInfo = showContactInfo
        };

        if (headers != null)
        {
            foreach (var header in headers)
            {
                context.Response.Headers[header.Key] = header.Value;
            }
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(error, JsonSerializationOptions));
    }
}

public class ApiErrorResponse(string message, ApiExceptionType errorCode)
{
    public string Message { get; } = message;
    public ApiExceptionType ErrorCode { get; } = errorCode;
    public object? Data { get; set; }
    public bool ShowContactInfo { get; set; }
}
