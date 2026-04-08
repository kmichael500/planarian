namespace Planarian.Modules.Account.Backup.Models;

public class AccountBackupEntranceByCaveDto
{
    public string CavePlanarianId { get; set; } = null!;
    public string CountyCode { get; set; } = null!;
    public string CountyCaveNumber { get; set; } = null!;
    public string? EntranceName { get; set; }
    public double DecimalLatitude { get; set; }
    public double DecimalLongitude { get; set; }
    public double? EntranceElevationFt { get; set; }
    public string? LocationQuality { get; set; }
    public string? EntranceDescription { get; set; }
    public double? EntrancePitDepth { get; set; }
    public string EntranceStatuses { get; set; } = null!;
    public string EntranceHydrology { get; set; } = null!;
    public string FieldIndication { get; set; } = null!;
    public string? ReportedOnDate { get; set; }
    public string ReportedByNames { get; set; } = null!;
    public bool IsPrimaryEntrance { get; set; }
}
