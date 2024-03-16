using System.Collections;
using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Controllers;

namespace Planarian.Modules.Caves.Models;

public class AddCaveVm
{
    public AddCaveVm(string stateId, string countyId, string name,
        double lengthFeet,
        double depthFeet, int numberOfPits, IEnumerable<string> geologyTagIds)
    {
        StateId = stateId;
        CountyId = countyId;
        Name = name;
        LengthFeet = lengthFeet;
        DepthFeet = depthFeet;
        NumberOfPits = numberOfPits;
        GeologyTagIds = geologyTagIds;
    }


    public AddCaveVm(double maxPitDepthFeet, string narrative, DateTime? reportedOn,
        string reportedByName, string stateId, string countyId, string name,
        double lengthFeet,
        double depthFeet, int numberOfPits, IEnumerable<string> geologyTagIds) : this(stateId, countyId, name,
        lengthFeet, depthFeet, numberOfPits, geologyTagIds)
    {
        MaxPitDepthFeet = maxPitDepthFeet;
        Narrative = narrative;
        ReportedOn = reportedOn;
        ReportedByName = reportedByName;
    }

    public AddCaveVm()
    {
    }

    [MaxLength(PropertyLength.Id)] public string? Id { get; set; } = null!;
    [MaxLength(PropertyLength.Name)] public string Name { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CountyId { get; set; }
    [MaxLength(PropertyLength.Id)] public string StateId { get; set; }


    public double LengthFeet { get; set; }
    public double DepthFeet { get; set; }
    public double MaxPitDepthFeet { get; set; }
    public int NumberOfPits { get; set; } = 0;

    public string? Narrative { get; set; }

    public DateTime? ReportedOn { get; set; }
    [MaxLength(PropertyLength.Name)] public string? ReportedByName { get; set; }

    public IEnumerable<AddEntranceVm> Entrances { get; set; } = new HashSet<AddEntranceVm>();
    public IEnumerable<EditFileMetadataVm>? Files { get; set; } = new HashSet<EditFileMetadataVm>();
    public IEnumerable<string> GeologyTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<ReportedByNameVm> ReportedByNameTagIds { get; set; }
}