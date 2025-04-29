using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Account.Repositories;

public class CaveChangeRequestRepository : RepositoryBase
{
    public CaveChangeRequestRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext,
        requestUser)
    {
    }

    public async Task<CaveChangeRequest?> GetCaveChangeRequest(string id, CancellationToken cancellationToken)
    {
        return await DbContext.CaveChangeRequests
            .Include(e => e.CaveChangeHistory)
            .FirstOrDefaultAsync(e => e.Id == id && e.AccountId == RequestUser.AccountId,
                cancellationToken);
    }

    public async Task<IEnumerable<ChangesForReviewVm>> GetChangesForReview()
    {
        return await DbContext.CaveChangeRequests
            .Where(e => e.AccountId == RequestUser.AccountId && e.ReviewedOn == null)
            .Select(e => new ChangesForReviewVm
            {
                Id = e.Id,
                CaveName = !string.IsNullOrWhiteSpace(e.CaveId)
                    ? e.Cave.Name
                    : e.CaveChangeHistory.OrderByDescending(history => history.CreatedOn)
                        .First(history => history.PropertyName == CaveLogPropertyNames.Name).ValueString!,
                IsNew = string.IsNullOrWhiteSpace(e.CaveId),
                SubmittedOn = e.CreatedOn,
                SubmittedByUserId = e.CreatedByUserId!,
                CaveDisplayId = !string.IsNullOrWhiteSpace(e.CaveId)
                    ? e.Cave.County.DisplayId + e.Account.CountyIdDelimiter + e.Cave.CountyNumber
                    : null,
                CountyId = !string.IsNullOrWhiteSpace(e.CaveId)
                    ? e.Cave.CountyId
                    : e.CaveChangeHistory.OrderByDescending(history => history.CreatedOn)
                        .First(history => history.PropertyName == CaveLogPropertyNames.CountyName)
                        .PropertyId,
            })
            .ToListAsync();
    }
}