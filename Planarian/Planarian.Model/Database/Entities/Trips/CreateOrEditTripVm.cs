using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Model.Database.Entities.Trips;

public class CreateOrEditTripVm : ITrip
{
    [MaxLength(PropertyLength.Id)] public string? Id { get; set; }
    [Required]
    [MaxLength(PropertyLength.Id)]
    public string ProjectId { get; set; } = null!;

    [Required] public IEnumerable<string> TripTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> TripMemberIds { get; set; } = new HashSet<string>();
    
    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.MediumText)]
    public string Description { get; set; } = null!;

    public string? TripReport { get; set; }
}