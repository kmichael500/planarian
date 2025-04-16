using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;


namespace Planarian.Model.Database.Entities.RidgeWalker;

public class CaveGeoJson : EntityBase
{
    [MaxLength(PropertyLength.Id)]
    public string CaveId { get; set; } = null!;
    
    [MaxLength(PropertyLength.Max)] public string GeoJson { get; set; } = string.Empty;

    public virtual Cave Cave { get; set; } = null!;
}

public class CaveGeoJsonConfiguration : BaseEntityTypeConfiguration<CaveGeoJson>
{
    public override void Configure(EntityTypeBuilder<CaveGeoJson> builder)
    {
        builder.Property(c => c.GeoJson)
            .HasColumnType("jsonb");
            
        builder.HasOne(c => c.Cave)
            .WithMany(cave => cave.GeoJsons)
            .HasForeignKey(c => c.CaveId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
