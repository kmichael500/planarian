using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

/// <summary>
/// Transactional reset cleanup debt. Storage bytes must never be deleted before the
/// owning relational purge commits. Targets remain until confirmed absent in storage.
/// The locator is intentionally independent of deleted Cave/File foreign keys.
/// </summary>
public sealed class AccountResetCleanupTarget : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.Key)] public string StoragePartition { get; set; } = null!;
    [MaxLength(PropertyLength.Key)] public string StorageKey { get; set; } = null!;
    public bool IsContainer { get; set; }
}

public sealed class AccountResetCleanupTargetConfiguration : BaseEntityTypeConfiguration<AccountResetCleanupTarget>
{
    public override void Configure(EntityTypeBuilder<AccountResetCleanupTarget> builder)
    {
        builder.HasIndex(row => new { row.AccountId, row.StoragePartition, row.StorageKey, row.IsContainer }).IsUnique();
        builder.HasIndex(row => row.AccountId);
    }
}
