namespace Planarian.Shared.Options;

public class RateLimitOptions
{
    public const string Key = "RateLimit";

    public int DefaultRequestsPerMinute { get; set; } = 240;
}
