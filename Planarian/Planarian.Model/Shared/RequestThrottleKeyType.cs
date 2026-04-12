namespace Planarian.Model.Shared;

public enum RequestThrottleKeyType
{
    EndpointRateLimit,
    FileAccessUser,
    FileAccess,
    LoginIp,
    LoginEmail,
    PasswordResetIp,
    PasswordResetEmail
}
