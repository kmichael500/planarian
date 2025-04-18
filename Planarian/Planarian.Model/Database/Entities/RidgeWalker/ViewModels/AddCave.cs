using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Model.Database.Entities.RidgeWalker.ViewModels;

public class AddCave
{
    public AddCave(string stateId, string countyId, string name,
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


    public AddCave(double maxPitDepthFeet, string narrative, DateTime? reportedOn,
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

    public AddCave()
    {
    }

    [MaxLength(PropertyLength.Id)] public string? Id { get; set; } = null!;
    [MaxLength(PropertyLength.Name)] public string Name { get; set; } = null!;
    public IEnumerable<string> AlternateNames { get; set; } = new HashSet<string>();

    [MaxLength(PropertyLength.Id)] public string CountyId { get; set; }
    [MaxLength(PropertyLength.Id)] public string StateId { get; set; }


    public double LengthFeet { get; set; }
    public double DepthFeet { get; set; }
    public double MaxPitDepthFeet { get; set; }
    public int NumberOfPits { get; set; } = 0;

    public string? Narrative { get; set; }

    public DateTime? ReportedOn { get; set; }
    [MaxLength(PropertyLength.Name)] public string? ReportedByName { get; set; }

    public IEnumerable<AddEntrance> Entrances { get; set; } = new HashSet<AddEntrance>();
    public IEnumerable<EditFileMetadata>? Files { get; set; } = new HashSet<EditFileMetadata>();
    public IEnumerable<string> GeologyTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> ReportedByNameTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> BiologyTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> ArcheologyTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> CartographerNameTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> MapStatusTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> GeologicAgeTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> PhysiographicProvinceTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> OtherTagIds { get; set; } = new HashSet<string>();
}