namespace Planarian.Shared.Exceptions;

public static class ApiExceptionDictionary
{
    #region Default 1-99

    public static ApiException BadRequest(string message) => new(StatusCodes.Status400BadRequest, 1, message);
    public static ApiException Unauthorized(string message) => new(StatusCodes.Status401Unauthorized, 2, message);
    public static ApiException Forbidden(string message) => new(StatusCodes.Status403Forbidden, 3, message);
    public static ApiException NotFound(string type) => new(StatusCodes.Status404NotFound, 4, $"{type} not found");
    public static ApiException Conflict(string message) => new(StatusCodes.Status409Conflict, 5, message);
    public static ApiException InternalServerError(string message) => new(500, 6, message);

    #endregion

    #region User 100-199

    public static ApiException EmailAlreadyExists => new(StatusCodes.Status400BadRequest, 100, "Email already exists");

    public static ApiException InvalidPasswordComplexity => new(StatusCodes.Status400BadRequest, 101,
        "The password does not meet the complexity requirements");

    public static ApiException InvalidPhoneNumber =>
        new(StatusCodes.Status400BadRequest, 103, "Phone number is invalid");

    #endregion

    #region Authentication 200-299

    public static ApiException EmailDoesNotExist =>
        new(StatusCodes.Status401Unauthorized, 200, "Email does not exist");
    public static ApiException InvalidPassword =>
        new(StatusCodes.Status401Unauthorized, 201, "Password is invalid");
    
    #endregion
}