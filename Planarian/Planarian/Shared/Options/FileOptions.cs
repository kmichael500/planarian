namespace Planarian.Shared.Options;

public class FileOptions
{
    public const string Key = "File";

    public string ConnectionString { get; set; } = null!;
    public int SasLinkExpirationSeconds { get; set; } = 15;
    public ICollection<string> ViewSasExtendedFileTypes { get; set; } = [".pdf"];
    public int ViewSasExtendedExpirationSeconds { get; set; } = 300;
}
