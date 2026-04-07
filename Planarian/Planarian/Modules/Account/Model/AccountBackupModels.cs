using Planarian.Modules.Import.Models;

namespace Planarian.Modules.Account.Model;

#region Backup Models

public sealed class AccountBackupCaveDto : CaveCsvModel
{
    public string PlanarianId { get; set; } = null!;
    public string? CountyIdDelimiter { get; set; }
}

public class AccountBackupEntranceDto : EntranceCsvModel
{
}

public sealed class AccountBackupEntranceByCaveDto : AccountBackupEntranceDto
{
    public string CavePlanarianId { get; set; } = null!;
}

public class AccountBackupFileDto
{
    public string Id { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string? BlobKey { get; set; }
    public string? BlobContainer { get; set; }
    public string? FileTypeDisplayName { get; set; }
}

public sealed class AccountBackupFileByCaveDto : AccountBackupFileDto
{
    public string CavePlanarianId { get; set; } = null!;
}

public class AccountBackupGeoJsonDto
{
    public string Name { get; set; } = null!;
    public string GeoJson { get; set; } = string.Empty;
}

public sealed class AccountBackupGeoJsonByCaveDto : AccountBackupGeoJsonDto
{
    public string CavePlanarianId { get; set; } = null!;
}

#endregion
