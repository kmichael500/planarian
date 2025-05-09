using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class EntranceReportedByNameTag : EntityBase, IEntranceTag
{
    [MaxLength(PropertyLength.Id)] public string TagTypeId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string EntranceId { get; set; } = null!;

    public TagType TagType { get; set; } = null!;
    public Entrance Entrance { get; set; } = null!;
}

public class EntranceReportedByNameTagConfiguration : BaseEntityTypeConfiguration<EntranceReportedByNameTag>
{
    public override void Configure(EntityTypeBuilder<EntranceReportedByNameTag> builder)
    {
        base.Configure(builder);
        builder.HasKey(e => new { e.TagTypeId, e.EntranceId });

        builder
            .HasOne(e => e.TagType)
            .WithMany(e => e.EntranceReportedByNameTags)
            .HasForeignKey(e => e.TagTypeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Entrance)
            .WithMany(e => e.EntranceReportedByNameTags)
            .HasForeignKey(e => e.EntranceId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}