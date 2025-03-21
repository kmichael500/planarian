using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Users.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Users.Repositories;

public class UserRepository : RepositoryBase
{
    public UserRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<User?> GetUserByEmail(string email)
    {
        return await DbContext.Users.Where(e => e.EmailAddress == email && !e.IsTemporary).FirstOrDefaultAsync();
    }

    public async Task<User?> Get(string id)
    {
        return await DbContext.Users.Where(e => e.Id == id && e.Id == id).FirstOrDefaultAsync();
    }

    public async Task<bool> EmailExists(string email, bool ignoreCurrentUser = false)
    {
        var query = DbContext.Users.Where(e => e.EmailAddress == email && !e.IsTemporary);
        if (ignoreCurrentUser) query = query.Where(e => e.Id != RequestUser.Id);

        return await query.AnyAsync();
    }

    public async Task<UserVm?> GetUserVm(string id)
    {
        return await DbContext.Users.Where(e => e.Id == id && RequestUser.Id == id)
            .Select(e => new UserVm(e))
            .FirstOrDefaultAsync();
    }

    public async Task<User?> GetUserByPasswordResetCode(string code)
    {
        return await DbContext.Users.Where(e => e.PasswordResetCode == code).FirstOrDefaultAsync();
    }

    public async Task<User?> GetUserByPasswordEmailConfirmationCode(string code)
    {
        return await DbContext.Users.FirstOrDefaultAsync(e => e.EmailConfirmationCode == code);
    }

    public async Task<List<UserManagerGridVm>> GetAccountUsers(string accountId)
    {
        return await ToUserGridVmQuery(DbContext.AccountUsers
                .Where(e => e.AccountId == accountId)
                .Select(e => e)
            )
            .ToListAsync();
    }

    public async Task<AcceptInvitationVm> GetInvitation(string code)
    {
        return await DbContext.AccountUsers
            .Where(e => e.InvitationCode == code && e.User != null && e.User.IsTemporary)
            .Select(e => new AcceptInvitationVm
            {
                FirstName = e.User!.FirstName,
                LastName = e.User.LastName,
                Email = e.User.EmailAddress,
                Regions = e.Account!.AccountStates.OrderByDescending(ee => ee.State.Name).Select(ee => ee.State.Name),
                AccountName = e.Account.Name,
                AccountId = e.Account.Id
            })
            .FirstOrDefaultAsync();
    }

    public async Task<(AccountUser? AccountUser, User? User)> GetInvitationEntities(string? invitationCode)
    {
        var accountUser = await DbContext.AccountUsers
            .Include(e => e.User)
            .Where(e => e.InvitationCode == invitationCode && e.User != null && e.User.IsTemporary)
            .FirstOrDefaultAsync();

        return (accountUser, accountUser?.User);
    }

    public async Task<bool> UserInAccount(string existingUserId, string accountId)
    {
        return await DbContext.AccountUsers
            .Where(e => e.UserId == existingUserId && e.AccountId == accountId)
            .AnyAsync();
    }

    #region Manage Permissions


    public async Task<CavePermissionManagementVm> GetCavePermissionsVm(string userId, string accountId,
        string permissionKey)
    {
        var permissions = GetCavePermissionQuery(userId, accountId, permissionKey);
        
        var data = await permissions.Select(e=> new
        {
            CaveName = e.Cave!.Name,
            CaveCountyId = e.Cave!.CountyId,
            e.CaveId,
            e.CountyId,
            StateId = e.County!.StateId
        }).ToListAsync();
        
        var hasAllLocations = data.Any(permission => permission.CountyId == null && permission.CaveId == null);

        var stateCountyValues = new StateCountyValue
        {
            States = data.Where(e=>!string.IsNullOrWhiteSpace(e.StateId)).Select(permission => permission.StateId).Distinct().ToList(),
            CountiesByState = 
                data.Where(e => !string.IsNullOrWhiteSpace(e.StateId) && !string.IsNullOrWhiteSpace(e.CountyId))
                .GroupBy(permission => permission.StateId)
                .ToDictionary(group => group.Key, e => e.Select(ee => ee.CountyId!).ToList())
        };

        var cavePermissions = new List<SelectListItem<string, CavePermissionManagementData>>();
        foreach (var cave in data.Where(e => !string.IsNullOrWhiteSpace(e.CaveId)))
        {
            cavePermissions.Add(
                new SelectListItem<string, CavePermissionManagementData>
                {
                    Display = cave.CaveName,
                    Value = cave.CaveId!,
                    Data = new CavePermissionManagementData
                    {
                        CountyId = cave.CaveCountyId,
                        RequestUserHasAccess =
                            await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, cave.CaveId, cave.CountyId, false)
                    }
                }
            );
        }

        var result = new CavePermissionManagementVm
        {
            HasAllLocations = hasAllLocations,
            StateCountyValues = stateCountyValues,
            CavePermissions = cavePermissions
        };

        return result;
    }

    public async Task<IEnumerable<CavePermission>> GetCavePermissions(string userId,
        string accountId, string permissionKey)
    {
        return await GetCavePermissionQuery(userId, accountId, permissionKey).ToListAsync();
    }

    private IQueryable<CavePermission> GetCavePermissionQuery(string userId, string accountId, string permissionKey)
    {
        return DbContext.CavePermissions
            .Where(e => e.UserId == userId && e.AccountId == accountId && e.Permission.Key == permissionKey &&
                        e.Permission.PermissionType == PermissionType.Cave);
    }
    
    public async Task<IEnumerable<string>> GetPermissions(string userId, string accountId)
    {
        var userPermissions = await DbContext.UserPermissions
            .Where(e => e.UserId == userId && e.AccountId == accountId)
            .Select(e => e.Permission!.Key)
            .ToListAsync();
        
        var cavePermissions = await DbContext.CavePermissions
            .Where(e => e.UserId == userId && e.AccountId == accountId)
            .Select(e => e.Permission!.Key)
            .Distinct()
            .ToListAsync();

        var isAdminManger = await DbContext.CavePermissions
            .Where(e =>
                e.UserId == userId
                && e.AccountId == accountId
                && string.IsNullOrWhiteSpace(e.CaveId)
                && string.IsNullOrWhiteSpace(e.CountyId)
                && e.Permission!.Key == PermissionPolicyKey.Manager
            )
            .AnyAsync();
        if (isAdminManger == false)
        {
            isAdminManger = await DbContext.UserPermissions
                .AnyAsync(e =>
                    e.UserId == userId
                    && e.AccountId == accountId
                    && (
                        PermissionKey.Admin == e.Permission!.Key
                        || PermissionKey.PlanarianAdmin == e.Permission!.Key
                    )
                );
        }        
        if(isAdminManger)
        {
            cavePermissions.Add(PermissionPolicyKey.AdminManager);
        }

        userPermissions.AddRange(cavePermissions);
        
        return userPermissions.Distinct();
    }

    #endregion

    public async Task<UserManagerGridVm?> GetUserById(string userId, string requestUserAccountId)
    {
        var user = await ToUserGridVmQuery(DbContext.AccountUsers
            .Where(e => e.UserId == userId && e.AccountId == requestUserAccountId)
            .Select(e => (e))
        ).FirstOrDefaultAsync();

        return user;
    }
    
    private static IQueryable<UserManagerGridVm> ToUserGridVmQuery(IQueryable<AccountUser> query)
    {
        return query.Select(e => new UserManagerGridVm(e.UserId, e.User!.EmailAddress, e.User.FullName, e.InvitationSentOn,
            e.InvitationAcceptedOn));
    }

    /// <summary>
    /// Return all Access Permissions the user currently has.
    /// </summary>
    public async Task<List<UserPermissionVm>> GetUserPermissions(
        string userId)
    {
        return await DbContext.UserPermissions
            .Where(e => e.UserId == userId && e.AccountId == RequestUser.AccountId &&
                        e.Permission!.PermissionType == PermissionType.User)
            .Select(e => new UserPermissionVm(e.Id, e.Permission!.Key, e.Permission.Name, e.Permission!.Description))
            .ToListAsync();
    }

    public async Task<UserPermission?> GetUserPermission(string userId, string permissionKey)
        {
            return await DbContext.UserPermissions
                .Where(e => e.UserId == userId && e.Permission!.Key == permissionKey && e.AccountId == RequestUser.AccountId)
                .FirstOrDefaultAsync();
        }
}