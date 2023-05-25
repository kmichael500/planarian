using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class EntranceHydrologyFrequencyTag : EntityBase
{
    public string TagTypeId { get; set; }
    public string EntranceId { get; set; }

    public TagType TagType { get; set; }
    public Entrance Entrance { get; set; }
}

public class LeadTagConfiguration : BaseEntityTypeConfiguration<EntranceHydrologyFrequencyTag>
{
    public override void Configure(EntityTypeBuilder<EntranceHydrologyFrequencyTag> builder)
    {
        base.Configure(builder);

        builder.HasKey(e => new { e.TagTypeId, TripId = e.EntranceId });
        
        builder
            .HasOne(e => e.TagType)
            .WithMany(e => e.EntranceHydrologyFrequencyTags)
            .HasForeignKey(bc => bc.TagTypeId);

        builder.HasOne(e => e.Entrance)
            .WithMany(e => e.EntranceHydrologyFrequencyTags)
            .HasForeignKey(e => e.EntranceId);
    }
}