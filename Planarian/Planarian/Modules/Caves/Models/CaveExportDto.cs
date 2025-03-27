namespace Planarian.Modules.Caves.Models;

public class CaveExportDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public IEnumerable<string> AlternateNames { get; set; } = new List<string>();
    public string CountyName { get; set; } = null!;
    public string CountyDisplayId { get; set; } = null!;
    public string? CountyIdDelimiter { get; set; } = null!;
    public string StateName { get; set; } = null!;
    public int CountyNumber { get; set; }
    public double? LengthFeet { get; set; }
    public double? DepthFeet { get; set; }
    public double? MaxPitDepthFeet { get; set; }
    public int? NumberOfPits { get; set; }
    public string? Narrative { get; set; }
    public DateTime? ReportedOn { get; set; }
    public bool IsArchived { get; set; }
        
    // Tag groups
    public List<string> GeologyTags { get; set; } = [];
    public List<string> MapStatusTags { get; set; } = [];
    public List<string> GeologicAgeTags { get; set; } = [];
    public List<string> PhysiographicProvinceTags { get; set; } = [];
    public List<string> BiologyTags { get; set; } = [];
    public List<string> ArcheologyTags { get; set; } = [];
    public List<string> CartographerNameTags { get; set; } = [];
    public List<string> CaveReportedByTags { get; set; } = [];
    public List<string> CaveOtherTags { get; set; } = [];
        
    // Entrances for this cave.
    public List<EntranceExportDto> Entrances { get; set; } = [];
}