using Planarian.Model.Shared;

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
    public SelectListItem<string> County { get; set; }
    public string CountyDisplayId { get; set; } = null!;
    public int CountyNumber { get; set; }
    public string DisplayId { get; set; }
    
    public double? PrimaryEntranceLatitude { get; set; }
    public double? PrimaryEntranceLongitude { get; set; }
    public double? PrimaryEntranceElevationFeet { get; set; }

    public double? DistanceMiles { get; set; }

    public bool IsFavorite { get; set; }

    public IEnumerable<SelectListItem<string>> ArchaeologyTags { get; set; }
    public IEnumerable<SelectListItem<string>> BiologyTags { get; set; }
    public IEnumerable<SelectListItem<string>> CartographerNameTags { get; set; }
    public IEnumerable<SelectListItem<string>> GeologicAgeTags { get; set; }
    public IEnumerable<SelectListItem<string>> GeologyTags { get; set; }
    public IEnumerable<SelectListItem<string>> MapStatusTags { get; set; }
    public IEnumerable<SelectListItem<string>> OtherTags { get; set; }
    public IEnumerable<SelectListItem<string>> PhysiographicProvinceTags { get; set; }
    public IEnumerable<SelectListItem<string>> ReportedByTags { get; set; }
}
