using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class PhysiographicProvinceTag : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string TagTypeId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CaveId { get; set; } = null!;

    public TagType TagType { get; set; } = null!;
    public Cave Cave { get; set; } = null!;
}

public class PhysiographicProvinceTagConfiguration : BaseEntityTypeConfiguration<PhysiographicProvinceTag>
{
    public override void Configure(EntityTypeBuilder<PhysiographicProvinceTag> builder)
    {
        base.Configure(builder);
        builder.HasKey(e => new { e.TagTypeId, e.CaveId });

        builder
            .HasOne(e => e.TagType)
            .WithMany(e => e.PhysiographicProvinceTags)
            .HasForeignKey(bc => bc.TagTypeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Cave)
            .WithMany(e => e.PhysiographicProvinceTags)
            .HasForeignKey(e => e.CaveId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}