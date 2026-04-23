using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.PlanarianSettings.Repositories;

public class PlanarianSettingsRepository : RepositoryBase
{
    public PlanarianSettingsRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext,
        requestUser)
    {
    }

    public async Task<List<string>> GetExistingStateIds(IEnumerable<string> stateIds,
        CancellationToken cancellationToken)
    {
        return await DbContext.States
            .Where(e => stateIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Permission?> GetPermissionByKey(string permissionKey, CancellationToken cancellationToken)
    {
        return await DbContext.Permissions
            .FirstOrDefaultAsync(e => e.Key == permissionKey, cancellationToken);
    }
}
