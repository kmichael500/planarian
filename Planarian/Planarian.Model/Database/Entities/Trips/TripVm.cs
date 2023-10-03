using System.ComponentModel.DataAnnotations;
using Planarian.Model.Database.Entities.Leads;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Shared;

namespace Planarian.Model.Database.Entities.Trips;

public class TripVm : ITrip
{
    public TripVm(string id, string projectId, IEnumerable<string> tripTagTypeIds,
        IEnumerable<string> tripMemberIds, string name,
        string? description,
        string? tripReport, int numberOfPhotos, DateTime createdOn, DateTime? modifiedOn)
    {
        Id = id;
        ProjectId = projectId;
        TripTagTypeIds = tripTagTypeIds;
        TripMemberIds = tripMemberIds;
        TripReport = tripReport;
        IsTripReportCompleted = !string.IsNullOrWhiteSpace(TripReport);
        NumberOfPhotos = numberOfPhotos;
        ModifiedOn = modifiedOn;
    }

    public TripVm(Trip trip, IEnumerable<string> tripTagTypeIds,
        IEnumerable<string> tripMemberIds, int numberOfPhotos)
    {
        Id = trip.Id;
        ProjectId = trip.ProjectId;
        TripTagTypeIds = tripTagTypeIds;
        TripMemberIds = tripMemberIds;
        Name = trip.Name;
        Description = trip.Description;
        TripReport = trip.TripReport;
        IsTripReportCompleted = !string.IsNullOrWhiteSpace(TripReport);
        NumberOfPhotos = numberOfPhotos;
        CreatedOn = trip.CreatedOn;
        ModifiedOn = trip.ModifiedOn;
    }


    public TripVm()
    {
    }

    [Required] public IEnumerable<string> TripTagTypeIds { get; set; } = new HashSet<string>();

    [Required] public IEnumerable<string> TripMemberIds { get; set; } = new HashSet<string>();
    public bool IsTripReportCompleted { get; set; }
    public int NumberOfPhotos { get; set; }

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
    public DateTime? ModifiedOn { get; set; }
    public DateTime CreatedOn { get; set; }
    public IEnumerable<LeadVm> Leads { get; set; }
}