using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Models;

public class EntranceVm
{
    public EntranceVm(string id, double latitude, double longitude, double elevationFeet,
        IEnumerable<string> entranceStatusTagIds, IEnumerable<string> fieldIndicationTagIds,
        IEnumerable<string> entranceHydrologyTagIds)
    {
        Id = id;
        Latitude = latitude;
        Longitude = longitude;
        ElevationFeet = elevationFeet;

        EntranceStatusTagIds = entranceStatusTagIds;
        FieldIndicationTagIds = fieldIndicationTagIds;
        EntranceHydrologyTagIds = entranceHydrologyTagIds;
    }


    public EntranceVm(string id, string name, string description, double latitude, double longitude,
        double elevationFeet,
        double pitFeet, string locationQualityTagId, string reportedByUserId, IEnumerable<string> reportedByNameTagIds,
        DateTime? reportedOn, IEnumerable<string> entranceStatusTagIds, IEnumerable<string> fieldIndicationTagIds,
        IEnumerable<string> entranceHydrologyTagIds, IEnumerable<string> entranceTypeTagIds) : this(id, latitude, longitude, elevationFeet,
        entranceStatusTagIds, fieldIndicationTagIds, entranceHydrologyTagIds)
    {
        Name = name;
        PitFeet = pitFeet;
        Description = description;
        LocationQualityTagId = locationQualityTagId;
        ReportedByUserId = reportedByUserId;
        ReportedByNameTagIds = reportedByNameTagIds;
        ReportedOn = reportedOn;
        EntranceTypeTagIds = entranceTypeTagIds;
    }

    public EntranceVm()
    {
    }

    [MaxLength(PropertyLength.Id)] public string Id { get; set; }
    [MaxLength(PropertyLength.Id)] public string? ReportedByUserId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string LocationQualityTagId { get; set; } = null!;

    public bool IsPrimary { get; set; }
    [MaxLength(PropertyLength.Name)] public string? Name { get; set; }
    public string? Description { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double ElevationFeet { get; set; }

    public DateTime? ReportedOn { get; set; }

    public double? PitFeet { get; set; }

    public IEnumerable<string> EntranceStatusTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> FieldIndicationTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> EntranceHydrologyTagIds { get; set; } = new HashSet<string>();

    public IEnumerable<string> ReportedByNameTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> EntranceTypeTagIds { get; set; } = new HashSet<string>();
}