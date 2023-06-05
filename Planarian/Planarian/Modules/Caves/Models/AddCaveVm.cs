using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Models;

public class AddCaveVm
{
    public AddCaveVm(string id, string countyId, string name,
        double lengthFeet,
        double depthFeet, int numberOfPits, IEnumerable<string> geologyTagIds)
    {
        Id = id;
        CountyId = countyId;
        Name = name;
        LengthFeet = lengthFeet;
        DepthFeet = depthFeet;
        NumberOfPits = numberOfPits;
        GeologyTagIds = geologyTagIds;
    }

    public AddCaveVm(string id, double maxPitDepthFeet, string narrative, DateTime? reportedOn,
        string reportedByName, string countyId, string name,
        double lengthFeet,
        double depthFeet, int numberOfPits, IEnumerable<string> geologyTagIds) : this(id, countyId, name, lengthFeet, depthFeet, numberOfPits, geologyTagIds)
    {
        MaxPitDepthFeet = maxPitDepthFeet;
        Narrative = narrative;
        ReportedOn = reportedOn;
        ReportedByName = reportedByName;
    }

    public AddCaveVm(){}
    
    [MaxLength(PropertyLength.Name)] public string Name { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string Id { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CountyId { get; set; } = null!;
    
    
    public double LengthFeet { get; set; }
    public double DepthFeet { get; set; }
    public double? MaxPitDepthFeet { get; set; }
    public int NumberOfPits { get; set; } = 0;

    public string? Narrative { get; set; }

    public DateTime? ReportedOn { get; set; }
    [MaxLength(PropertyLength.Name)] public string? ReportedByName { get; set; }

    public IEnumerable<AddEntranceVm> Entrances { get; set; } = new HashSet<AddEntranceVm>();
    public IEnumerable<string> GeologyTagIds { get; set; } = new HashSet<string>();
}