using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class EntranceHydrologyTag : EntityBase, IEntranceTag
{
    [MaxLength(PropertyLength.Id)] public string TagTypeId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string EntranceId { get; set; } = null!;

    public TagType TagType { get; set; } = null!;
    public Entrance Entrance { get; set; } = null!;
}

public class EntranceHydrologyTagConfiguration : BaseEntityTypeConfiguration<EntranceHydrologyTag>
{
    public override void Configure(EntityTypeBuilder<EntranceHydrologyTag> builder)
    {
        base.Configure(builder);

        builder.HasKey(e => new { e.TagTypeId, e.EntranceId });

        builder
            .HasOne(e => e.TagType)
            .WithMany(e => e.EntranceHydrologyTags)
            .HasForeignKey(bc => bc.TagTypeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Entrance)
            .WithMany(e => e.EntranceHydrologyTags)
            .HasForeignKey(e => e.EntranceId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}