using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Model.Database.Entities.TripObjectives;

public class CreateOrEditTripObjectiveVm : ITripObjective
{
    [Required]
    [MaxLength(PropertyLength.Id)]
    public string ProjectId { get; set; } = null!;

    [Required] public IEnumerable<string> TripObjectiveTypeIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> TripObjectiveMemberIds { get; set; } = new HashSet<string>();
    [MaxLength(PropertyLength.Id)] public string? Id { get; set; }
    
    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.MediumText)]
    public string Description { get; set; } = null!;

    public string? TripReport { get; set; }
}