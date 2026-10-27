using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Tags.Repositories;

public class TagRepository<TDbContext> : RepositoryBase<TDbContext> where TDbContext : PlanarianDbContextBase
{
    public TagRepository(TDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<TagType?> GetFileTypeTagByName(string fileTagTypeName, string? accountId = null)
    {
        var result = await GetTagTypesQuery(TagTypeKeyConstant.File)
            .Where(e =>e.Name == fileTagTypeName)
            .FirstOrDefaultAsync();

        return result;
    }

    private IQueryable<TagType> GetTagTypesQuery(string key)
    {
        return DbContext.TagTypes
            .Where(e => e.Key == key &&
                        (e.AccountId == RequestUser.AccountId || e.IsDefault));
    }
}

public class TagRepository : TagRepository<PlanarianDbContext>
{
    public TagRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }
}
