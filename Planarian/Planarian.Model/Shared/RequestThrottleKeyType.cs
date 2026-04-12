namespace Planarian.Model.Shared;

public enum RequestThrottleKeyType
{
    EndpointRateLimit,
    FileAccess,
    LoginIp,
    LoginEmail,
    PasswordResetIp,
    PasswordResetEmail
}
