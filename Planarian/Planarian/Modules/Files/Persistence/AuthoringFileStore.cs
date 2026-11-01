using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Models;
using Planarian.Shared.Base;
using Planarian.Shared.Services;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Modules.Files.Persistence;

public sealed record AuthoringFileAccess(
    string Id,
    string Name,
    string Extension,
    string BlobKey,
    string BlobContainer);

public sealed record AuthoringStagedObject(
    string Id,
    string StorageKey,
    string StoragePartition,
    DateTime ExpiresOn);

/// <summary>
/// Owns the database lifecycle for Cave Files between immutable object upload and
/// Cave publication. Proposal claims protect a staged File from expiration; unclaimed
/// Files remain creator-owned and deadline-bound.
/// </summary>
public class AuthoringFileStore<TDbContext> : AccountScopedDataAccessBase<TDbContext>
    where TDbContext : PlanarianDbContextBase
{
    private readonly TimeProvider _timeProvider;

    public AuthoringFileStore(
        TDbContext dbContext,
        RequestUser requestUser,
        TimeProvider? timeProvider = null) : base(dbContext, requestUser)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    private string AccountContainer => RequestUser.BuildAccountContainerName(AccountId);

    private IQueryable<File> UnclaimedCurrentUserFiles()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return DbContext.Files.IgnoreQueryFilters().AsNoTracking()
            .Where(file => file.AccountId == AccountId &&
                           file.CreatedByUserId == UserId &&
                           file.CaveId == null &&
                           file.ExpiresOn != null &&
                           file.ExpiresOn > now &&
                           file.BlobContainer == AccountContainer &&
                           file.BlobKey == AuthoringFileStoragePath.Prefix + file.Id &&
                           !DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters().Any(staged =>
                               staged.AccountId == AccountId && staged.FileId == file.Id));
    }

    public async Task<AuthoringFileAccess?> GetUnclaimedAccessAsync(
        string id,
        CancellationToken cancellationToken)
    {
        return await UnclaimedCurrentUserFiles()
            .Where(file => file.Id == id)
            .Select(file => new AuthoringFileAccess(
                file.Id,
                file.Name,
                file.Extension,
                file.BlobKey!,
                file.BlobContainer!))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuthoringStagedObject>> GetUnclaimedObjectsAsync(
        IEnumerable<string> fileIds,
        CancellationToken cancellationToken)
    {
        var ids = fileIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return [];

        return await UnclaimedCurrentUserFiles()
            .Where(file => ids.Contains(file.Id))
            .Select(file => new AuthoringStagedObject(
                file.Id,
                file.BlobKey!,
                file.BlobContainer!,
                file.ExpiresOn!.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<string>> GetAuthorableIdsAsync(
        IEnumerable<string> fileIds,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var ids = fileIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return new HashSet<string>(StringComparer.Ordinal);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var rows = await DbContext.Files.IgnoreQueryFilters().AsNoTracking()
            .Where(file => file.AccountId == AccountId &&
                           ids.Contains(file.Id) &&
                           file.CaveId == null &&
                           file.BlobContainer == AccountContainer &&
                           file.BlobKey == AuthoringFileStoragePath.Prefix + file.Id &&
                           ((requestId != null &&
                             DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters().Any(staged =>
                                 staged.AccountId == AccountId &&
                                 staged.ChangeRequestId == requestId &&
                                 staged.FileId == file.Id)) ||
                            (file.CreatedByUserId == UserId &&
                             file.ExpiresOn != null &&
                             file.ExpiresOn > now &&
                             !DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters().Any(staged =>
                                 staged.AccountId == AccountId &&
                                 staged.FileId == file.Id))))
            .Select(file => file.Id)
            .ToListAsync(cancellationToken);

        return rows.ToHashSet(StringComparer.Ordinal);
    }

    public async Task ClaimAsync(
        string requestId,
        IEnumerable<string> fileIds,
        CancellationToken cancellationToken)
    {
        var ids = fileIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0) return;

        var existingIds = (await DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
            .Where(staged => staged.AccountId == AccountId &&
                             staged.ChangeRequestId == requestId &&
                             ids.Contains(staged.FileId))
            .Select(staged => staged.FileId)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var fileId in ids.Where(id => !existingIds.Contains(id)))
        {
            var expectedKey = AuthoringFileStoragePath.BuildKey(fileId);
            var file = await DbContext.Files
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "Files"
                    WHERE "AccountId" = {AccountId}
                      AND "Id" = {fileId}
                      AND "CreatedByUserId" = {UserId}
                      AND "CaveId" IS NULL
                      AND "ExpiresOn" IS NOT NULL
                      AND "ExpiresOn" > clock_timestamp()
                      AND "BlobContainer" = {AccountContainer}
                      AND "BlobKey" = {expectedKey}
                    FOR UPDATE
                    """)
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            if (file is null)
                throw ApiExceptionDictionary.NotFound("Staged file");

            var existingClaim = await DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
                .Where(staged => staged.AccountId == AccountId && staged.FileId == fileId)
                .Select(staged => staged.ChangeRequestId)
                .SingleOrDefaultAsync(cancellationToken);
            if (existingClaim is not null)
                throw ApiExceptionDictionary.BadRequest(
                    "The staged file is already bound to another change request.");

            DbContext.CaveChangeRequestStagedFiles.Add(new CaveChangeRequestStagedFile
            {
                AccountId = AccountId,
                ChangeRequestId = requestId,
                FileId = fileId
            });
        }
    }

    public async Task<IReadOnlyList<File>> AttachUnclaimedToCaveAsync(
        string caveId,
        IEnumerable<string> fileIds,
        CancellationToken cancellationToken)
    {
        var ids = fileIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0) return [];

        var files = new List<File>(ids.Count);
        foreach (var id in ids)
        {
            var expectedKey = AuthoringFileStoragePath.BuildKey(id);
            var file = await DbContext.Files.FromSqlInterpolated($"""
                    SELECT *
                    FROM "Files"
                    WHERE "AccountId" = {AccountId}
                      AND "Id" = {id}
                      AND "CreatedByUserId" = {UserId}
                      AND "CaveId" IS NULL
                      AND "ExpiresOn" IS NOT NULL
                      AND "ExpiresOn" > clock_timestamp()
                      AND "BlobContainer" = {AccountContainer}
                      AND "BlobKey" = {expectedKey}
                    FOR UPDATE
                    """)
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(cancellationToken);

            if (file is null)
                throw ApiExceptionDictionary.NotFound("Staged file");

            var claimed = await DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
                .AnyAsync(staged => staged.AccountId == AccountId && staged.FileId == id, cancellationToken);
            if (claimed)
                throw ApiExceptionDictionary.NotFound("Staged file");

            files.Add(file);
        }

        foreach (var file in files)
        {
            file.CaveId = caveId;
            file.ExpiresOn = null;
        }

        return files;
    }

    public async Task<IReadOnlyList<StagedCaveFilePublication>> PlanRequestPublicationsAsync(
        string requestId,
        IReadOnlyDictionary<string, string> extensionsByFileId,
        CancellationToken cancellationToken)
    {
        var ids = extensionsByFileId.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return [];

        var rows = await (from staged in DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
                join file in DbContext.Files.IgnoreQueryFilters()
                    on new { staged.AccountId, staged.FileId }
                    equals new { file.AccountId, FileId = file.Id }
                where staged.AccountId == AccountId &&
                      staged.ChangeRequestId == requestId &&
                      ids.Contains(staged.FileId) &&
                      file.CaveId == null &&
                      file.BlobContainer == AccountContainer &&
                      file.BlobKey == AuthoringFileStoragePath.Prefix + file.Id
                select file)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (rows.Count != ids.Count ||
            rows.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count() != ids.Count)
            throw ApiExceptionDictionary.NotFound("Staged file");

        var sharedPendingFileId = await FindSharedPendingClaimAsync(
            requestId, ids, cancellationToken);
        if (sharedPendingFileId is not null)
            throw ApiExceptionDictionary.BadRequest(
                "A staged file shared with another pending request cannot be published.");

        return rows.Select(file =>
                new StagedCaveFilePublication(
                    file.Id,
                    file.BlobKey!,
                    file.BlobContainer!,
                    extensionsByFileId[file.Id]))
            .OrderBy(publication => publication.FileId, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<List<File>> AttachRequestFilesToCaveAsync(
        string requestId,
        string caveId,
        IReadOnlyList<StagedCaveFilePublication> publications,
        CancellationToken cancellationToken)
    {
        var byId = publications.ToDictionary(publication => publication.FileId, StringComparer.Ordinal);
        var ids = byId.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return [];

        var stagedIds = await DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
            .Where(staged => staged.AccountId == AccountId &&
                             staged.ChangeRequestId == requestId &&
                             ids.Contains(staged.FileId))
            .Select(staged => staged.FileId)
            .ToListAsync(cancellationToken);
        if (stagedIds.Count != ids.Count)
            throw ApiExceptionDictionary.NotFound("Staged file");

        var files = new List<File>(ids.Count);
        foreach (var id in ids)
        {
            var expectedKey = AuthoringFileStoragePath.BuildKey(id);
            var file = await DbContext.Files.FromSqlInterpolated($"""
                    SELECT *
                    FROM "Files"
                    WHERE "AccountId" = {AccountId}
                      AND "Id" = {id}
                      AND "CaveId" IS NULL
                      AND "BlobContainer" = {AccountContainer}
                      AND "BlobKey" = {expectedKey}
                    FOR UPDATE
                    """)
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(cancellationToken);
            if (file is null)
                throw ApiExceptionDictionary.NotFound("Staged file");
            files.Add(file);
        }

        var sharedPendingFileId = await FindSharedPendingClaimAsync(
            requestId, ids, cancellationToken);
        if (sharedPendingFileId is not null)
            throw ApiExceptionDictionary.BadRequest(
                "A staged file shared with another pending request cannot be published.");

        foreach (var file in files)
        {
            var publication = byId[file.Id];
            if (!string.Equals(file.BlobKey, publication.StorageKey, StringComparison.Ordinal) ||
                !string.Equals(file.BlobContainer, publication.StoragePartition, StringComparison.Ordinal))
                throw ApiExceptionDictionary.NotFound("Staged file");

            file.Extension = publication.Extension;
            file.CaveId = caveId;
            file.ExpiresOn = null;
        }

        await DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
            .Where(staged => staged.AccountId == AccountId &&
                             staged.ChangeRequestId == requestId &&
                             stagedIds.Contains(staged.FileId))
            .ExecuteDeleteAsync(cancellationToken);

        return files;
    }

    public Task<IReadOnlyList<string>> RemoveUnclaimedAsync(
        string id,
        CancellationToken cancellationToken) =>
        RunInOwnedTransactionAsync<IReadOnlyList<string>>(async token =>
        {
            var expectedKey = AuthoringFileStoragePath.BuildKey(id);
            var file = await DbContext.Files.FromSqlInterpolated($"""
                    SELECT *
                    FROM "Files"
                    WHERE "AccountId" = {AccountId}
                      AND "Id" = {id}
                      AND "CreatedByUserId" = {UserId}
                      AND "CaveId" IS NULL
                      AND "ExpiresOn" IS NOT NULL
                      AND "BlobContainer" = {AccountContainer}
                      AND "BlobKey" = {expectedKey}
                    FOR UPDATE
                    """)
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(token);

            if (file is null) return [];

            var claimed = await DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
                .AnyAsync(staged => staged.AccountId == AccountId && staged.FileId == id, token);
            if (claimed) return [];

            var cleanupTargets = await StorageCleanupStore.QueueAsync(
                DbContext,
                AccountId,
                UserId,
                StorageCleanupPurpose.UnpublishedFileRemoval,
                [new StorageCleanupAddress(file.BlobContainer!, file.BlobKey!)],
                token);

            var deleted = await DbContext.Files.IgnoreQueryFilters()
                .Where(row => row.AccountId == AccountId &&
                              row.Id == id &&
                              row.CaveId == null &&
                              row.ExpiresOn != null)
                .ExecuteDeleteAsync(token);
            if (deleted != 1)
                throw new InvalidOperationException("The locked staged File could not be deleted.");

            await SaveChangesAsync(token);
            return cleanupTargets.Select(target => target.Id)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }, cancellationToken);

    public Task<IReadOnlyList<string>> RemoveExpiredAsync(
        int maxCount,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);

        return RunInOwnedTransactionAsync<IReadOnlyList<string>>(async token =>
        {
            var expiredFiles = await DbContext.Files.FromSqlInterpolated($"""
                    SELECT file.*
                    FROM "Files" AS file
                    WHERE file."AccountId" = {AccountId}
                      AND file."CaveId" IS NULL
                      AND file."ExpiresOn" < clock_timestamp()
                      AND file."BlobContainer" = {AccountContainer}
                      AND file."BlobKey" = 'objects/files/' || file."Id"
                      AND NOT EXISTS (
                          SELECT 1
                          FROM "CaveChangeRequestStagedFiles" AS staged
                          WHERE staged."AccountId" = file."AccountId"
                            AND staged."FileId" = file."Id"
                      )
                    ORDER BY file."Id" COLLATE "C"
                    LIMIT {maxCount}
                    FOR UPDATE OF file
                    """)
                .IgnoreQueryFilters()
                .ToListAsync(token);

            if (expiredFiles.Count == 0) return [];

            // A claim may commit while the SELECT above waits for a File lock without
            // modifying the File tuple. Re-read after all locks so READ COMMITTED sees it.
            var candidateIds = expiredFiles.Select(file => file.Id).ToArray();
            var protectedIds = (await DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters().AsNoTracking()
                    .Where(staged => staged.AccountId == AccountId &&
                                     candidateIds.Contains(staged.FileId))
                    .Select(staged => staged.FileId)
                    .ToListAsync(token))
                .ToHashSet(StringComparer.Ordinal);

            var deletable = expiredFiles
                .Where(file => !protectedIds.Contains(file.Id))
                .ToList();
            if (deletable.Count == 0) return [];

            var cleanupTargets = await StorageCleanupStore.QueueAsync(
                DbContext,
                AccountId,
                UserId,
                StorageCleanupPurpose.UnpublishedFileRemoval,
                deletable.Select(file => new StorageCleanupAddress(file.BlobContainer!, file.BlobKey!)),
                token);

            DbContext.Files.RemoveRange(deletable);
            await SaveChangesAsync(token);

            return cleanupTargets.Select(target => target.Id)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<StorageCleanupTarget>> RemoveRequestClaimsAndUnreferencedAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        var stagedFileIds = await DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
            .Where(staged => staged.AccountId == AccountId &&
                             staged.ChangeRequestId == requestId)
            .Select(staged => staged.FileId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (stagedFileIds.Count == 0) return [];
        if (DbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Releasing proposal File claims requires a transaction.");

        // Serialize claim release against new proposal claims before the relationship
        // rows disappear. ClaimAsync locks the same File row before inserting its FK;
        // without this lock, a new claim can appear after the unreferenced query but
        // before SaveChanges deletes the File, aborting rejection/approval.
        var idsToLock = stagedFileIds.Order(StringComparer.Ordinal).ToArray();
        await DbContext.Files.FromSqlInterpolated($"""
                SELECT * FROM "Files"
                WHERE "AccountId" = {AccountId} AND "Id" = ANY({idsToLock})
                ORDER BY "Id" COLLATE "C"
                FOR UPDATE
                """)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        await DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
            .Where(staged => staged.AccountId == AccountId &&
                             staged.ChangeRequestId == requestId)
            .ExecuteDeleteAsync(cancellationToken);

        var files = await DbContext.Files.IgnoreQueryFilters()
            .Where(file => file.AccountId == AccountId &&
                           stagedFileIds.Contains(file.Id) &&
                           file.CaveId == null &&
                           file.BlobContainer == AccountContainer &&
                           file.BlobKey == AuthoringFileStoragePath.Prefix + file.Id &&
                           !DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters().Any(staged =>
                               staged.AccountId == AccountId && staged.FileId == file.Id))
            .ToListAsync(cancellationToken);

        var targets = await StorageCleanupStore.QueueAsync(
            DbContext,
            AccountId,
            UserId,
            StorageCleanupPurpose.UnpublishedFileRemoval,
            files.Select(file => new StorageCleanupAddress(file.BlobContainer!, file.BlobKey!)),
            cancellationToken);

        DbContext.Files.RemoveRange(files);
        return targets;
    }

    private Task<string?> FindSharedPendingClaimAsync(
        string requestId,
        IReadOnlyCollection<string> fileIds,
        CancellationToken cancellationToken) =>
        (from staged in DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
            join request in DbContext.CaveChangeRequests.IgnoreQueryFilters()
                on new { staged.AccountId, Id = staged.ChangeRequestId }
                equals new { request.AccountId, request.Id }
            where staged.AccountId == AccountId &&
                  fileIds.Contains(staged.FileId) &&
                  staged.ChangeRequestId != requestId &&
                  CaveChangeRequestStatePolicy.OpenStatuses.Contains(request.Status)
            select staged.FileId)
        .FirstOrDefaultAsync(cancellationToken);
}

public sealed class AuthoringFileStore : AuthoringFileStore<PlanarianDbContext>
{
    public AuthoringFileStore(
        PlanarianDbContext dbContext,
        RequestUser requestUser,
        TimeProvider timeProvider)
        : base(dbContext, requestUser, timeProvider)
    {
    }
}
