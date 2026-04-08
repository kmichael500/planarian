namespace Planarian.Modules.Account.Backup.Models;

public class AccountBackupFileByCaveDto
{
    public string CavePlanarianId { get; set; } = null!;
    public string Id { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string? BlobKey { get; set; }
    public string FileTypeDisplayName { get; set; } = null!;
}
