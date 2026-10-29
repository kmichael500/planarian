using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Settings.Models;
using Planarian.Modules.Users.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Settings.Repositories;

public class SettingsRepository<TDbContext> : RepositoryBase<TDbContext> where TDbContext : PlanarianDbContextBase
{
    public SettingsRepository(TDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<string> GetTagTypeName(string tagTypeId)
    {
        return await DbContext.TagTypes
            .Where(e => e.Id == tagTypeId && (e.AccountId == RequestUser.AccountId || e.IsDefault)).Select(e => e.Name)
            .FirstAsync();
    }

    public async Task<NameProfilePhotoVm?> GetUserNameProfilePhoto(string userId)
    {
        return await DbContext.AccountUsers
            .Where(e => e.AccountId == RequestUser.AccountId && e.UserId == userId)
            .Select(e => new NameProfilePhotoVm(e.User!.FullName, e.User.ProfilePhotoBlobKey))
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetUsers()
    {
        return await DbContext.AccountUsers
            .Where(e => e.AccountId == RequestUser.AccountId && e.User != null && !e.User.IsTemporary)
            .OrderBy(e => e.User!.FirstName)
            .ThenBy(e => e.User!.LastName)
            .Select(e => new SelectListItem<string>(e.User!.FullName, e.User.Id))
            .ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetStates(string? permissionKey = null)
    {
        var accountStates = DbContext.AccountStates.Where(e => e.AccountId == RequestUser.AccountId);

        if (permissionKey == PermissionKey.Manager)
        {
            // Regional permission administration must be based on the scope that was actually
            // granted, not on a Cave row's descriptive StateId. Cave-only Managers use the
            // separate individual-Cave picker and must not be offered whole States here.
            accountStates = accountStates.Where(accountState =>
                DbContext.UserPermissions.Any(permission =>
                    permission.UserId == RequestUser.Id
                    && permission.AccountId == RequestUser.AccountId
                    && permission.Permission!.PermissionType == PermissionType.User
                    && permission.Permission.Key == PermissionKey.Admin)
                || DbContext.CavePermissions.Any(permission =>
                    permission.UserId == RequestUser.Id
                    && permission.AccountId == RequestUser.AccountId
                    && permission.Permission!.PermissionType == PermissionType.Cave
                    && permission.Permission.Key == PermissionKey.Manager
                    && permission.CaveId == null
                    && (
                        (permission.StateId == null && permission.CountyId == null)
                        || permission.StateId == accountState.StateId
                        || (permission.CountyId != null && permission.County!.StateId == accountState.StateId)
                    )));
        }

        return await accountStates
            .Select(accountState => new SelectListItem<string>(accountState.State!.Name, accountState.State.Id))
            .ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetStateCounties(string stateId,
        string? permissionKey = null)
    {
        var query =
            DbContext.UserCavePermissionView.Where(e =>
                e.AccountId == RequestUser.AccountId
                && e.UserId == RequestUser.Id
            );

        IQueryable<County> countyQuery;
        if (permissionKey == PermissionKey.Manager)
        {
            // Regional Manager choices come from the grants themselves so empty Counties
            // remain administrable. Cave-only Managers use the separate Cave picker.
            countyQuery = DbContext.Counties.Where(county =>
                county.AccountId == RequestUser.AccountId &&
                (DbContext.UserPermissions.Any(permission =>
                     permission.UserId == RequestUser.Id &&
                     permission.AccountId == RequestUser.AccountId &&
                     permission.Permission!.PermissionType == PermissionType.User &&
                     permission.Permission.Key == PermissionKey.Admin) ||
                 DbContext.CavePermissions.Any(permission =>
                     permission.UserId == RequestUser.Id &&
                     permission.AccountId == RequestUser.AccountId &&
                     permission.Permission!.PermissionType == PermissionType.Cave &&
                     permission.Permission.Key == PermissionKey.Manager &&
                     permission.CaveId == null &&
                     ((permission.StateId == null && permission.CountyId == null) ||
                      permission.CountyId == county.Id ||
                      (permission.CountyId == null && permission.StateId == county.StateId)))));
        }
        else if (permissionKey == PermissionKey.View)
        {
            countyQuery = query
                .Select(e => e.Cave!.County!);
        }
        else
        {
            countyQuery = DbContext.Counties.Where(e => e.AccountId == RequestUser.AccountId);
        }

        return await countyQuery
            .Where(e => e.StateId == stateId)
            .Distinct()
            .Select(e => new SelectListItem<string>(e.Name, e.Id))
            .ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetTags(string key, string? projectId = null)
    {
        return string.IsNullOrWhiteSpace(projectId)
            // Cave Tags
            ? await DbContext.TagTypes
                .Where(e => e.Key == key && (e.AccountId == RequestUser.AccountId || e.IsDefault))
                .Select(e => new SelectListItem<string>(e.Name, e.Id)).ToListAsync()
            // Project Tags
            : await DbContext.TagTypes
                .Where(e => e.Key == key && e.AccountId == null &&
                            e.ProjectId == projectId)
                .Select(e => new SelectListItem<string>(e.Name, e.Id)).ToListAsync();
    }

    public async Task<string?> GetCountyId(string countyId)
    {
        return await DbContext.Counties.Where(e => e.Id == countyId && e.AccountId == RequestUser.AccountId)
            .Select(e => e.Name).FirstOrDefaultAsync();
    }

    public async Task<CountyMetadataVm?> GetCountyMetadata(string countyId)
    {
        return await DbContext.Counties
            .Where(e => e.Id == countyId && e.AccountId == RequestUser.AccountId)
            .Select(e => new CountyMetadataVm
            {
                Id = e.Id,
                Name = e.Name,
                DisplayId = e.DisplayId,
                CountyIdDelimiter = e.Account!.CountyIdDelimiter
            })
            .FirstOrDefaultAsync();
    }

    public async Task<string?> GetStateName(string stateId)
    {
        return await DbContext.States.Where(e => e.Id == stateId).Select(e => e.Name).FirstOrDefaultAsync();
    }

}

public class SettingsRepository : SettingsRepository<PlanarianDbContext>
{
    public SettingsRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }
}
