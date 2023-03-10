using System.Text.Json;
using Planarian.Shared.Exceptions;

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
            var error = new ApiErrorResponse(e.Message, e.ErrorCode);

            context.Response.StatusCode = e.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(error,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
        catch (Exception e)
        {
            var error = new ApiErrorResponse("There was an unexpected issue! Please try again or contact support!", -1);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(error,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
    }
}

public class ApiErrorResponse
{
    public ApiErrorResponse(string message, int errorCode)
    {
        Message = message;
        ErrorCode = errorCode;
    }

    public string Message { get; }
    public int ErrorCode { get; }
}