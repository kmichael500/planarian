using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.Files.Repositories;

/// <summary>
/// General persisted Cave File queries. Staged/authoring lifecycle operations belong
/// to AuthoringFileRepository so callers cannot accidentally mix published and staged rules.
/// </summary>
public class FileRepository<TDbContext> : RepositoryBase<TDbContext>
    where TDbContext : PlanarianDbContextBase
{
    public FileRepository(TDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public sealed record FileAccessInfoResult(
        string Id,
        string Name,
        string Extension,
        string? BlobKey,
        string? ContainerName,
        string? CaveId,
        string? CountyId,
        string? StateId);

    public Task<FileAccessInfoResult?> GetFileAccessInfo(string id)
    {
        return DbContext.Files
            .Where(file => file.Id == id && file.AccountId == RequestUser.AccountId)
            .Select(file => new FileAccessInfoResult(
                file.Id,
                file.Name,
                file.Extension,
                file.BlobKey,
                file.BlobContainer,
                file.CaveId,
                file.Cave != null ? file.Cave.CountyId : null,
                file.Cave != null ? file.Cave.StateId : null))
            .FirstOrDefaultAsync();
    }

    public Task<bool> IsDuplicateFile(string caveId, string name, string extension)
    {
        return DbContext.Files.AnyAsync(file =>
            file.CaveId == caveId &&
            file.Name == name &&
            file.Extension == extension &&
            file.AccountId == RequestUser.AccountId);
    }
}

public class FileRepository : FileRepository<PlanarianDbContext>
{
    public FileRepository(PlanarianDbContext dbContext, RequestUser requestUser)
        : base(dbContext, requestUser)
    {
    }
}
