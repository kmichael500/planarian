namespace Planarian.Modules.Account.Model;

public class ArchiveListItemVm
{
    public string BlobKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}
