using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

/// <summary>
/// Immutable object-storage source used by Cave/Entrance CSV imports.
/// Temporary uploads carry an expiration; a successful apply promotes the same source by clearing it.
/// This is import provenance, not a Cave File asset.
/// </summary>
public sealed class CaveImportSource : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.FileName)] public string FileName { get; set; } = null!;
    [MaxLength(PropertyLength.Key)] public string BlobContainer { get; set; } = null!;
    [MaxLength(PropertyLength.Key)] public string BlobKey { get; set; } = null!;
    public DateTime? ExpiresOn { get; set; }
}

public sealed class CaveImportSourceConfiguration : BaseEntityTypeConfiguration<CaveImportSource>
{
    public override void Configure(EntityTypeBuilder<CaveImportSource> builder)
    {
        base.Configure(builder);
        builder.HasAlternateKey(e => new { e.AccountId, e.Id });
        builder.HasOne<Account>().WithMany()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.AccountId, e.ExpiresOn, e.Id });
        builder.ToTable("CaveImportSources", table =>
        {
            table.HasCheckConstraint("CK_CaveImportSources_StorageAddress",
                "btrim(\"BlobContainer\") <> '' AND btrim(\"BlobKey\") <> ''");
            table.HasCheckConstraint("CK_CaveImportSources_FileName",
                "length(\"FileName\") > 0");
        });
    }
}
