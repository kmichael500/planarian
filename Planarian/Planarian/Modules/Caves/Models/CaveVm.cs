using System.ComponentModel.DataAnnotations;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Models;

public class CaveVm
{
    public CaveVm(string id, string stateId, string countyId, string displayId, string name,
        double lengthFeet,
        double depthFeet, double maxPitDepthFeet, int numberOfPits, bool isArchived,
        EntranceVm primaryEntrance,
        IEnumerable<string> mapIds,
        IEnumerable<EntranceVm> entrances, IEnumerable<string> geologyTagIds)
    {
        Id = id;
        StateId = stateId;
        CountyId = countyId;
        DisplayId = displayId;
        Name = name;
        LengthFeet = lengthFeet;
        DepthFeet = depthFeet;
        NumberOfPits = numberOfPits;
        IsArchived = isArchived;
        PrimaryEntrance = primaryEntrance;
        MapIds = mapIds;
        Entrances = entrances;
        GeologyTagIds = geologyTagIds;
    }


    public CaveVm(string id, string reportedByUserId, string narrative, DateTime? reportedOn,
        string reportedByName, string stateId, string countyId, string displayId, string name,
        double lengthFeet,
        double depthFeet, double maxPitDepthFeet, int numberOfPits, bool isArchived,
        EntranceVm primaryEntrance,
        IEnumerable<string> mapIds,
        IEnumerable<EntranceVm> entrances, IEnumerable<string> geologyTagIds) : this(id, stateId,
        countyId, displayId, name, lengthFeet, depthFeet, maxPitDepthFeet, numberOfPits, isArchived, primaryEntrance,
        mapIds, entrances, geologyTagIds)
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
    [MaxLength(PropertyLength.Id)] public string StateId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CountyId { get; set; } = null!;
    
    public string DisplayId { get; set; } = null!;
    
    [MaxLength(PropertyLength.Name)] public string Name { get; set; } = null!;
    
    public double LengthFeet { get; set; }
    public double DepthFeet { get; set; }
    public double MaxPitDepthFeet { get; set; }
    public int NumberOfPits { get; set; } = 0;

    public string? Narrative { get; set; }

    public DateTime? ReportedOn { get; set; }
    [MaxLength(PropertyLength.Name)] public string? ReportedByName { get; set; }
    public bool IsArchived { get; set; } = false;

    public EntranceVm PrimaryEntrance { get; set; } = null!;

    public IEnumerable<string> MapIds { get; set; } = new HashSet<string>();
    public IEnumerable<EntranceVm> Entrances { get; set; } = new HashSet<EntranceVm>();
    public IEnumerable<string> GeologyTagIds { get; set; } = new HashSet<string>();
}