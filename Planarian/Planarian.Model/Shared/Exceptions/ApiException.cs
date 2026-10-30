// Namespace retained for source compatibility; this contract is owned by Planarian.Model.
namespace Planarian.Library.Exceptions;

public class ApiException : Exception
{
    public ApiException(int statusCode, ApiExceptionType errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    public int StatusCode { get; }
    public ApiExceptionType ErrorCode { get; }
    public new object? Data { get; set; }
    public bool ShowContactInfo { get; set; }
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
