using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Model.Database.Entities.Trips;

public class TripVm : ITrip
{
    public TripVm(string id, string projectId, IEnumerable<string> tripTagTypeIds,
        IEnumerable<string> tripMemberIds, string name,
        string? description,
        string? tripReport)
    {
        Id = id;
        ProjectId = projectId;
        TripTagTypeIds = tripTagTypeIds;
        TripMemberIds = tripMemberIds;
        TripReport = tripReport;
    }

    public TripVm(Trip trip, IEnumerable<string> tripTagTypeIds,
        IEnumerable<string> tripMemberIds)
    {
        Id = trip.Id;
        ProjectId = trip.ProjectId;
        TripTagTypeIds = tripTagTypeIds;
        TripMemberIds = tripMemberIds;
        Name = trip.Name;
        Description = trip.Description;
        TripReport = trip.TripReport;
    }


    public TripVm()
    {
    }

    [Required] public IEnumerable<string> TripTagTypeIds { get; set; } = new HashSet<string>();

    [Required] public IEnumerable<string> TripMemberIds { get; set; } = new HashSet<string>();

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string Id { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string ProjectId { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = null!;

    [MaxLength(PropertyLength.MediumText)] public string? Description { get; set; }

    public string? TripReport { get; set; }
}