namespace Planarian.Modules.Account.Model;

public sealed class AccountBackupGeoJsonByCaveDto : AccountBackupGeoJsonDto
{
    public string CavePlanarianId { get; set; } = null!;
}