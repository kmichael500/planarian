using LinqToDB;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.Account.Repositories;

public class PermissionRepository : RepositoryBase
{
    public PermissionRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<Permission?> GetPermissionByKey(string permissionKey)
    {
        return await DbContext.Permissions.FirstOrDefaultAsync(e => e.Key.Equals(permissionKey));
    }
}