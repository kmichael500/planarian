namespace Planarian.Modules.Account.Backup.Models;

public sealed class AccountBackupFileByCaveDto : AccountBackupFileDto
{
    public string CavePlanarianId { get; set; } = null!;
}