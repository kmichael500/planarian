using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Services;
using Planarian.Shared.Base;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Modules.Files.Repositories;

public class FileRepository : RepositoryBase
{
    public FileRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<File?> GetCaveFileByBlobKey(string blobKey)
    {
        return await DbContext.Files.Where(e => e.BlobKey == blobKey && e.Cave.AccountId == RequestUser.AccountId)
            .FirstOrDefaultAsync();
    }

    public async Task<File?> GetFileById(string id)
    {
        return await DbContext.Files.Where(e => e.Id == id && e.AccountId == RequestUser.AccountId)
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
        return await DbContext.Files.Where(e => e.ExpiresOn < DateTime.UtcNow && e.AccountId == RequestUser.AccountId)
            .ToListAsync();
    }
}