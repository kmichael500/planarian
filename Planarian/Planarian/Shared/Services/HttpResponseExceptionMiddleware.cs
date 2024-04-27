using System.Text.Json;
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
            var error = new ApiErrorResponse(e.Message, e.ErrorCode)
            {
                Data = e.Data
            };

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault
            };

            context.Response.StatusCode = e.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(error, options));
        }
        catch (Exception e)
        {
            var error = new ApiErrorResponse("There was an unexpected issue! Please try again or contact support!", -1);
            
            #if DEBUG
            
            error = new ApiErrorResponse($"There was an unexpected issue: {e.Message}", -1);
            
            #endif

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
    public object? Data { get; set; }
}