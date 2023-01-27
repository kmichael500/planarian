namespace Planarian.Shared.Exceptions;

public static class ApiExceptionDictionary
{
    #region Default 1-99

    public static ApiException BadRequest(string message)
    {
        return new ApiException(StatusCodes.Status400BadRequest, 1, message);
    }

    public static ApiException Unauthorized(string message)
    {
        return new ApiException(StatusCodes.Status401Unauthorized, 2, message);
    }

    public static ApiException Forbidden(string message)
    {
        return new ApiException(StatusCodes.Status403Forbidden, 3, message);
    }

    public static ApiException NotFound(string type)
    {
        return new ApiException(StatusCodes.Status404NotFound, 4, $"{type} not found");
    }

    public static ApiException Conflict(string message)
    {
        return new ApiException(StatusCodes.Status409Conflict, 5, message);
    }

    public static ApiException InternalServerError(string message)
    {
        return new ApiException(500, 6, message);
    }

    #endregion

    #region User 100-199

    public static ApiException EmailAlreadyExists => new(StatusCodes.Status400BadRequest, 100, "Email already exists");

    public static ApiException InvalidPasswordComplexity => new(StatusCodes.Status400BadRequest, 101,
        "The password does not meet the complexity requirements");

    public static ApiException InvalidPhoneNumber =>
        new(StatusCodes.Status400BadRequest, 102, "Phone number is invalid");

    public static ApiException InvalidEmailConfirmationCode =>
        new(StatusCodes.Status400BadRequest, 103, "The email confirmation code does not exist");

    public static ApiException EmailNotConfirmed => new(StatusCodes.Status400BadRequest, 14,
        "Please confirm your email! A new confirmation code has been sent to your email address.");

    #endregion

    #region Authentication 200-299

    public static ApiException EmailDoesNotExist =>
        new(StatusCodes.Status401Unauthorized, 200, "Email does not exist");

    public static ApiException InvalidPassword =>
        new(StatusCodes.Status401Unauthorized, 201, "Password is invalid");

    public static ApiException PasswordResetCodeExpired =>
        new(StatusCodes.Status401Unauthorized, 202, "Password reset code expired");

    public static ApiException InvalidPasswordResetCode =>
        new(StatusCodes.Status500InternalServerError, 203, "The code does not exist");

    #endregion

    #region Email Issues 300-399

    public static ApiException MessageTypeNotFound =>
        new(StatusCodes.Status500InternalServerError, 300, "There was an issue");

    public static ApiException EmailFailedToSend =>
        new(StatusCodes.Status500InternalServerError, 301, "The email failed to send");

    #endregion
}