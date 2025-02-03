namespace Tcs;

public class CaveRecord
{
    public string TcsNumber { get; set; } = null!;
    public string? CaveName { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int LengthFt { get; set; }
    public int DepthFt { get; set; }
    public int PitDepthFt { get; set; }
    public int NumberOfPits { get; set; }
    public string? CountyName { get; set; }
    public string? TopographicName { get; set; }
    public int ElevationFt { get; set; }
    public string? Ownership { get; set; }
    public string? RequiredGear { get; set; }
    public string? EntranceType { get; set; }
    public string? FieldIndication { get; set; }
    public string? MapStatus { get; set; }
    public string? CaveGeology { get; set; }
    public string? GeologicalAge { get; set; }
    public string? PhysiographicProvince { get; set; }
    public string? Narrative { get; set; }
}