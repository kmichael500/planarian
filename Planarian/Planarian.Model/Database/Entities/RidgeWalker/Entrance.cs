using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class Entrance : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string? ReportedByUserId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public virtual string CaveId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string LocationQualityTagId { get; set; } = null!;

    
    [MaxLength(PropertyLength.Name)] public string? Name { get; set; }

    public bool IsPrimary { get; set; } = false;
    public string? Description { get; set; }
    public Point Location { get; set; } = null!;

    public DateTime? ReportedOn { get; set; }
    [MaxLength(PropertyLength.Name)]public string? ReportedByName { get; set; }

    public double? PitFeet { get; set; } // TODO: Rename to PitDepthFeet

    public User? ReportedByUser { get; set; }
    public virtual Cave Cave { get; set; } = null!;
    public virtual TagType LocationQualityTag { get; set; } = null!;
    public ICollection<EntranceStatusTag> EntranceStatusTags { get; set; } = new HashSet<EntranceStatusTag>();
    public ICollection<EntranceHydrologyTag> EntranceHydrologyTags { get; set; } = new HashSet<EntranceHydrologyTag>();
    public ICollection<EntranceHydrologyFrequencyTag> EntranceHydrologyFrequencyTags { get; set; } = new HashSet<EntranceHydrologyFrequencyTag>();
    public ICollection<FieldIndicationTag> FieldIndicationTags { get; set; } = new HashSet<FieldIndicationTag>();
}

public class EntranceConfiguration : BaseEntityTypeConfiguration<Entrance>
{
    public override void Configure(EntityTypeBuilder<Entrance> builder)
    {
        builder.HasOne(e => e.Cave)
            .WithMany(e => e.Entrances)
            .HasForeignKey(e => e.CaveId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ReportedByUser)
            .WithMany(e => e.EntrancesReported)
            .HasForeignKey(e => e.ReportedByUserId)
            .OnDelete(DeleteBehavior.SetNull);


        builder.HasOne(e => e.LocationQualityTag)
            .WithMany(e => e.EntranceLocationQualitiesTags)
            .HasForeignKey(e => e.LocationQualityTagId)
            .OnDelete(DeleteBehavior.NoAction);

    }
}