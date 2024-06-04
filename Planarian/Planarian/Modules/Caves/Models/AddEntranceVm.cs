using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Models;

public class AddEntranceVm
{
    public AddEntranceVm(double latitude, double longitude, double elevationFeet,
        IEnumerable<string> entranceStatusTagIds, IEnumerable<string> fieldIndicationTagIds,
        IEnumerable<string> entranceHydrologyTagIds)
    {
        Latitude = latitude;
        Longitude = longitude;
        ElevationFeet = elevationFeet;

        EntranceStatusTagIds = entranceStatusTagIds;
        FieldIndicationTagIds = fieldIndicationTagIds;
        EntranceHydrologyTagIds = entranceHydrologyTagIds;
    }


    public AddEntranceVm(string name, string description, double latitude, double longitude,
        double elevationFeet,
        double? pitFeet, string locationQualityTagId, string reportedByName,
        DateTime? reportedOn, IEnumerable<string> entranceStatusTagIds, IEnumerable<string> fieldIndicationTagIds,
        IEnumerable<string> entranceHydrologyTagIds) : this(latitude, longitude, elevationFeet, entranceStatusTagIds, fieldIndicationTagIds, entranceHydrologyTagIds)
    {
        Name = name;
        PitFeet = pitFeet;
        Description = description;
        LocationQualityTagId = locationQualityTagId;
        ReportedByName = reportedByName;
        ReportedOn = reportedOn;
    }

    public AddEntranceVm()
    {
    }

    [MaxLength(PropertyLength.Id)] public string? Id { get; set; }
    public bool IsPrimary { get; set; }
    [MaxLength(PropertyLength.Id)] public string LocationQualityTagId { get; set; } = null!;

    [MaxLength(PropertyLength.Name)] public string? Name { get; set; }
    public string? Description { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double ElevationFeet { get; set; }

    public DateTime? ReportedOn { get; set; }
    [MaxLength(PropertyLength.Name)] public string? ReportedByName { get; set; }

    public double? PitFeet { get; set; }

    public IEnumerable<string> EntranceStatusTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> FieldIndicationTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> EntranceHydrologyTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> ReportedByNameTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> EntranceOtherTagIds { get; set; } = new HashSet<string>();
}

public class EditEntranceVm : AddEntranceVm
{
    public string Id { get; set; } = null!;
}