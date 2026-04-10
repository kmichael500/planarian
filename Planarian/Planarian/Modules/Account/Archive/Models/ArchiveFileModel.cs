namespace Planarian.Modules.Account.Archive.Models;

public class ArchiveFileModel
{
    public string Id { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string? BlobKey { get; set; }
    public string? FileTypeDisplayName { get; set; }
}
