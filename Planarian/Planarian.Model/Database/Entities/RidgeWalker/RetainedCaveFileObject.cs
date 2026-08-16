using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

/// <summary>
/// Internal locator for immutable Cave file bytes that were once published but
/// are no longer part of current Cave state. Historical user-facing metadata
/// remains in Cave revision snapshots; this row intentionally carries no live
/// Cave/File/TagType foreign keys.
/// </summary>
public sealed class RetainedCaveFileObject : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CaveId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string FileId { get; set; } = null!;
    [MaxLength(PropertyLength.Key)] public string StoragePartition { get; set; } = null!;
    [MaxLength(PropertyLength.Key)] public string StorageKey { get; set; } = null!;
}

public sealed class RetainedCaveFileObjectConfiguration : BaseEntityTypeConfiguration<RetainedCaveFileObject>
{
    public override void Configure(EntityTypeBuilder<RetainedCaveFileObject> builder)
    {
        // Deliberately do not add live-domain foreign keys. This row must remain
        // usable after the current File/Cave/TagType relationships are removed.
        builder.HasIndex(row => new { row.AccountId, row.FileId }).IsUnique();
        builder.HasIndex(row => new { row.AccountId, row.CaveId });
    }
}
