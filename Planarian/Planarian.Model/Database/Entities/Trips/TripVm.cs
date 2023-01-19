using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Model.Database.Entities.Trips;

public class TripVm : ITrip
{
    public TripVm(string name, string id, string projectId, DateTime tripDate, int tripNumber)
    {
        Id = id;
        Name = name;
        ProjectId = projectId;
        TripDate = tripDate;
        TripNumber = tripNumber;
    }

    public TripVm(Trip trip, int tripNumber)
    {
        Id = trip.Id;
        ProjectId = trip.ProjectId;
        TripDate = trip.TripDate;
        Name = trip.Name;
        TripNumber = tripNumber;
    }

    public TripVm()
    {
    }

    [Required] public int TripNumber { get; set; }

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string Id { get; set; } = null!;

    [Required] public string ProjectId { get; set; } = null!;
    [Required] public DateTime TripDate { get; set; }
    [Required] public string Name { get; set; } = null!;
}