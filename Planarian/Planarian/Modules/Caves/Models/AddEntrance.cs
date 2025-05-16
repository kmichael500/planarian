using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Planarian.Model.Shared;

namespace Planarian.Model.Database.Entities.RidgeWalker.ViewModels;

public class AddEntrance
{
    public AddEntrance(double latitude, double longitude, double elevationFeet,
        List<string> entranceStatusTagIds, List<string> fieldIndicationTagIds,
        List<string> entranceHydrologyTagIds)
    {
        Latitude = latitude;
        Longitude = longitude;
        ElevationFeet = elevationFeet;

        EntranceStatusTagIds = entranceStatusTagIds;
        FieldIndicationTagIds = fieldIndicationTagIds;
        EntranceHydrologyTagIds = entranceHydrologyTagIds;
    }

    public AddEntrance()
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

    public double? PitFeet { get; set; }

    public List<string> EntranceStatusTagIds { get; set; } = [];
    public List<string> FieldIndicationTagIds { get; set; } = [];
    public List<string> EntranceHydrologyTagIds { get; set; } = [];
    public List<string> ReportedByNameTagIds { get; set; } = [];
    public List<string> EntranceOtherTagIds { get; set; } = [];
}