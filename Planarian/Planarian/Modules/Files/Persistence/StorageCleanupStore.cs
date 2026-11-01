using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Shared.Base;
using Planarian.Shared.Services;

namespace Planarian.Modules.Files.Persistence;

public sealed record StorageCleanupAddress(string StoragePartition, string StorageKey, bool IsContainer = false)
{
    public static StorageCleanupAddress Object(StorageObjectAddress address) =>
        new(address.Partition, address.Key);
}

/// <summary>
/// Persistence boundary for durable post-commit object-storage cleanup debt.
/// Queueing is idempotent at the PostgreSQL unique constraint rather than through
/// caller-specific check-then-insert logic.
/// </summary>
public class StorageCleanupStore<TDbContext> : AccountScopedDataAccessBase<TDbContext>
    where TDbContext : PlanarianDbContextBase
{
    private const int LookupBatchSize = 500;

    public StorageCleanupStore(TDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    private sealed record QueueRow(
        string Id,
        string StoragePartition,
        string StorageKey,
        bool IsContainer);

    public static async Task<IReadOnlyList<StorageCleanupTarget>> QueueAsync(
        PlanarianDbContextBase db,
        string accountId,
        string? userId,
        StorageCleanupPurpose purpose,
        IEnumerable<StorageCleanupAddress> requestedAddresses,
        CancellationToken cancellationToken)
    {
        var addresses = requestedAddresses
            .Where(address => !string.IsNullOrWhiteSpace(address.StoragePartition) &&
                              (address.IsContainer || !string.IsNullOrWhiteSpace(address.StorageKey)))
            .Distinct()
            .ToList();
        if (addresses.Count == 0) return [];

        var purposeValue = purpose.ToString();
        foreach (var chunk in addresses.Chunk(LookupBatchSize))
        {
            var payload = JsonSerializer.Serialize(chunk.Select(address => new QueueRow(
                IdGenerator.Generate(),
                address.StoragePartition,
                address.StorageKey,
                address.IsContainer)));
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "StorageCleanupTargets"
                    ("Id", "AccountId", "StoragePartition", "StorageKey", "IsContainer", "Purpose",
                     "CreatedByUserId", "CreatedOn")
                SELECT row."Id", {accountId}, row."StoragePartition", row."StorageKey", row."IsContainer",
                       {purposeValue}, {userId}, clock_timestamp()
                FROM jsonb_to_recordset({payload}::jsonb) AS row(
                    "Id" text,
                    "StoragePartition" text,
                    "StorageKey" text,
                    "IsContainer" boolean)
                ON CONFLICT ("AccountId", "StoragePartition", "StorageKey", "IsContainer", "Purpose")
                DO NOTHING
                """, cancellationToken);
        }

        var results = new List<StorageCleanupTarget>(addresses.Count);
        foreach (var partition in addresses.GroupBy(address =>
                     (address.StoragePartition, address.IsContainer)))
        {
            foreach (var keyChunk in partition.Select(address => address.StorageKey)
                         .Distinct(StringComparer.Ordinal).Chunk(LookupBatchSize))
            {
                var keys = keyChunk.ToArray();
                var found = await db.StorageCleanupTargets.IgnoreQueryFilters().AsNoTracking()
                    .Where(row => row.AccountId == accountId &&
                                  row.Purpose == purpose &&
                                  row.IsContainer == partition.Key.IsContainer &&
                                  row.StoragePartition == partition.Key.StoragePartition &&
                                  keys.Contains(row.StorageKey))
                    .ToListAsync(cancellationToken);
                results.AddRange(found);
            }
        }

        if (results.Count != addresses.Count)
            throw new InvalidOperationException("Durable storage cleanup responsibility could not be recorded.");

        return results;
    }

    public Task<IReadOnlyList<StorageCleanupTarget>> QueueAsync(
        StorageCleanupPurpose purpose,
        IEnumerable<StorageCleanupAddress> addresses,
        CancellationToken cancellationToken)
    {
        return QueueAsync(DbContext, AccountId, UserId, purpose, addresses, cancellationToken);
    }

    public void ForgetTrackedChanges() => DbContext.ChangeTracker.Clear();

    public Task<List<StorageCleanupTarget>> GetTargetsAsync(
        IReadOnlyCollection<StorageCleanupPurpose> purposes,
        IEnumerable<string>? targetIds,
        int maxCount,
        CancellationToken cancellationToken)
    {
        var purposeValues = purposes.Distinct().ToArray();
        if (purposeValues.Length == 0) return Task.FromResult(new List<StorageCleanupTarget>());
        var ids = targetIds?.Distinct(StringComparer.Ordinal).ToArray();
        if (ids is { Length: 0 }) return Task.FromResult(new List<StorageCleanupTarget>());

        var query = DbContext.StorageCleanupTargets.AsNoTracking()
            .Where(row => row.AccountId == AccountId && purposeValues.Contains(row.Purpose));
        if (ids is not null) query = query.Where(row => ids.Contains(row.Id));
        return query.OrderBy(row => row.ModifiedOn ?? row.CreatedOn).ThenBy(row => row.Id)
            .Take(maxCount).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Rotate deletion failures behind newer work without losing the durable target.
    /// ModifiedOn records the latest failed attempt, so one permanently inaccessible
    /// object cannot indefinitely monopolize the bounded opportunistic sweep.
    /// </summary>
    public Task<int> DeferFailedAsync(
        IReadOnlyCollection<StorageCleanupPurpose> purposes,
        IEnumerable<string> targetIds,
        CancellationToken cancellationToken)
    {
        var purposeValues = purposes.Distinct().ToArray();
        var ids = targetIds.Distinct(StringComparer.Ordinal).ToArray();
        if (purposeValues.Length == 0 || ids.Length == 0) return Task.FromResult(0);
        var attemptedOn = DateTime.UtcNow;

        return DbContext.StorageCleanupTargets
            .Where(row => row.AccountId == AccountId &&
                          purposeValues.Contains(row.Purpose) && ids.Contains(row.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                row => row.ModifiedOn, attemptedOn), cancellationToken);
    }

    public async Task<int> CompleteAsync(
        IReadOnlyCollection<StorageCleanupPurpose> purposes,
        IEnumerable<string> targetIds,
        CancellationToken cancellationToken)
    {
        var purposeValues = purposes.Distinct().ToArray();
        var ids = targetIds.Distinct(StringComparer.Ordinal).ToArray();
        if (purposeValues.Length == 0 || ids.Length == 0) return 0;

        var deleted = await DbContext.StorageCleanupTargets
            .Where(row => row.AccountId == AccountId &&
                          purposeValues.Contains(row.Purpose) && ids.Contains(row.Id))
            .ExecuteDeleteAsync(cancellationToken);

        // ExecuteDelete bypasses the change tracker. Detach acknowledged persisted
        // targets so a later SaveChanges cannot act on a stale in-memory row.
        // Added targets remain tracked because they have not been persisted yet.
        var purposeSet = purposeValues.ToHashSet();
        var idSet = ids.ToHashSet(StringComparer.Ordinal);
        foreach (var entry in DbContext.ChangeTracker.Entries<StorageCleanupTarget>()
                     .Where(entry => entry.State != EntityState.Added &&
                                     entry.Entity.AccountId == AccountId &&
                                     purposeSet.Contains(entry.Entity.Purpose) &&
                                     idSet.Contains(entry.Entity.Id))
                     .ToList())
            entry.State = EntityState.Detached;

        return deleted;
    }

    public async Task<HashSet<string>> GetTargetIdsReferencedByLiveObjectsAsync(
        IReadOnlyCollection<StorageCleanupTarget> targets,
        CancellationToken cancellationToken)
    {
        var referencedTargetIds = new HashSet<string>(StringComparer.Ordinal);
        if (targets.Count == 0) return referencedTargetIds;

        foreach (var partition in targets.Where(target => !target.IsContainer)
                     .GroupBy(target => target.StoragePartition, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var targetChunk in partition.Chunk(LookupBatchSize))
            {
                var chunk = targetChunk.ToArray();
                var keys = chunk.Select(target => target.StorageKey)
                    .Distinct(StringComparer.Ordinal).ToArray();
                var normalizedPartition = partition.Key.ToLowerInvariant();

                // External deletion safety is intentionally global by storage address: a live reference in
                // any account must prevent deletion, even when stale or migrated data shares an address.
                var fileKeys = await DbContext.Files.IgnoreQueryFilters().AsNoTracking()
                    .Where(file => file.BlobContainer != null &&
                                   file.BlobContainer.ToLower() == normalizedPartition &&
                                   file.BlobKey != null && keys.Contains(file.BlobKey))
                    .Select(file => file.BlobKey!)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var importSourceKeys = await DbContext.CaveImportSources.IgnoreQueryFilters().AsNoTracking()
                    .Where(source => source.BlobContainer.ToLower() == normalizedPartition &&
                                     keys.Contains(source.BlobKey))
                    .Select(source => source.BlobKey)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var referencedKeys = fileKeys.Concat(importSourceKeys).ToHashSet(StringComparer.Ordinal);
                foreach (var target in chunk)
                    if (referencedKeys.Contains(target.StorageKey))
                        referencedTargetIds.Add(target.Id);
            }
        }

        return referencedTargetIds;
    }
}

public sealed class StorageCleanupStore : StorageCleanupStore<PlanarianDbContext>
{
    public StorageCleanupStore(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }
}
