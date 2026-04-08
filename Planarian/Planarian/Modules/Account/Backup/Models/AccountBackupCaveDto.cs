namespace Planarian.Modules.Account.Backup.Models;

public class AccountBackupCaveDto
{
    public string PlanarianId { get; set; } = null!;
    public string CaveName { get; set; } = null!;
    public string AlternateNames { get; set; } = null!;
    public string State { get; set; } = null!;
    public string CountyName { get; set; } = null!;
    public string CountyCode { get; set; } = null!;
    public string CountyIdDelimiter { get; set; } = null!;
    public int CountyCaveNumber { get; set; }
    public double? CaveLengthFt { get; set; }
    public double? CaveDepthFt { get; set; }
    public double? MaxPitDepthFt { get; set; }
    public int? NumberOfPits { get; set; }
    public string? Narrative { get; set; }
    public string Geology { get; set; } = null!;
    public string GeologicAges { get; set; } = null!;
    public string PhysiographicProvinces { get; set; } = null!;
    public string Archeology { get; set; } = null!;
    public string Biology { get; set; } = null!;
    public string? ReportedOnDate { get; set; }
    public string ReportedByNames { get; set; } = null!;
    public bool IsArchived { get; set; }
    public string OtherTags { get; set; } = null!;
    public string MapStatuses { get; set; } = null!;
    public string CartographerNames { get; set; } = null!;
}
