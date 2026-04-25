using Microsoft.AspNetCore.Http;

namespace Planarian.Library.Exceptions;

public static class ApiExceptionDictionary
{
    #region Default 1-99

    public static ApiException BadRequest(string message)
    {
        return new ApiException(StatusCodes.Status400BadRequest, ApiExceptionType.BadRequest, message);
    }

    public static ApiException Unauthorized(string message)
    {
        return new ApiException(StatusCodes.Status401Unauthorized, ApiExceptionType.Unauthorized, message);
    }

    public static ApiException Forbidden(string message)
    {
        return new ApiException(StatusCodes.Status403Forbidden, ApiExceptionType.Forbidden, message);
    }

    public static ApiException NotFound(string type)
    {
        return new ApiException(StatusCodes.Status404NotFound, ApiExceptionType.NotFound, $"{type} not found");
    }

    public static ApiException Conflict(string message)
    {
        return new ApiException(StatusCodes.Status409Conflict, ApiExceptionType.Conflict, message);
    }

    public static ApiException FromErrorCode(string? errorCode, string message)
    {
        return FromErrorCode(errorCode, message, null);
    }

    public static ApiException FromErrorCode(string? errorCode, string message, object? data)
    {
        if (!Enum.TryParse<ApiExceptionType>(errorCode, out var apiExceptionType))
        {
            return WithData(BadRequest(message), data);
        }

        var exception = apiExceptionType switch
        {
            ApiExceptionType.BadRequest => BadRequest(message),
            ApiExceptionType.NotFound => new ApiException(StatusCodes.Status404NotFound, ApiExceptionType.NotFound, message),
            ApiExceptionType.Conflict => Conflict(message),
            ApiExceptionType.SessionCancelled => SessionCancelled(message),
            ApiExceptionType.TooManyRequests => TooManyRequests(message),
            ApiExceptionType.Unauthorized => Unauthorized(message),
            ApiExceptionType.Forbidden => Forbidden(message),
            ApiExceptionType.InternalServerError => InternalServerError(message),
            _ => new ApiException(StatusCodes.Status400BadRequest, apiExceptionType, message)
        };

        return WithData(exception, data);
    }

    public static ApiException TooManyRequests(string message)
    {
        return new ApiException(StatusCodes.Status429TooManyRequests, ApiExceptionType.TooManyRequests, message)
        {
            ShowContactInfo = true
        };
    }

    public static ApiException TooManyRequests(string message, int retryAfterSeconds)
    {
        var exception = TooManyRequests(message);
        exception.Headers["Retry-After"] = retryAfterSeconds.ToString();
        return exception;
    }

    public static ApiException InternalServerError(string message)
    {
        return new ApiException(500, ApiExceptionType.InternalServerError, message);
    }

    private static ApiException WithData(ApiException exception, object? data)
    {
        exception.Data = data;
        return exception;
    }

    #endregion

    #region User 100-199

    public static ApiException EmailAlreadyExists => new(StatusCodes.Status400BadRequest, ApiExceptionType.EmailAlreadyExists, "Email already exists");

    public static ApiException InvalidPasswordComplexity => new(StatusCodes.Status400BadRequest, ApiExceptionType.InvalidPasswordComplexity,
        "The password does not meet the complexity requirements");

    public static ApiException InvalidPhoneNumber =>
        new(StatusCodes.Status400BadRequest, ApiExceptionType.InvalidPhoneNumber, "Phone number is invalid");

    public static ApiException InvalidEmailConfirmationCode =>
        new(StatusCodes.Status400BadRequest, ApiExceptionType.InvalidEmailConfirmationCode, "The email confirmation code does not exist");

    public static ApiException EmailNotConfirmed => new(StatusCodes.Status400BadRequest, ApiExceptionType.EmailNotConfirmed,
        "Please confirm your email! A new confirmation code has been sent to your email address.");

    #endregion

    #region Authentication 200-299

    public static ApiException EmailDoesNotExist =>
        new(StatusCodes.Status401Unauthorized, ApiExceptionType.EmailDoesNotExist, "Email does not exist");

    public static ApiException InvalidPassword =>
        new(StatusCodes.Status401Unauthorized, ApiExceptionType.InvalidPassword, "Password is invalid");

    public static ApiException PasswordResetCodeExpired =>
        new(StatusCodes.Status401Unauthorized, ApiExceptionType.PasswordResetCodeExpired, "Password reset code expired");

    public static ApiException InvalidPasswordResetCode =>
        new(StatusCodes.Status500InternalServerError, ApiExceptionType.InvalidPasswordResetCode, "The code does not exist");

    public static ApiException UserAlreadyInAccount =>
        new(StatusCodes.Status400BadRequest, ApiExceptionType.UserAlreadyInAccount, "You are already have access to this account");

    #endregion

    #region Email Issues 300-399

    public static ApiException MessageTypeNotFound =>
        new(StatusCodes.Status500InternalServerError, ApiExceptionType.MessageTypeNotFound, "There was an issue");

    public static ApiException EmailFailedToSend =>
        new(StatusCodes.Status500InternalServerError, ApiExceptionType.EmailFailedToSend, "The email failed to send");

    #endregion

    public static ApiException NoAccount =>
        new(StatusCodes.Status500InternalServerError, ApiExceptionType.NoAccount, "No account was found");

    public static Exception InvalidPermission =>
        new ApiException(StatusCodes.Status400BadRequest, ApiExceptionType.BadRequest, "Invalid permission key");

    public static ApiException EntranceRequired(string atLeastEntranceIsRequired)
    {
        return new ApiException(StatusCodes.Status400BadRequest, ApiExceptionType.EntranceRequired, atLeastEntranceIsRequired);
    }

    #region Import 400-499

    public static ApiException InvalidImport<T>(IEnumerable<FailedCaveCsvRecord<T>> failedCaveRecords,
        ImportType importType)
    {
        return new ApiException(StatusCodes.Status400BadRequest, ApiExceptionType.InvalidImport,
                $"Failed {importType.ToString().ToLower()} import")
            { Data = failedCaveRecords };
    }

    public static ApiException NullValue(string name)
    {
        return new ApiException(StatusCodes.Status400BadRequest, ApiExceptionType.NullValue, $"Value is missing from '{name}'");
    }

    public static ApiException SessionCancelled(string message = "Upload session was canceled.")
    {
        return new ApiException(StatusCodes.Status409Conflict, ApiExceptionType.SessionCancelled, message);
    }

    #endregion

    #region Query 500-599

    public static Exception QueryInvalidValue(string field, string value)
    {
        return new ApiException(StatusCodes.Status400BadRequest, ApiExceptionType.QueryInvalidValue,
            $"Invalid value '{value}' for field '{field}'");
    }

    #endregion
}
