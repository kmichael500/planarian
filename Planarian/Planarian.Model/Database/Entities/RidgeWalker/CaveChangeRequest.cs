using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public enum CaveChangeRequestStatus
{
    Pending,
    Approved,
    Rejected
}

public class CaveChangeRequest : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CaveId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? BaseRevisionId { get; set; }
    [MaxLength(PropertyLength.Id)] public string? CurrentProposalVersionId { get; set; }
    [MaxLength(PropertyLength.Id)] public string? ApprovedRevisionId { get; set; }
    public CaveChangeRequestStatus Status { get; set; } = CaveChangeRequestStatus.Pending;
    [MaxLength(PropertyLength.Id)] public string? ReviewerUserId { get; set; }
    public DateTime? ReviewedOn { get; set; }
    public string? ReviewerNotes { get; set; }
    [MaxLength(PropertyLength.Id)] public string? BaseStateId { get; set; }
    [MaxLength(PropertyLength.Id)] public string? BaseCountyId { get; set; }
    [MaxLength(PropertyLength.Id)] public string? ProposedStateId { get; set; }
    [MaxLength(PropertyLength.Id)] public string? ProposedCountyId { get; set; }
    public uint Version { get; private set; }
}

public class CaveChangeRequestConfiguration : BaseEntityTypeConfiguration<CaveChangeRequest>
{
    public override void Configure(EntityTypeBuilder<CaveChangeRequest> builder)
    {
        builder.HasAlternateKey(e => new { e.AccountId, e.Id });
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(PropertyLength.Key);
        builder.Property(e => e.Version).HasColumnName("xmin").IsRowVersion().ValueGeneratedOnAddOrUpdate();
        builder.HasIndex(e => new { e.AccountId, e.Status, e.CreatedOn });
        builder.HasIndex(e => new { e.CaveId, e.Status });
        builder.HasOne<CaveRevision>().WithMany()
            .HasPrincipalKey(e => new { e.AccountId, e.Id })
            .HasForeignKey(e => new { e.AccountId, e.BaseRevisionId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CaveRevision>().WithMany()
            .HasPrincipalKey(e => new { e.AccountId, e.Id })
            .HasForeignKey(e => new { e.AccountId, e.ApprovedRevisionId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CaveProposalVersion>().WithMany()
            .HasPrincipalKey(e => new { e.AccountId, e.Id })
            .HasForeignKey(e => new { e.AccountId, e.CurrentProposalVersionId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CaveProposalVersion : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string ChangeRequestId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? PreviousProposalVersionId { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public string ProposalJson { get; set; } = null!;
}

public class CaveProposalVersionConfiguration : BaseEntityTypeConfiguration<CaveProposalVersion>
{
    public override void Configure(EntityTypeBuilder<CaveProposalVersion> builder)
    {
        builder.HasAlternateKey(e => new { e.AccountId, e.Id });
        builder.Property(e => e.ProposalJson).HasColumnType("jsonb");
        builder.HasIndex(e => new { e.ChangeRequestId, e.CreatedOn });
        builder.HasOne<CaveChangeRequest>().WithMany()
            .HasPrincipalKey(e => new { e.AccountId, e.Id })
            .HasForeignKey(e => new { e.AccountId, e.ChangeRequestId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CaveProposalVersion>().WithMany()
            .HasPrincipalKey(e => new { e.AccountId, e.Id })
            .HasForeignKey(e => new { e.AccountId, e.PreviousProposalVersionId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CaveChangeRequestStagedFile : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string ChangeRequestId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string FileId { get; set; } = null!;
}

public class CaveChangeRequestStagedFileConfiguration : BaseEntityTypeConfiguration<CaveChangeRequestStagedFile>
{
    public override void Configure(EntityTypeBuilder<CaveChangeRequestStagedFile> builder)
    {
        builder.HasOne<CaveChangeRequest>().WithMany()
            .HasPrincipalKey(e => new { e.AccountId, e.Id })
            .HasForeignKey(e => new { e.AccountId, e.ChangeRequestId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<File>().WithMany()
            .HasForeignKey(e => e.FileId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.ChangeRequestId, e.FileId }).IsUnique();
    }
}
