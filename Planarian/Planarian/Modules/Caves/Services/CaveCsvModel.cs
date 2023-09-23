namespace Planarian.Modules.Caves.Services;

public class CaveCsvModel   
{
    public string CaveName { get; set; }
    public double CaveLengthFt { get; set; }
    public double CaveDepthFt { get; set; }
    public double MaxPitDepthFt { get; set; }
    public int NumberOfPits { get; set; }
    public string? Narrative { get; set; }
    public string CountyCode { get; set; }
    public string CountyName { get; set; }
    public int CountyCaveNumber { get; set; }
    public string State { get; set; }
    public string? Geology { get; set; }
    public string? ReportedOnDate { get; set; }
    public string? ReportedByName { get; set; }
    public bool? IsArchived { get; set; }
}