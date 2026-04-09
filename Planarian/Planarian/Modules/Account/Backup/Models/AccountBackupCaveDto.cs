using Planarian.Modules.Import.Models;

namespace Planarian.Modules.Account.Backup.Models;

public sealed class AccountBackupCaveDto : CaveCsvModel
{
    public string PlanarianId { get; set; } = null!;
    public string? CountyIdDelimiter { get; set; }
}