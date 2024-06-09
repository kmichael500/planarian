using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class EntranceOtherTag : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string TagTypeId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string EntranceId { get; set; } = null!;

    public TagType? TagType { get; set; }
    public Entrance? Entrance { get; set; }
}

public class EntranceOtherTagConfiguration : BaseEntityTypeConfiguration<EntranceOtherTag>
{
    public override void Configure(EntityTypeBuilder<EntranceOtherTag> builder)
    {
        base.Configure(builder);
        builder.HasKey(e => new { e.TagTypeId, e.EntranceId });

        builder
            .HasOne(e => e.TagType)
            .WithMany(e => e.EntranceOtherTags)
            .HasForeignKey(bc => bc.TagTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Entrance)
            .WithMany(e => e.EntranceOtherTags)
            .HasForeignKey(e => e.EntranceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}