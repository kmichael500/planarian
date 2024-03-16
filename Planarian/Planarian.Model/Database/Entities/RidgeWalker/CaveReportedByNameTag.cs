using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class CaveReportedByNameTag : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string PeopleTagId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CaveId { get; set; } = null!;

    public PeopleTag PeopleTag { get; set; }
    public Cave Cave { get; set; }
}

public class ReportedByNameTagConfiguration : BaseEntityTypeConfiguration<CaveReportedByNameTag>
{
    public override void Configure(EntityTypeBuilder<CaveReportedByNameTag> builder)
    {
        base.Configure(builder);
        builder.HasKey(e => new { e.PeopleTagId, e.CaveId });

        builder
            .HasOne(e => e.PeopleTag)
            .WithMany(e => e.ReportedByNameTags)
            .HasForeignKey(bc => bc.PeopleTagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Cave)
            .WithMany(e => e.CaveReportedByNameTags)
            .HasForeignKey(e => e.CaveId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}