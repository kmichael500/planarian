namespace Planarian.Library.Exceptions;

public enum ApiExceptionType
{
    BadRequest = 1,
    Unauthorized = 2,
    Forbidden = 3,
    NotFound = 4,
    Conflict = 5,
    InternalServerError = 6,
    EmailAlreadyExists = 100,
    InvalidPasswordComplexity = 101,
    InvalidPhoneNumber = 102,
    InvalidEmailConfirmationCode = 103,
    EmailNotConfirmed = 104,
    EmailDoesNotExist = 200,
    InvalidPassword = 201,
    PasswordResetCodeExpired = 202,
    InvalidPasswordResetCode = 203,
    MessageTypeNotFound = 300,
    EmailFailedToSend = 301,
    NoAccount,
    EntranceRequired = 303,
    InvalidImport = 400,
    NullValue = 401,
    QueryInvalidValue = 500,
    UnexpectedIssue = -1
}