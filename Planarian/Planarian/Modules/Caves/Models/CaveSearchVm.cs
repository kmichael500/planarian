namespace Planarian.Modules.Caves.Models;

public class CaveSearchVm
{
    public string Id { get; set; }
    public string Name { get; set; }
    
    public string? NarrativeSnippet { get; set; }
    
    public DateTime? ReportedOn { get; set; }
    public bool IsArchived { get; set; }
    public double? DepthFeet { get; set; }
    public double? LengthFeet { get; set; }
    public double? MaxPitDepthFeet { get; set; }
    public int? NumberOfPits { get; set; }
    public string CountyId { get; set; }
    public string CountyDisplayId { get; set; } = null!;
    public int CountyNumber { get; set; }
    public string DisplayId { get; set; }
    
    public double? PrimaryEntranceLatitude { get; set; }
    public double? PrimaryEntranceLongitude { get; set; }
    public double? PrimaryEntranceElevationFeet { get; set; }

    public double? DistanceMiles { get; set; }

    public bool IsFavorite { get; set; }

    public IEnumerable<string> ArchaeologyTagIds { get; set; }
    public IEnumerable<string> BiologyTagIds { get; set; }
    public IEnumerable<string> CartographerNameTagIds { get; set; }
    public IEnumerable<string> GeologicAgeTagIds { get; set; }
    public IEnumerable<string> GeologyTagIds { get; set; }
    public IEnumerable<string> MapStatusTagIds { get; set; }
    public IEnumerable<string> OtherTagIds { get; set; }
    public IEnumerable<string> PhysiographicProvinceTagIds { get; set; }
    public IEnumerable<string> ReportedByTagIds { get; set; }
}
