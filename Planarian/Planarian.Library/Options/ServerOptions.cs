namespace Planarian.Library.Options;

public class ServerOptions
{
    public const string Key = "Server";
    public string SqlConnectionString { get; set; } = null!;
    public string AllowedCorsOrigins { get; set; } = string.Empty;
    public Dictionary<string, string> ClientOriginMappings { get; set; } = new();
    public int ThrottleEventLogRetentionDays { get; set; } = 30;
    public string SupportName { get; set; } = "Planarian Team";
    public string SupportEmail { get; set; } = "support@example.com";
}
