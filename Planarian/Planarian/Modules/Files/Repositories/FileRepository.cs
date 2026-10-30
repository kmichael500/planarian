using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Services;
using Planarian.Shared.Base;
using Planarian.Shared.Services;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Modules.Files.Repositories;

public class FileRepository<TDbContext> : RepositoryBase<TDbContext> where TDbContext : PlanarianDbContextBase
{
    private const int CleanupLookupBatchSize = 500;

    public FileRepository(TDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<File?> GetDeletableUnpublishedFileByIdAsync(string id,
        CancellationToken cancellationToken)
    {
        return await DbContext.Files.Where(file => file.Id == id &&
                file.AccountId == RequestUser.AccountId && file.CreatedByUserId == RequestUser.Id &&
                file.CaveId == null &&
                !DbContext.CaveChangeRequestStagedFiles.Any(staged =>
                    staged.AccountId == RequestUser.AccountId && staged.FileId == file.Id))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<FileAccessInfoResult?> GetAuthoringStagedFileAccessInfoAsync(string id,
        CancellationToken cancellationToken)
    {
        return await DbContext.Files.AsNoTracking()
            .Where(file => file.Id == id && file.AccountId == RequestUser.AccountId &&
                           file.CreatedByUserId == RequestUser.Id && file.CaveId == null && file.ExpiresOn != null &&
                           !DbContext.CaveChangeRequestStagedFiles.Any(staged =>
                               staged.AccountId == RequestUser.AccountId && staged.FileId == file.Id))
            .Select(file => new FileAccessInfoResult(
                file.Id, file.Name, file.Extension, file.BlobKey, file.BlobContainer, file.CaveId, null, null))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public sealed record AuthoringStagedObjectResult(
        string Id, string StorageKey, string StoragePartition, DateTime ExpiresOn);

    public async Task<IReadOnlyList<AuthoringStagedObjectResult>> GetAuthoringStagedObjectsAsync(
        IEnumerable<string> fileIds, CancellationToken cancellationToken)
    {
        var ids = fileIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return [];

        return await DbContext.Files.AsNoTracking()
            .Where(file => ids.Contains(file.Id) && file.AccountId == RequestUser.AccountId &&
                           file.CreatedByUserId == RequestUser.Id && file.CaveId == null && file.ExpiresOn != null &&
                           file.BlobKey != null && file.BlobContainer != null &&
                           !DbContext.CaveChangeRequestStagedFiles.Any(staged =>
                               staged.AccountId == RequestUser.AccountId && staged.FileId == file.Id))
            .Select(file => new AuthoringStagedObjectResult(
                file.Id, file.BlobKey!, file.BlobContainer!, file.ExpiresOn!.Value))
            .ToListAsync(cancellationToken);
    }

    public sealed record FileAuthorizationContextResult(string Id, string? CaveId, string? CountyId, string? StateId);

    public async Task<FileAuthorizationContextResult?> GetFileAuthorizationContext(string id)
    {
        return await DbContext.Files
            .Where(e => e.Id == id && e.AccountId == RequestUser.AccountId)
            .Select(e => new FileAuthorizationContextResult(e.Id, e.CaveId, e.Cave != null ? e.Cave.CountyId : null,
                    e.Cave != null ? e.Cave.StateId : null))
                .FirstOrDefaultAsync();
    }

    public sealed record FileAccessInfoResult(
        string Id,
        string Name,
        string Extension,
        string? BlobKey,
        string? ContainerName,
        string? CaveId,
        string? CountyId,
        string? StateId
        );

    public async Task<FileAccessInfoResult?> GetFileAccessInfo(string id)
    {
        return await DbContext.Files
            .Where(e => e.Id == id && e.AccountId == RequestUser.AccountId)
            .Select(e => new FileAccessInfoResult(
                e.Id,
                e.Name,
                e.Extension,
                e.BlobKey,
                e.BlobContainer,
                e.CaveId,
                e.Cave != null ? e.Cave.CountyId : null,
                e.Cave != null ? e.Cave.StateId : null
            ))
            .FirstOrDefaultAsync();
    }

    public sealed record GetFileBlobPropertiesResult(string? BlobKey, string? ContainerName);

    public async Task<GetFileBlobPropertiesResult?> GetFileBlobProperties(string id)
    {
        return await DbContext.Files.Where(e => e.Id == id && e.AccountId == RequestUser.AccountId)
            .Select(e => new GetFileBlobPropertiesResult(e.BlobKey, e.BlobContainer)).FirstOrDefaultAsync();
    }

    public async Task<FileVm?> GetFileVm(string id)
    {
        return await ToFileVm(DbContext.Files.Where(e => e.Id == id && e.AccountId == RequestUser.AccountId))
            .FirstOrDefaultAsync();
    }

    private IQueryable<FileVm> ToFileVm(IQueryable<File> query)
    {
        return query.Select(e => new FileVm
        {
            Id = e.Id,
            Name = e.Name,
            Extension = e.Extension,
            FileTypeKey = e.FileTypeTag.Name,
            FileTypeTagId = e.FileTypeTagId
        });
    }

    public async Task<IEnumerable<File>> GetExpiredFiles()
    {
        return await DbContext.Files.Where(e => e.ExpiresOn < DateTime.UtcNow &&
                                                e.AccountId == RequestUser.AccountId &&
                                                !DbContext.CaveChangeRequestStagedFiles.Any(staged =>
                                                    staged.AccountId == e.AccountId && staged.FileId == e.Id))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<StorageCleanupTarget>> QueueUnpublishedFileObjectCleanupAsync(
        IEnumerable<File> files, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw new InvalidOperationException("The current account is required for unpublished File cleanup.");

        var candidates = new List<(string Partition, string Key)>();
        foreach (var file in files)
        {
            if (file.AccountId != RequestUser.AccountId)
                throw new InvalidOperationException("The unpublished File does not belong to the current account.");
            if (string.IsNullOrWhiteSpace(file.BlobKey) && string.IsNullOrWhiteSpace(file.BlobContainer)) continue;
            if (string.IsNullOrWhiteSpace(file.BlobKey) || string.IsNullOrWhiteSpace(file.BlobContainer))
                throw new InvalidOperationException("The unpublished File has an incomplete object-storage address.");
            candidates.Add((file.BlobContainer, file.BlobKey));
        }

        var addresses = candidates.Distinct().ToList();
        if (addresses.Count == 0) return [];

        var existingByAddress = DbContext.StorageCleanupTargets.Local
            .Where(row => row.AccountId == RequestUser.AccountId &&
                          row.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval && !row.IsContainer)
            .ToDictionary(row => (row.StoragePartition, row.StorageKey));

        foreach (var partition in addresses.GroupBy(address => address.Partition))
        {
            foreach (var keyChunk in partition.Select(address => address.Key)
                         .Distinct().Chunk(CleanupLookupBatchSize))
            {
                var keys = keyChunk.ToArray();
                var persisted = await DbContext.StorageCleanupTargets
                    .Where(row => row.AccountId == RequestUser.AccountId &&
                                  row.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval &&
                                  !row.IsContainer && row.StoragePartition == partition.Key &&
                                  keys.Contains(row.StorageKey))
                    .ToListAsync(cancellationToken);
                foreach (var target in persisted)
                    existingByAddress.TryAdd((target.StoragePartition, target.StorageKey), target);
            }
        }

        var targets = new List<StorageCleanupTarget>(addresses.Count);
        foreach (var address in addresses)
        {
            if (existingByAddress.TryGetValue(address, out var existing))
            {
                targets.Add(existing);
                continue;
            }

            var target = new StorageCleanupTarget
            {
                AccountId = RequestUser.AccountId,
                StoragePartition = address.Partition,
                StorageKey = address.Key,
                Purpose = StorageCleanupPurpose.UnpublishedFileRemoval
            };
            DbContext.StorageCleanupTargets.Add(target);
            existingByAddress.Add(address, target);
            targets.Add(target);
        }

        return targets;
    }

    public async Task<bool> IsDuplicateFile(string caveId, string name, string extension)
    {
        return await DbContext.Files.AnyAsync(e =>
            e.CaveId == caveId && e.Name == name && e.Extension == extension &&
            e.AccountId == RequestUser.AccountId);
    }

    public Task<List<StorageCleanupTarget>> GetStorageCleanupTargetsAsync(
        IReadOnlyCollection<StorageCleanupPurpose> purposes, IEnumerable<string>? targetIds, int maxCount,
        CancellationToken cancellationToken)
    {
        var purposeValues = purposes.Distinct().ToArray();
        if (purposeValues.Length == 0) return Task.FromResult(new List<StorageCleanupTarget>());
        var ids = targetIds?.Distinct(StringComparer.Ordinal).ToArray();
        if (ids is { Length: 0 }) return Task.FromResult(new List<StorageCleanupTarget>());
        var query = DbContext.StorageCleanupTargets.AsNoTracking()
            .Where(row => row.AccountId == RequestUser.AccountId && purposeValues.Contains(row.Purpose));
        if (ids is not null) query = query.Where(row => ids.Contains(row.Id));
        return query.OrderBy(row => row.CreatedOn).ThenBy(row => row.Id)
            .Take(maxCount).ToListAsync(cancellationToken);
    }

    public async Task<int> CompleteStorageCleanupTargetsAsync(
        IReadOnlyCollection<StorageCleanupPurpose> purposes, IEnumerable<string> targetIds,
        CancellationToken cancellationToken)
    {
        var purposeValues = purposes.Distinct().ToArray();
        var ids = targetIds.Distinct(StringComparer.Ordinal).ToArray();
        if (purposeValues.Length == 0 || ids.Length == 0) return 0;

        var deleted = await DbContext.StorageCleanupTargets
            .Where(row => row.AccountId == RequestUser.AccountId &&
                          purposeValues.Contains(row.Purpose) && ids.Contains(row.Id))
            .ExecuteDeleteAsync(cancellationToken);

        // ExecuteDelete intentionally bypasses the change tracker. A persisted target may have
        // been loaded/created earlier in this same scoped DbContext, so detach acknowledged
        // persisted rows to prevent later queueing from mistaking stale local state for durable
        // debt. Never detach Added rows: they have not reached the database and therefore
        // cannot have been acknowledged by ExecuteDelete.
        var purposeSet = purposeValues.ToHashSet();
        var idSet = ids.ToHashSet(StringComparer.Ordinal);
        foreach (var entry in DbContext.ChangeTracker.Entries<StorageCleanupTarget>()
                     .Where(entry => entry.State != EntityState.Added &&
                                     entry.Entity.AccountId == RequestUser.AccountId &&
                                     purposeSet.Contains(entry.Entity.Purpose) &&
                                     idSet.Contains(entry.Entity.Id))
                     .ToList())
            entry.State = EntityState.Detached;

        return deleted;
    }

    public async Task<HashSet<string>> GetCleanupTargetIdsReferencedByLiveFilesAsync(
        IReadOnlyCollection<StorageCleanupTarget> targets, CancellationToken cancellationToken)
    {
        var referencedTargetIds = new HashSet<string>(StringComparer.Ordinal);
        if (targets.Count == 0) return referencedTargetIds;

        foreach (var partition in targets.GroupBy(
                     target => target.StoragePartition, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var targetChunk in partition.Chunk(CleanupLookupBatchSize))
            {
                var chunk = targetChunk.ToArray();
                var keys = chunk.Select(target => target.StorageKey)
                    .Distinct(StringComparer.Ordinal).ToArray();
                var normalizedPartition = partition.Key.ToLowerInvariant();

                var ownKeys = await DbContext.Files.AsNoTracking()
                    .Where(file => file.AccountId == RequestUser.AccountId &&
                                   file.BlobContainer != null &&
                                   file.BlobContainer.ToLower() == normalizedPartition &&
                                   file.BlobKey != null && keys.Contains(file.BlobKey))
                    .Select(file => file.BlobKey!)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var foreignKeys = await DbContext.Files.IgnoreQueryFilters().AsNoTracking()
                    .Where(file => file.AccountId != RequestUser.AccountId &&
                                   file.BlobContainer != null &&
                                   file.BlobContainer.ToLower() == normalizedPartition &&
                                   file.BlobKey != null && keys.Contains(file.BlobKey))
                    .Select(file => file.BlobKey!)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var referencedKeys = ownKeys.Concat(foreignKeys)
                    .ToHashSet(StringComparer.Ordinal);
                foreach (var target in chunk)
                    if (referencedKeys.Contains(target.StorageKey))
                        referencedTargetIds.Add(target.Id);
            }
        }

        return referencedTargetIds;
    }
}

public class FileRepository : FileRepository<PlanarianDbContext>
{
    public FileRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }
}
