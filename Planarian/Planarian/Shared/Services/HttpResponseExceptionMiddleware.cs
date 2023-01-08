using System.Text.Json;

namespace Planarian.Shared.Services;

public class HttpResponseExceptionMiddleware
{
    private readonly RequestDelegate _next;

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
            var error = new ApiErrorResponse(e.Message, -1);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(error,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
    }
}

public class ApiErrorResponse
{
    public ApiErrorResponse(string message, int errorCode) :base ()
    {
        Message = message;
        ErrorCode = errorCode;
    }
  
    public string Message { get; }
    public int ErrorCode { get; }
}

public class ApiException : Exception
{
    public ApiException(int statusCode, int errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
    
    public int StatusCode { get; }
    public int ErrorCode { get; }
}

public class ApiExceptionDictionary
{
    #region Default 1-100

    public static ApiException BadRequest(string message) => new(StatusCodes.Status400BadRequest, 1, message);
    public static ApiException Unauthorized(string message) => new(StatusCodes.Status401Unauthorized, 2, message);
    public static ApiException Forbidden(string message) => new(StatusCodes.Status403Forbidden, 3, message);
    public static ApiException NotFound(string type) => new(StatusCodes.Status404NotFound, 4, $"{type} not found");
    public static ApiException Conflict(string message) => new(StatusCodes.Status409Conflict, 5, message);
    public static ApiException InternalServerError(string message) => new(500, 6, message);

    #endregion

    #region User 101-200

    public static ApiException EmailAlreadyExists => new(StatusCodes.Status400BadRequest, 101, "Email already exists");

    public static ApiException InvalidPassword => new(StatusCodes.Status400BadRequest, 102,
        "The password does not meet the complexity requirements");

    #endregion
}


