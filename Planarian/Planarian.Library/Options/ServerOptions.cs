namespace Planarian.Library.Options;

public class ServerOptions
{
    public const string Key = "Server";
    public string SqlConnectionString { get; set; } = null!;
    public string ClientBaseUrl { get; set; } = null!;
    public string ServerBaseUrl { get; set; } = null!;
    public string AllowedCorsOrigins { get; set; } = string.Empty;
}