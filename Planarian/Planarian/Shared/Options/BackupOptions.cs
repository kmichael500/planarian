namespace Planarian.Shared.Options;

public class BackupOptions
{
    public const string Key = "Backup";

    public int BlobDownloadTransferConcurrency { get; set; } = 8;
    public int BlobDownloadTransferSizeMb { get; set; } = 8;
    public string? TempDirectory { get; set; }
}
