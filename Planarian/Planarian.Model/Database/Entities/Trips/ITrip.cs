using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Model.Database.Entities.Trips;

public interface ITrip
{
    [Required]
    [MaxLength(PropertyLength.Id)]
    public string Id { get; set; }

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string ProjectId { get; set; }

    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; }

    [Required]
    [MaxLength(PropertyLength.MediumText)]
    public string Description { get; set; }

    public string? TripReport { get; set; }
}