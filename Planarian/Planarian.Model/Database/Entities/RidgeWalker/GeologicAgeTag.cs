using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class GeologicAgeTag : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string TagTypeId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CaveId { get; set; } = null!;

    public TagType? TagType { get; set; }
    public Cave? Cave { get; set; }
}

public class GeologicAgeTagConfiguration : BaseEntityTypeConfiguration<GeologicAgeTag>
{
    public override void Configure(EntityTypeBuilder<GeologicAgeTag> builder)
    {
        base.Configure(builder);
        builder.HasKey(e => new { e.TagTypeId, e.CaveId });

        builder
            .HasOne(e => e.TagType)
            .WithMany(e => e.GeologicAgeTags)
            .HasForeignKey(bc => bc.TagTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Cave)
            .WithMany(e => e.GeologicAgeTags)
            .HasForeignKey(e => e.CaveId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}