using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.FeatureSettings.Repositories;

public class CaveChangeRequestRepository : RepositoryBase
{
    public CaveChangeRequestRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext,
        requestUser)
    {
    }

    public async Task<CaveChangeRequest?> GetCaveChangeRequest(string id, CancellationToken cancellationToken)
    {
        return await DbContext.CaveChangeRequests
            .FirstOrDefaultAsync(e => e.Id == id && e.AccountId == RequestUser.AccountId,
                cancellationToken);
    }
}