using System.Net;
using Planarian.Library.Exceptions;

namespace Planarian.Model.Shared.Exceptions;

internal static class RequestPolicyErrors
{
    public static ApiException BadRequest(string message) =>
        new((int)HttpStatusCode.BadRequest, ApiExceptionType.BadRequest, message);

    public static ApiException Unauthorized(string message) =>
        new((int)HttpStatusCode.Unauthorized, ApiExceptionType.Unauthorized, message);

    public static ApiException Forbidden(string message) =>
        new((int)HttpStatusCode.Forbidden, ApiExceptionType.Forbidden, message);
}
