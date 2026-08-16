using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Services;
using Planarian.Shared.Base;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Modules.Files.Repositories;

public class FileRepository<TDbContext> : RepositoryBase<TDbContext> where TDbContext : PlanarianDbContextBase
{
    public FileRepository(TDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<File?> GetCaveFileByBlobKey(string blobKey)
    {
        return await DbContext.Files.Where(e => e.BlobKey == blobKey && e.Cave!.AccountId == RequestUser.AccountId)
            .FirstOrDefaultAsync();
    }

    public async Task<File?> GetFileById(string id)
    {
        return await DbContext.Files.Where(e => e.Id == id && e.AccountId == RequestUser.AccountId)
            .FirstOrDefaultAsync();
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

    public sealed record FileMetadataMutationContextResult(
        string Id,
        string FileTypeTagId,
        string? CaveId,
        string FileName,
        bool IsChangeRequestStaged,
        string? CountyId,
        string? StateId);

    public async Task<FileMetadataMutationContextResult?> GetFileMetadataMutationContextAsync(
        string id, CancellationToken cancellationToken)
    {
        return await DbContext.Files.AsNoTracking()
            .Where(file => file.Id == id && file.AccountId == RequestUser.AccountId)
            .Select(file => new FileMetadataMutationContextResult(
                file.Id,
                file.FileTypeTagId,
                file.CaveId,
                file.FileName,
                DbContext.CaveChangeRequestStagedFiles.Any(staged =>
                    staged.AccountId == RequestUser.AccountId && staged.FileId == file.Id),
                file.Cave != null ? file.Cave.CountyId : null,
                file.Cave != null ? file.Cave.StateId : null))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<File>> GetTrackedFilesByIdsAsync(IEnumerable<string> fileIds,
        CancellationToken cancellationToken)
    {
        var ids = fileIds.Distinct(StringComparer.Ordinal).ToList();
        return await DbContext.Files
            .Where(file => ids.Contains(file.Id) && file.AccountId == RequestUser.AccountId)
            .ToListAsync(cancellationToken);
    }

    public async Task<FileAccessInfoResult?> GetAuthoringStagedFileAccessInfoAsync(string id,
        CancellationToken cancellationToken)
    {
        return await DbContext.Files.AsNoTracking()
            .Where(file => file.Id == id && file.AccountId == RequestUser.AccountId &&
                           file.CreatedByUserId == RequestUser.Id && file.CaveId == null && file.ExpiresOn != null)
            .Select(file => new FileAccessInfoResult(
                file.Id, file.FileName, file.BlobKey, file.BlobContainer, file.CaveId, null, null))
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
                           file.BlobKey != null && file.BlobContainer != null)
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
        string FileName,
        string? BlobKey,
        string? ContainerName,
        string? CaveId,
        string? CountyId,
        string? StateId
        );

    public async Task<FileAccessInfoResult?> GetFileAccessInfo(string id, bool checkRequestUserAccountId = true)
    {
        return await DbContext.Files
            .Where(e => e.Id == id && (!checkRequestUserAccountId || e.AccountId == RequestUser.AccountId))
            .Select(e => new FileAccessInfoResult(
                e.Id,
                e.FileName,
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

    public async Task<IEnumerable<GetFileBlobPropertiesResult>> GetAllCavesBlobProperties()
    {
        return await DbContext.Files
            .Where(e => e.AccountId == RequestUser.AccountId && !string.IsNullOrWhiteSpace(e.CaveId))
            .Select(e => new GetFileBlobPropertiesResult(e.BlobKey, e.BlobContainer)).ToListAsync();
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
            DisplayName = e.DisplayName,
            FileName = e.FileName,
            FileTypeKey = e.FileTypeTag.Key,
            FileTypeTagId = e.FileTypeTagId
        });
    }

    public async Task<IEnumerable<string>> GetAllExistingFileIdsByCaveId(string entityId)
    {
        return await DbContext.Files.Where(e => e.CaveId == entityId && e.AccountId == RequestUser.AccountId)
            .Select(e => e.Id).ToListAsync();
    }

    public async Task<IEnumerable<File>> GetExpiredFiles()
    {
        return await DbContext.Files.Where(e => e.ExpiresOn < DateTime.UtcNow &&
                                                e.AccountId == RequestUser.AccountId &&
                                                !DbContext.CaveChangeRequestStagedFiles.Any(staged =>
                                                    staged.AccountId == e.AccountId && staged.FileId == e.Id))
            .ToListAsync();
    }

    public async Task<bool> IsDuplicateFile(string caveId, string fileName)
    {
        return await DbContext.Files.AnyAsync(e =>
            e.CaveId == caveId && e.FileName == fileName && e.AccountId == RequestUser.AccountId);
    }
}

public class FileRepository : FileRepository<PlanarianDbContext>
{
    public FileRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }
}
