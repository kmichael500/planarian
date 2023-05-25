using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class Map : EntityBase
{
    public string CaveId { get; set; }
    public string MapStatusTagId { get; set; }
    
    public TagType MapStatusTag { get; set; }
    
    public virtual Cave Cave { get; set; } = null!;
}

public class MapConfiguration : BaseEntityTypeConfiguration<Map>
{
    public override void Configure(EntityTypeBuilder<Map> builder)
    {
        builder
            .HasOne(e => e.Cave)
            .WithMany(e => e.Maps)
            .HasForeignKey(bc => bc.CaveId);

        builder.HasOne(e => e.MapStatusTag)
            .WithMany(e => e.MapStatusTags)
            .HasForeignKey(e => e.MapStatusTagId);
    }
}