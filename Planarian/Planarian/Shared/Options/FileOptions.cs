namespace Planarian.Shared.Options;

public class FileOptions
{
    public const string Key = "File";

    public string ConnectionString { get; set; } = null!;
    public int SasLinkExpirationSeconds { get; set; } = 15;
}
