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
        return await DbContext.AccountUsers
            .Where(e => e.AccountId == accountId)
            .Select(e => new UserManagerGridVm(e.UserId, e.User.EmailAddress, e.User.FullName, e.InvitationSentOn,
                e.InvitationAcceptedOn))
            .ToListAsync();
    }

    public async Task<AcceptInvitationVm> GetInvitation(string code)
    {
        return await DbContext.AccountUsers
            .Where(e => e.InvitationCode == code && e.User != null && e.User.IsTemporary)
            .Select(e => new AcceptInvitationVm
            {
                FirstName = e.User.FirstName,
                LastName = e.User.LastName,
                Email = e.User.EmailAddress,
                Regions = e.Account.AccountStates.OrderByDescending(ee => ee.State.Name).Select(ee => ee.State.Name),
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

        var permissionsEntities = (await permissions
                .Include(e=>e.County)
                .Include(e=>e.Cave)
                .GroupBy(e => e.UserId)
                .ToListAsync());

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

        var cavePermissions = data.Where(e => !string.IsNullOrWhiteSpace(e.CaveId)).Select(e =>
            new SelectListItem<string, CavePermissionManagementData>
            {
                Display = e.CaveName,
                Value = e.CaveId!,
                Data = new CavePermissionManagementData
                {
                    CountyId = e.CaveCountyId!
                }
            }).ToList();
                    
        
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
            .Where(cp => cp.UserId == userId && cp.AccountId == accountId && cp.Permission.Key == permissionKey);
    }

    #endregion
}