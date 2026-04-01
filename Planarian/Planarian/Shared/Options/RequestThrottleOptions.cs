namespace Planarian.Shared.Options;

/// <summary>
/// Controls request throttling behavior for login and password reset flows.
/// </summary>
public class RequestThrottleOptions
{
    public const string Key = "RequestThrottle";

    /// <summary>
    /// Length of the throttling window, in minutes, for login attempts tracked by IP address and email address.
    /// </summary>
    public int LoginWindowMinutes { get; set; } = 15;

    /// <summary>
    /// Maximum number of login attempts allowed from a single IP address within the login throttling window.
    /// </summary>
    public int LoginIpLimit { get; set; } = 30;

    /// <summary>
    /// Maximum number of login attempts allowed for a single email address within the login throttling window.
    /// </summary>
    public int LoginEmailLimit { get; set; } = 12;

    /// <summary>
    /// Length of the throttling window, in minutes, for password reset requests tracked by IP address and email address.
    /// </summary>
    public int PasswordResetWindowMinutes { get; set; } = 15;

    /// <summary>
    /// Maximum number of password reset requests allowed from a single IP address within the password reset throttling window.
    /// </summary>
    public int PasswordResetIpLimit { get; set; } = 12;

    /// <summary>
    /// Maximum number of password reset requests allowed for a single email address within the password reset throttling window.
    /// </summary>
    public int PasswordResetEmailLimit { get; set; } = 12;

    /// <summary>
    /// Length of the throttling window, in minutes, for repeated file access tracked by user and file.
    /// </summary>
    public int FileAccessWindowMinutes { get; set; } = 1;

    /// <summary>
    /// Maximum number of times a single user can request access to the same file within the file access window.
    /// </summary>
    public int FileAccessPerUserPerFileLimit { get; set; } = 10;
}
