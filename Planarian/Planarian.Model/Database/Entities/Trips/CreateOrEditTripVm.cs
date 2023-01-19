using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Model.Database.Entities.Trips;

public class CreateOrEditTripVm : ITrip
{
    [MaxLength(PropertyLength.Id)] public string? Id { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string ProjectId { get; set; } = null!;

    [Required] public string Name { get; set; } = null!;
    [Required] public DateTime TripDate { get; set; }
}