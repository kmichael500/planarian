using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Models;

public class CaveVm
{
    public CaveVm(string id, string primaryEntranceId, string countyId, string displayId, string name,
        double lengthFeet,
        double depthFeet, int numberOfPits, bool isArchived, EntranceVm primaryEntrance, IEnumerable<string> mapIds,
        IEnumerable<string> entranceIds, IEnumerable<string> geologyTagIds)
    {
        Id = id;
        PrimaryEntranceId = primaryEntranceId;
        CountyId = countyId;
        DisplayId = displayId;
        Name = name;
        LengthFeet = lengthFeet;
        DepthFeet = depthFeet;
        NumberOfPits = numberOfPits;
        IsArchived = isArchived;
        PrimaryEntrance = primaryEntrance;
        MapIds = mapIds;
        EntranceIds = entranceIds;
        GeologyTagIds = geologyTagIds;
    }

    public CaveVm(string id, string reportedByUserId, double maxPitDepthFeet, string narrative, DateTime? reportedOn,
        string reportedByName, string primaryEntranceId, string countyId, string displayId, string name,
        double lengthFeet,
        double depthFeet, int numberOfPits, bool isArchived, EntranceVm primaryEntrance, IEnumerable<string> mapIds,
        IEnumerable<string> entranceIds, IEnumerable<string> geologyTagIds) : this(id, primaryEntranceId, countyId, displayId, name, lengthFeet, depthFeet, numberOfPits, isArchived, primaryEntrance, mapIds, entranceIds, geologyTagIds)
    {
        ReportedByUserId = reportedByUserId;
        MaxPitDepthFeet = maxPitDepthFeet;
        Narrative = narrative;
        ReportedOn = reportedOn;
        ReportedByName = reportedByName;
    }

    public CaveVm(){}
    
    [MaxLength(PropertyLength.Id)] public string Id { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? ReportedByUserId { get; set; }
    [MaxLength(PropertyLength.Id)] public string PrimaryEntranceId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CountyId { get; set; } = null!;
    
    public string DisplayId { get; set; } = null!;
    
    [MaxLength(PropertyLength.Name)] public string Name { get; set; } = null!;
    
    public double LengthFeet { get; set; }
    public double DepthFeet { get; set; }
    public double? MaxPitDepthFeet { get; set; }
    public int NumberOfPits { get; set; } = 0;

    public string? Narrative { get; set; }

    public DateTime? ReportedOn { get; set; }
    [MaxLength(PropertyLength.Name)] public string? ReportedByName { get; set; }
    public bool IsArchived { get; set; } = false;

    public EntranceVm PrimaryEntrance { get; set; } = null!;

    public IEnumerable<string> MapIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> EntranceIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> GeologyTagIds { get; set; } = new HashSet<string>();
}