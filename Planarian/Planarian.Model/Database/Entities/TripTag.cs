using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class TripTag : EntityBase
{
    public string TagTypeId { get; set; }
    public TagType TagType { get; set; }
    public Trip Trip { get; set; }
    public string TripId { get; set; }
}

public class TripTagConfiguration : BaseEntityTypeConfiguration<TripTag>
{
    public override void Configure(EntityTypeBuilder<TripTag> builder)
    {
        builder.HasKey(e => new { e.TagTypeId, e.TripId });
        builder
            .HasOne(e => e.TagType)
            .WithMany(e => e.TripTags)
            .HasForeignKey(bc => bc.TagTypeId);


        builder.HasOne(e => e.Trip)
            .WithMany(e => e.TripTags)
            .HasForeignKey(e => e.TripId);
    }
}