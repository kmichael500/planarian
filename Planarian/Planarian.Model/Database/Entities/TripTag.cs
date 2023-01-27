using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class TripTag : EntityBase
{
    public string TagId { get; set; }
    public Tag Tag { get; set; }
    public Trip Trip { get; set; }
    public string TripId { get; set; }
}

public class TripTagConfiguration : IEntityTypeConfiguration<TripTag>
{
    public void Configure(EntityTypeBuilder<TripTag> builder)
    {
        builder.HasKey(e => new { e.TagId, e.TripId });
        builder
            .HasOne(e => e.Tag)
            .WithMany(e => e.TripTags)
            .HasForeignKey(bc => bc.TagId);


        builder.HasOne(e => e.Trip)
            .WithMany(e => e.TripTags)
            .HasForeignKey(e => e.TripId);
    }
}