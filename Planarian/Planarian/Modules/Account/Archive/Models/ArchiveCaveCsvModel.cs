using Planarian.Modules.Import.Models;

namespace Planarian.Modules.Account.Archive.Models;

public sealed class ArchiveCaveCsvModel : CaveCsvModel
{
    public string PlanarianId { get; set; } = null!;
    public string? CountyIdDelimiter { get; set; }
}
