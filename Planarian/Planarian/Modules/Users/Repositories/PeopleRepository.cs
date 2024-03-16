using LinqToDB;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.Users.Repositories;

public class PeopleRepository : RepositoryBase
{
    public PeopleRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<PeopleTag?> GetByTagTypeId(string? tagTypeId)
    {
        return await DbContext.PeopleTags.FirstOrDefaultAsync(e => e.TagTypeId == tagTypeId);
    }
}