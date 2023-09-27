namespace Planarian.Library.Options;

public class ServerOptions
{
    public const string Key = "Server";
    public string SqlConnectionString { get; set; } = null!;
    public string ClientBaseUrl { get; set; } = null!;
    public string? ServerBaseUrl { get; set; }
    public IEnumerable<string> AllowedCorsOrigins { get; set; } = new List<string>();
}