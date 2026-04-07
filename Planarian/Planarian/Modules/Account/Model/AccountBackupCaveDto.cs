using Planarian.Modules.Import.Models;

namespace Planarian.Modules.Account.Model;

public sealed class AccountBackupCaveDto : CaveCsvModel
{
    public string PlanarianId { get; set; } = null!;
    public string? CountyIdDelimiter { get; set; }
}