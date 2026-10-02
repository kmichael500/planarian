namespace Planarian.Library.Options;

public class AuthOptions
{
    public const string Key = "Auth";
    public string JwtSecret { get; set; } = null!;
    public string JwtIssuer { get; set; } = null!;
    // TODO(security): Revisit the JWT lifetime separately from session revocation.
    public int JwtExpiryDurationSeconds { get; set; } = 2592000;
}
