using LinqToDB;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Model;
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

    public async Task<IEnumerable<SelectListItemDescriptionData<string, PermissionSelectListData>>>
        GetPermissionSelectList(string permissionType, IEnumerable<string>? allowedPermissionKeys = null)
    {
        var allowedKeys = allowedPermissionKeys?.ToList();

        var query = DbContext.Permissions
            .Where(e => !e.IsHidden && e.PermissionType == permissionType);

        if (allowedKeys is { Count: > 0 })
        {
            query = query.Where(e => allowedKeys.Contains(e.Key));
        }

        return await query
            .Select(e => new SelectListItemDescriptionData<string, PermissionSelectListData>(
                e.Name,
                e.Id,
                e.Description,
                new PermissionSelectListData
                {
                    Name = e.Name,
                    Description = e.Description,
                    Key = e.Key,
                    SortOrder = e.SortOrder
                }))
            .ToListAsync();
    }

}
