using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.Tags.Repositories;

public class TagRepository : RepositoryBase
{
    public TagRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<Tag?> GetTag(string tagId)
    {
        return await DbContext.Tags.FirstOrDefaultAsync(e => e.Id == tagId);
    }
}