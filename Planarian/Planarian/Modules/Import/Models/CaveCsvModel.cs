namespace Planarian.Modules.Import.Models;

public class CaveCsvModel
{
    public string CaveName { get; set; }
    public string? AlternateNames { get; set; }

    public string State { get; set; }
    public string CountyCode { get; set; }
    public int CountyCaveNumber { get; set; }
    public string CountyName { get; set; }
    public string? MapStatuses { get; set; }
    public string? CartographerNames { get; set; }

    public double? CaveLengthFt { get; set; }
    public double? CaveDepthFt { get; set; }
    public double? MaxPitDepthFt { get; set; }
    public int? NumberOfPits { get; set; }
    public string? Narrative { get; set; }
    public string? Geology { get; set; }
    public string? GeologicAges { get; set; }
    public string? PhysiographicProvinces { get; set; }
    public string? Archeology { get; set; }
    public string? Biology { get; set; }
    public string? ReportedOnDate { get; set; }
    public string? ReportedByNames { get; set; }
    public bool? IsArchived { get; set; }
    public string? OtherTags { get; set; }
}