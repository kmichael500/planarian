using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Model.Database.Entities.TripObjectives;

public class TripObjectiveVm : ITripObjective
{
    public TripObjectiveVm(string id, string tripId, IEnumerable<string> tripObjectiveTypeIds,
        IEnumerable<string> tripObjectiveMemberIds, string name,
        string description,
        string? tripReport)
    {
        Id = id;
        TripId = tripId;
        TripObjectiveTypeIds = tripObjectiveTypeIds;
        TripObjectiveMemberIds = tripObjectiveMemberIds;
        TripReport = tripReport;
    }

    public TripObjectiveVm(TripObjective tripObjective, IEnumerable<string> tripObjectiveTypeIds,
        IEnumerable<string> tripObjectiveMemberIds)
    {
        Id = tripObjective.Id;
        TripId = tripObjective.TripId;
        TripObjectiveTypeIds = tripObjectiveTypeIds;
        TripObjectiveMemberIds = tripObjectiveMemberIds;
        Name = tripObjective.Name;
        Description = tripObjective.Description;
        TripReport = tripObjective.TripReport;
    }


    public TripObjectiveVm()
    {
    }

    [Required] public IEnumerable<string> TripObjectiveTypeIds { get; set; } = new HashSet<string>();

    [Required] public IEnumerable<string> TripObjectiveMemberIds { get; set; } = new HashSet<string>();

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string Id { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string TripId { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.MediumText)]
    public string Description { get; set; } = null!;

    public string? TripReport { get; set; }
}