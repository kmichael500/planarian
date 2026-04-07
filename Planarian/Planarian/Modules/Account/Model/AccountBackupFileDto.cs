namespace Planarian.Modules.Account.Model;

public class AccountBackupFileDto
{
    public string Id { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string? BlobKey { get; set; }
    public string? FileTypeDisplayName { get; set; }
}