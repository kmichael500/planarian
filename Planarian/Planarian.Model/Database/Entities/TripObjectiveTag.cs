using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.TripObjectives;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class TripObjectiveTag : EntityBase
{
    public string TagId { get; set; }
    public Tag Tag { get; set; }
    public TripObjective TripObjective { get; set; }
    public string TripObjectiveId { get; set; }
}

public class TripObjectiveTagConfiguration : IEntityTypeConfiguration<TripObjectiveTag>
{
    public void Configure(EntityTypeBuilder<TripObjectiveTag> builder)
    {
        builder.HasKey(e => new { e.TagId, e.TripObjectiveId });
        builder
            .HasOne(e => e.Tag)
            .WithMany(e => e.TripObjectiveTags)
            .HasForeignKey(bc => bc.TagId);


        builder.HasOne(e => e.TripObjective)
            .WithMany(e => e.TripObjectiveTags)
            .HasForeignKey(e => e.TripObjectiveId);
    }
}