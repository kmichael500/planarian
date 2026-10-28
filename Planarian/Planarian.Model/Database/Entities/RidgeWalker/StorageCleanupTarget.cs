using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public enum StorageCleanupPurpose
{
    AccountReset,
    CaveAssetRemoval,
    UnpublishedFileRemoval
}

/// <summary>
/// Durable post-commit object-storage cleanup debt. Storage bytes must never be deleted before
/// the owning relational mutation commits. Targets remain until the external cleanup is safely
/// completed or the address is proven to still belong to a live File.
/// </summary>
public sealed class StorageCleanupTarget : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.Key)] public string StoragePartition { get; set; } = null!;
    [MaxLength(PropertyLength.Key)] public string StorageKey { get; set; } = null!;
    public bool IsContainer { get; set; }
    public StorageCleanupPurpose Purpose { get; set; }
}

public sealed class StorageCleanupTargetConfiguration : BaseEntityTypeConfiguration<StorageCleanupTarget>
{
    public override void Configure(EntityTypeBuilder<StorageCleanupTarget> builder)
    {
        ConfigureEnum(builder, row => row.Purpose);
        builder.HasIndex(row => new
        {
            row.AccountId, row.StoragePartition, row.StorageKey, row.IsContainer, row.Purpose
        }).IsUnique();
        builder.HasIndex(row => new { row.AccountId, row.Purpose });
    }
}
