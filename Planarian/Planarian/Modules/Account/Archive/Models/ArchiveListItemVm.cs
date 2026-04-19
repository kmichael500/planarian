namespace Planarian.Modules.Account.Archive.Models;

public class ArchiveListItemVm
{
    public string BlobKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
