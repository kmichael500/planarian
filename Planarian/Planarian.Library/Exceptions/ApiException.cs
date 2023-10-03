namespace Planarian.Library.Exceptions;

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
    public object? Data { get; set; }
}