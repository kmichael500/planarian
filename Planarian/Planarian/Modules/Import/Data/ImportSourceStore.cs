using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Import.Planning;
using Planarian.Shared.Base;
using Planarian.Shared.Services;

using Planarian.Modules.Files.Persistence;
namespace Planarian.Modules.Import.Data;

public sealed record ImportSourceAccessInfo(
    string Id,
    string FileName,
    string BlobContainer,
    string BlobKey,
    DateTime? ExpiresOn);

public sealed record PersistedImportSource(string Id, string FileName);

/// <summary>
/// Database lifecycle for Cave/Entrance CSV import provenance.
/// Row locking serializes promotion and expiration cleanup on the source itself.
/// </summary>
public sealed class ImportSourceStore : AccountScopedDataAccessBase
{
    public ImportSourceStore(PlanarianDbContext dbContext, RequestUser requestUser)
        : base(dbContext, requestUser)
    {
    }

    public Task<ImportSourceAccessInfo?> GetAsync(string id, CancellationToken cancellationToken)
    {
        return DbContext.CaveImportSources.AsNoTracking()
            .Where(source => source.Id == id && source.AccountId == AccountId)
            .Select(source => new ImportSourceAccessInfo(
                source.Id,
                source.FileName,
                source.BlobContainer,
                source.BlobKey,
                source.ExpiresOn))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<PersistedImportSource> PromoteAsync(
        string sourceId,
        CancellationToken cancellationToken)
    {
        return PromoteAsync(DbContext, AccountId, UserId, sourceId, cancellationToken);
    }

    public static async Task<PersistedImportSource> PromoteAsync(
        PlanarianDbContext db,
        string accountId,
        string? userId,
        string sourceId,
        CancellationToken cancellationToken)
    {
        var source = await db.CaveImportSources
            .FromSqlInterpolated($"""
                SELECT *
                FROM "CaveImportSources"
                WHERE "AccountId" = {accountId} AND "Id" = {sourceId}
                FOR UPDATE
                """)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (source is null)
            throw new ImportPlanConcurrencyException(
                "The import source is no longer available. Re-upload and re-plan the import.");

        var expectedContainer = RequestUser.BuildAccountContainerName(accountId);
        if (!string.Equals(source.BlobContainer, expectedContainer, StringComparison.OrdinalIgnoreCase) ||
            !ImportSourceStoragePath.IsCaveEntranceCsvKey(source.BlobKey))
            throw new ImportPlanConcurrencyException(
                "The import source is not stored in the active account import-source namespace. Re-upload the CSV.");

        if (source.ExpiresOn is not null)
        {
            // CURRENT_TIMESTAMP/now() are transaction-start timestamps in PostgreSQL.
            // Promotion must use actual wall clock at the mutation itself.
            var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "CaveImportSources"
                SET "ExpiresOn" = NULL,
                    "ModifiedOn" = clock_timestamp(),
                    "ModifiedByUserId" = {userId}
                WHERE "AccountId" = {accountId}
                  AND "Id" = {sourceId}
                  AND "ExpiresOn" IS NOT NULL
                  AND "ExpiresOn" > clock_timestamp()
                """, cancellationToken);
            if (updated != 1)
                throw new ImportPlanConcurrencyException(
                    "The import source expired or changed while apply was starting. Re-upload and re-plan the import.");
        }

        return new PersistedImportSource(source.Id, source.FileName);
    }

    public Task<IReadOnlyList<string>> RemoveExpiredAsync(
        int maxCount,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);
        var accountId = AccountId;

        return RunInOwnedTransactionAsync<IReadOnlyList<string>>(async token =>
        {
            var expired = await DbContext.CaveImportSources
                .FromSqlInterpolated($"""
                    SELECT source.*
                    FROM "CaveImportSources" AS source
                    WHERE source."AccountId" = {accountId}
                      AND source."ExpiresOn" IS NOT NULL
                      AND source."ExpiresOn" < clock_timestamp()
                      AND NOT EXISTS (
                          SELECT 1
                          FROM "CaveImportBatches" AS batch
                          WHERE batch."AccountId" = source."AccountId"
                            AND batch."SourceFileId" = source."Id"
                      )
                    ORDER BY source."Id" COLLATE "C"
                    LIMIT {maxCount}
                    FOR UPDATE OF source
                    """)
                .IgnoreQueryFilters()
                .ToListAsync(token);

            if (expired.Count == 0) return [];

            var cleanupTargets = await StorageCleanupStore.QueueAsync(
                DbContext,
                accountId,
                UserId,
                StorageCleanupPurpose.ImportSourceRemoval,
                expired.Select(source => new StorageCleanupAddress(source.BlobContainer, source.BlobKey)),
                token);

            DbContext.CaveImportSources.RemoveRange(expired);
            await SaveChangesAsync(token);
            return cleanupTargets.Select(target => target.Id).Distinct(StringComparer.Ordinal).ToList();
        }, cancellationToken);
    }
}
