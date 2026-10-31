namespace Planarian.Shared.Options;

public class RequestThrottleOptions
{
    public const string Key = "RequestThrottle";

    // Fallback for endpoints without an explicit [Throttle] profile.
    public int DefaultRequestsPerMinute { get; set; } = 240;

    public int LoginWindowMinutes { get; set; } = 15;
    public int LoginIpLimit { get; set; } = 30;
    public int LoginEmailLimit { get; set; } = 12;

    public int PasswordResetWindowMinutes { get; set; } = 15;
    public int PasswordResetIpLimit { get; set; } = 12;
    public int PasswordResetEmailLimit { get; set; } = 12;

    public int EmailConfirmationWindowMinutes { get; set; } = 15;
    public int EmailConfirmationIpLimit { get; set; } = 12;
    public int EmailConfirmationEmailLimit { get; set; } = 5;

    public int FileAccessWindowMinutes { get; set; } = 10;
    public int FileAccessPerUserLimit { get; set; } = 120;
    public int FileAccessPerUserPerFileLimit { get; set; } = 20;

    // Per application instance.
    public int MaxConcurrentUploads { get; set; } = 3;
    public long ChunkedUploadMaxFileSizeBytes { get; set; } = 550L * 1024 * 1024;
    public long ChunkedUploadMaxChunkSizeBytes { get; set; } = 64L * 1024 * 1024;
}
