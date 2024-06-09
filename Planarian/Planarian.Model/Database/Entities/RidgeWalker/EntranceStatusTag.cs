using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class EntranceStatusTag : EntityBase, IEntranceTag
{
    [MaxLength(PropertyLength.Id)] public string TagTypeId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string EntranceId { get; set; } = null!;

    public TagType? TagType { get; set; }
    public Entrance? Entrance { get; set; }
}

public class EntranceStatusTagConfiguration : BaseEntityTypeConfiguration<EntranceStatusTag>
{
    public override void Configure(EntityTypeBuilder<EntranceStatusTag> builder)
    {
        base.Configure(builder);

        builder.HasKey(e => new { e.TagTypeId, e.EntranceId });

        builder
            .HasOne(e => e.TagType)
            .WithMany(e => e.EntranceStatusTags)
            .HasForeignKey(bc => bc.TagTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Entrance)
            .WithMany(e => e.EntranceStatusTags)
            .HasForeignKey(e => e.EntranceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}