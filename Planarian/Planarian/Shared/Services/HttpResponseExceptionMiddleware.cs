using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
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
            var error = new ApiErrorResponse(e.Message, e.ErrorCode)
            {
                Data = e.Data
            };

            context.Response.StatusCode = e.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(error,
                JsonSerializationOptions));
        }
        catch (Exception e)
        {
            var error = new ApiErrorResponse(
                $"There was an unexpected issue! This is likely a bug with Planarian. Please contact {_serverOptions.SupportName} at {_serverOptions.SupportEmail}.",
                ApiExceptionType.UnexpectedIssue);

#if DEBUG

            error = new ApiErrorResponse($"There was an unexpected issue: {e.Message}",
                ApiExceptionType.UnexpectedIssue);

#endif

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(error,
                JsonSerializationOptions));
        }
    }

    public JsonSerializerOptions JsonSerializationOptions => new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

public class ApiErrorResponse(string message, ApiExceptionType errorCode)
{
    public string Message { get; } = message;
    public ApiExceptionType ErrorCode { get; } = errorCode;
    public object? Data { get; set; }
}