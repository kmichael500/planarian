using System.Text.Json;
using System.Text.Json.Serialization;
using Planarian.Library.Exceptions;

namespace Planarian.Shared.Services;

public class HttpResponseExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public HttpResponseExceptionMiddleware(bool isDevelopment)
    {
    }

    public HttpResponseExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException e)
        {
            await WriteErrorResponseAsync(context, e.StatusCode, e.Message, e.ErrorCode, e.Data, e.Headers);
        }
        catch (Exception e)
        {
            var error = new ApiErrorResponse("There was an unexpected issue! Please try again or contact support!",
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
        IEnumerable<KeyValuePair<string, string>>? headers = null)
    {
        var error = new ApiErrorResponse(message, errorCode)
        {
            Data = data
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
}
