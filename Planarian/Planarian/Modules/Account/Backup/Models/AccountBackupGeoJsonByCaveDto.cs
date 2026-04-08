namespace Planarian.Modules.Account.Backup.Models;

public sealed class AccountBackupGeoJsonByCaveDto : AccountBackupGeoJsonDto
{
    public string CavePlanarianId { get; set; } = null!;
}