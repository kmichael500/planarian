using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class Entrance : EntityBase
{
    public string CaveId { get; set; } = null!;
    public string LocationQualityTagId { get; set; } = null!;
    public string ReportedByUserId { get; set; } = null!;
    
    public string Name { get; set; }
    public string Description { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double ElevationFeet { get; set; }
    
    public DateTime? ReportedOn { get; set; }
    public User? ReportedByUser { get; set; }
    public string ReportedByName { get; set; }

    public double? PitFeet { get; set; }

    public virtual Cave Cave { get; set; } = null!;
    public virtual TagType LocationQualityTag { get; set; } = null!;
    public ICollection<EntranceStatusTag> EntranceStatusTags { get; set; } = new HashSet<EntranceStatusTag>();
    public ICollection<EntranceHydrologyFrequencyTag> EntranceHydrologyFrequencyTags { get; set; } = new HashSet<EntranceHydrologyFrequencyTag>();
    public ICollection<FieldIndicationTag> FieldIndicationTags { get; set; } = new HashSet<FieldIndicationTag>();
}