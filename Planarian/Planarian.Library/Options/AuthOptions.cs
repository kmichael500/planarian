namespace Planarian.Shared.Options;

public class AuthOptions
{
    public const string Key = "Auth";
    public string JwtSecret { get; set; } = null!;
    public string JwtIssuer { get; set; } = null!;
}