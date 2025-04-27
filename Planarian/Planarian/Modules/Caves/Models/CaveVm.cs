using System.ComponentModel.DataAnnotations;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Services;

namespace Planarian.Modules.Caves.Models;

public class CaveVm
{
    public CaveVm(string id, string stateId, string countyId, string displayId, string name,
        IEnumerable<string> alternateNames,
        double? lengthFeet,
        double? depthFeet, double? maxPitDepthFeet, int? numberOfPits, bool isArchived,
        EntranceVm primaryEntrance,
        IEnumerable<string> mapIds,
        IEnumerable<EntranceVm> entrances, IEnumerable<string> geologyTagIds, IEnumerable<FileVm> files)
    {
        Id = id;
        StateId = stateId;
        CountyId = countyId;
        DisplayId = displayId;
        Name = name;
        AlternateNames = alternateNames;
        LengthFeet = lengthFeet;
        DepthFeet = depthFeet;
        NumberOfPits = numberOfPits;
        IsArchived = isArchived;
        PrimaryEntrance = primaryEntrance;
        MapIds = mapIds;
        Entrances = entrances;
        GeologyTagIds = geologyTagIds;
        Files = files;
    }


    public CaveVm(string id, string reportedByUserId, string narrative, DateTime? reportedOn,
        IEnumerable<string> reportedByNameTagIds, string stateId, string countyId, string displayId, string name,
        IEnumerable<string> alternateNames,
        double? lengthFeet,
        double? depthFeet, double? maxPitDepthFeet, int? numberOfPits, bool isArchived,
        EntranceVm primaryEntrance,
        IEnumerable<string> mapIds,
        IEnumerable<EntranceVm> entrances, IEnumerable<string> geologyTagIds, IEnumerable<FileVm> files) : this(id,
        stateId,
        countyId, displayId, name, alternateNames, lengthFeet, depthFeet, maxPitDepthFeet, numberOfPits, isArchived,
        primaryEntrance, mapIds, entrances, geologyTagIds, files)
    {
        ReportedByUserId = reportedByUserId;
        MaxPitDepthFeet = maxPitDepthFeet;
        Narrative = narrative;
        ReportedOn = reportedOn;
        ReportedByNameTagIds = reportedByNameTagIds;
    }

    public CaveVm()
    {
    }

    [MaxLength(PropertyLength.Id)] public string Id { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? ReportedByUserId { get; set; }
    [MaxLength(PropertyLength.Id)] public string StateId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CountyId { get; set; } = null!;

    public string DisplayId { get; set; } = null!;

    [MaxLength(PropertyLength.Name)] public string Name { get; set; } = null!;
    public IEnumerable<string> AlternateNames { get; set; }

    public double? LengthFeet { get; set; }
    public double? DepthFeet { get; set; }
    public double? MaxPitDepthFeet { get; set; }
    public int? NumberOfPits { get; set; } = 0;

    public string? Narrative { get; set; }
    
    public bool IsFavorite { get; set; }

    public DateTime? ReportedOn { get; set; }
    public bool IsArchived { get; set; } = false;
    public IEnumerable<FileVm> Files { get; set; } = new HashSet<FileVm>();

    public EntranceVm PrimaryEntrance { get; set; } = null!;

    public IEnumerable<string> MapIds { get; set; } = new HashSet<string>();
    public IEnumerable<EntranceVm> Entrances { get; set; } = new HashSet<EntranceVm>();
    public IEnumerable<string> GeologyTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> ReportedByNameTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> BiologyTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> ArcheologyTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> CartographerNameTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> GeologicAgeTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> PhysiographicProvinceTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> OtherTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> MapStatusTagIds { get; set; } = new HashSet<string>();
    
    public DateTime? UpdatedOn { get; set; }
}