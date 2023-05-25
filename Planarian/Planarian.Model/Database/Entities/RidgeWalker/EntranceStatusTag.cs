using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class EntranceStatusTag : EntityBase
{
    public string TagTypeId { get; set; }
    public string EntranceId { get; set; }

    public TagType TagType { get; set; }
    public Entrance Entrance { get; set; }
}

public class EntranceStatusTagConfiguration : BaseEntityTypeConfiguration<EntranceStatusTag>
{
    public override void Configure(EntityTypeBuilder<EntranceStatusTag> builder)
    {
        base.Configure(builder);

        builder.HasKey(e => new { e.TagTypeId, TripId = e.EntranceId });
        
        builder
            .HasOne(e => e.TagType)
            .WithMany(e => e.EntranceStatusTags)
            .HasForeignKey(bc => bc.TagTypeId);

        builder.HasOne(e => e.Entrance)
            .WithMany(e => e.EntranceStatusTags)
            .HasForeignKey(e => e.EntranceId);
    }
}