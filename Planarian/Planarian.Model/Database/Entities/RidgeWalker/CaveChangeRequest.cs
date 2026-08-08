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
    [MaxLength(PropertyLength.Id)] public string BaseRevisionId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CurrentProposalVersionId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? ApprovedRevisionId { get; set; }
    public CaveChangeRequestStatus Status { get; set; } = CaveChangeRequestStatus.Pending;
    [MaxLength(PropertyLength.Id)] public string? ReviewerUserId { get; set; }
    public DateTime? ReviewedOn { get; set; }
    public string? ReviewerNotes { get; set; }
    public string? BaseGeographicScopeJson { get; set; }
    public string? ProposedGeographicScopeJson { get; set; }
    public uint Version { get; private set; }
}

public class CaveChangeRequestConfiguration : BaseEntityTypeConfiguration<CaveChangeRequest>
{
    public override void Configure(EntityTypeBuilder<CaveChangeRequest> builder)
    {
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(PropertyLength.Key);
        builder.Property(e => e.BaseGeographicScopeJson).HasColumnType("jsonb");
        builder.Property(e => e.ProposedGeographicScopeJson).HasColumnType("jsonb");
        builder.Property(e => e.Version).HasColumnName("xmin").IsRowVersion().ValueGeneratedOnAddOrUpdate();
        builder.HasIndex(e => new { e.AccountId, e.Status, e.CreatedOn });
        builder.HasIndex(e => new { e.CaveId, e.Status });
    }
}

public class CaveProposalVersion : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string ChangeRequestId { get; set; } = null!;
    public int SchemaVersion { get; set; } = 1;
    public string ProposalJson { get; set; } = null!;
}

public class CaveProposalVersionConfiguration : BaseEntityTypeConfiguration<CaveProposalVersion>
{
    public override void Configure(EntityTypeBuilder<CaveProposalVersion> builder)
    {
        builder.Property(e => e.ProposalJson).HasColumnType("jsonb");
        builder.HasIndex(e => new { e.ChangeRequestId, e.CreatedOn });
    }
}

public class CaveChangeRequestStagedFile : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string ChangeRequestId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string FileId { get; set; } = null!;
}

public class CaveChangeRequestStagedFileConfiguration : BaseEntityTypeConfiguration<CaveChangeRequestStagedFile>
{
    public override void Configure(EntityTypeBuilder<CaveChangeRequestStagedFile> builder)
    {
        builder.HasIndex(e => new { e.ChangeRequestId, e.FileId }).IsUnique();
    }
}
