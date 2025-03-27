namespace Planarian.Modules.Caves.Models;

public class EntranceExportDto
{
    public string? Name { get; set; } = string.Empty;
    public string? Description { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateTime? ReportedOn { get; set; }
    public double? PitDepthFeet { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Elevation { get; set; }
    public string LocationQuality { get; set; } = "Unknown";
    public List<string> EntranceStatusTags { get; set; } = new();
    public List<string> FieldIndicationTags { get; set; } = new();
    public List<string> EntranceHydrologyTags { get; set; } = new();
    public List<string> EntranceReportedByTags { get; set; } = new();
    public List<string> EntranceOtherTags { get; set; } = new();
}