using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Account.Repositories;

public class CaveChangeLogRepository : RepositoryBase
{
    public CaveChangeLogRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext,
        requestUser)
    {
    }

    public async Task<IEnumerable<CaveChangeLogVm>> GetCaveHistory(string caveId, CancellationToken cancellationToken)
    {
        var value = await DbContext.CaveChangeHistory
            .Where(e => e.CaveId == caveId && e.AccountId == RequestUser.AccountId)
            .Select(e=>new CaveChangeLogVm
            {
                CaveId = e.CaveId,
                EntranceId = e.EntranceId,
                ChangedByUserId = e.ChangedByUserId,
                ApprovedByUserId = e.ApprovedByUserId,
                PropertyName = e.PropertyName,
                ChangeType = e.ChangeType,
                ChangeValueType = e.ChangeValueType,
                ValueString = e.ValueString,
                ValueInt = e.ValueInt,
                ValueDouble = e.ValueDouble,
                ValueBool = e.ValueBool,
                ValueDateTime = e.ValueDateTime,
                CreatedOn = e.CreatedOn,
            })
            .OrderByDescending(e=>e.CreatedOn)
            .ToListAsync(cancellationToken);

        return value;
    }
}