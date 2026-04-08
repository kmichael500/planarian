namespace Planarian.Modules.Account.Backup.Models;

public sealed class AccountBackupEntranceByCaveDto : AccountBackupEntranceDto
{
    public string CavePlanarianId { get; set; } = null!;
}