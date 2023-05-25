using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class GeologyTag : EntityBase
{
    public string TagTypeId { get; set; }
    public string CaveId { get; set; }

    public TagType TagType { get; set; }
    public Cave Cave { get; set; }
}

public class GeologyTagConfiguration : BaseEntityTypeConfiguration<GeologyTag>
{
    public override void Configure(EntityTypeBuilder<GeologyTag> builder)
    {
        builder
            .HasOne(e => e.TagType)
            .WithMany(e => e.GeologyTags)
            .HasForeignKey(bc => bc.TagTypeId);

        builder.HasOne(e => e.Cave)
            .WithMany(e => e.GeologyTags)
            .HasForeignKey(e => e.CaveId);
    }
}