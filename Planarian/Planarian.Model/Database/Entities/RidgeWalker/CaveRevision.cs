using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public enum CaveRevisionSource
{
    SystemBaseline,
    ManagerEdit,
    UserSubmission,
    Import,
    System
}

public enum CaveRevisionOperation
{
    Create,
    Update,
    Archive,
    Unarchive,
    Delete
}

public class CaveRevision : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CaveId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? PreviousRevisionId { get; set; }
    public CaveRevisionSource Source { get; set; }
    public CaveRevisionOperation Operation { get; set; }
    public int SnapshotSchemaVersion { get; set; } = 1;
    public string SnapshotJson { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? ChangeRequestId { get; set; }
    [MaxLength(PropertyLength.Id)] public string? ImportBatchId { get; set; }
}

public class CaveRevisionConfiguration : BaseEntityTypeConfiguration<CaveRevision>
{
    public override void Configure(EntityTypeBuilder<CaveRevision> builder)
    {
        builder.Property(e => e.Source).HasConversion<string>().HasMaxLength(PropertyLength.Key);
        builder.Property(e => e.Operation).HasConversion<string>().HasMaxLength(PropertyLength.Key);
        builder.Property(e => e.SnapshotJson).HasColumnType("jsonb");
        builder.HasAlternateKey(e => new { e.AccountId, e.Id });
        builder.HasOne<CaveRevision>().WithMany()
            .HasPrincipalKey(e => new { e.AccountId, e.Id })
            .HasForeignKey(e => new { e.AccountId, e.PreviousRevisionId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CaveImportBatch>().WithMany()
            .HasPrincipalKey(e => new { e.AccountId, e.Id })
            .HasForeignKey(e => new { e.AccountId, e.ImportBatchId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CaveChangeRequest>().WithMany()
            .HasPrincipalKey(e => new { e.AccountId, e.Id })
            .HasForeignKey(e => new { e.AccountId, e.ChangeRequestId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.AccountId, e.CaveId, e.CreatedOn });
        builder.HasIndex(e => e.PreviousRevisionId);
        builder.HasIndex(e => e.ChangeRequestId);
        builder.HasIndex(e => e.ImportBatchId);
    }
}

public class CaveImportBatch : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.FileName)] public string? SourceFileName { get; set; }
    public bool SyncExisting { get; set; }
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int DeletedCount { get; set; }
    public int NoChangeCount { get; set; }
}

public class CaveImportBatchConfiguration : BaseEntityTypeConfiguration<CaveImportBatch>
{
    public override void Configure(EntityTypeBuilder<CaveImportBatch> builder)
    {
        builder.HasAlternateKey(e => new { e.AccountId, e.Id });
        builder.HasIndex(e => new { e.AccountId, e.CreatedOn });
    }
}
