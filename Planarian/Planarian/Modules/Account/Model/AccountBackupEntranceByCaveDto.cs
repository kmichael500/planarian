namespace Planarian.Modules.Account.Model;

public sealed class AccountBackupEntranceByCaveDto : AccountBackupEntranceDto
{
    public string CavePlanarianId { get; set; } = null!;
}