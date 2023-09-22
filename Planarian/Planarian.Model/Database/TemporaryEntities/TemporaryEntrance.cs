using System.ComponentModel.DataAnnotations;
using Microsoft.SqlServer.Types;
using NetTopologySuite.Geometries;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.TemporaryEntities;

public class TemporaryEntrance : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string? CaveId { get; set; }
    [MaxLength(PropertyLength.SmallText)] public string CountyDisplayId { get; set; } = null!;
    public int CountyCaveNumber { get; set; }

    [MaxLength(PropertyLength.Id)] public string? ReportedByUserId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string LocationQualityTagId { get; set; } = null!;


    [MaxLength(PropertyLength.Name)] public string? Name { get; set; }

    public bool IsPrimary { get; set; } = false;
    public string? Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Elevation { get; set; }

    public DateTime? ReportedOn { get; set; }
    [MaxLength(PropertyLength.Name)] public string? ReportedByName { get; set; }

    public double? PitFeet { get; set; }

    // public User? ReportedByUser { get; set; }
    // public virtual Cave Cave { get; set; } = null!;
    // public virtual TagType LocationQualityTag { get; set; } = null!;
    // public ICollection<EntranceStatusTag> EntranceStatusTags { get; set; } = new HashSet<EntranceStatusTag>();
    // public ICollection<EntranceHydrologyTag> EntranceHydrologyTags { get; set; } = new HashSet<EntranceHydrologyTag>();
    //
    // public ICollection<EntranceHydrologyFrequencyTag> EntranceHydrologyFrequencyTags { get; set; } =
    //     new HashSet<EntranceHydrologyFrequencyTag>();

    // public ICollection<FieldIndicationTag> FieldIndicationTags { get; set; } = new HashSet<FieldIndicationTag>();
}