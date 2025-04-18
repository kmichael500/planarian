using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Model.Database.Entities.RidgeWalker.ViewModels;

public class AddCave
{
    public AddCave(string stateId, string countyId, string name,
        double lengthFeet,
        double depthFeet, int numberOfPits, List<string> geologyTagIds)
    {
        StateId = stateId;
        CountyId = countyId;
        Name = name;
        LengthFeet = lengthFeet;
        DepthFeet = depthFeet;
        NumberOfPits = numberOfPits;
        GeologyTagIds = geologyTagIds;
    }


    public AddCave(double maxPitDepthFeet, string narrative, DateTime? reportedOn,
        string reportedByName, string stateId, string countyId, string name,
        double lengthFeet,
        double depthFeet, int numberOfPits, List<string> geologyTagIds) : this(stateId, countyId, name,
        lengthFeet, depthFeet, numberOfPits, geologyTagIds)
    {
        MaxPitDepthFeet = maxPitDepthFeet;
        Narrative = narrative;
        ReportedOn = reportedOn;
        ReportedByName = reportedByName;
    }

    public AddCave()
    {
    }

    [MaxLength(PropertyLength.Id)] public string? Id { get; set; } = null!;
    [MaxLength(PropertyLength.Name)] public string Name { get; set; } = null!;
    public List<string> AlternateNames { get; set; } = [];

    [MaxLength(PropertyLength.Id)] public string CountyId { get; set; }
    [MaxLength(PropertyLength.Id)] public string StateId { get; set; }


    public double LengthFeet { get; set; }
    public double DepthFeet { get; set; }
    public double MaxPitDepthFeet { get; set; }
    public int NumberOfPits { get; set; } = 0;

    public string? Narrative { get; set; }

    public DateTime? ReportedOn { get; set; }
    [MaxLength(PropertyLength.Name)] public string? ReportedByName { get; set; }

    public List<AddEntrance> Entrances { get; set; } = [];
    public List<EditFileMetadata>? Files { get; set; } = new List<EditFileMetadata>();
    public List<string> GeologyTagIds { get; set; } = [];
    public List<string> ReportedByNameTagIds { get; set; } = [];
    public List<string> BiologyTagIds { get; set; } = [];
    public List<string> ArcheologyTagIds { get; set; } = [];
    public List<string> CartographerNameTagIds { get; set; } = [];
    public List<string> MapStatusTagIds { get; set; } = [];
    public List<string> GeologicAgeTagIds { get; set; } = [];
    public List<string> PhysiographicProvinceTagIds { get; set; } = [];
    public List<string> OtherTagIds { get; set; } = [];
}