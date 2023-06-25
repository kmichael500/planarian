using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
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
        return await DbContext.Files.Where(e=>e.BlobKey == blobKey && e.Cave.AccountId == RequestUser.AccountId).FirstOrDefaultAsync();
    }
}