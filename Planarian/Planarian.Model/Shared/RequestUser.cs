using Microsoft.EntityFrameworkCore;
using Planarian.Library.Constants;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;

namespace Planarian.Model.Shared;

public class RequestUser
{
    private static readonly TimeSpan LastActiveOnUpdateInterval = TimeSpan.FromMinutes(10);
    private readonly PlanarianDbContext _dbContext;

    public RequestUser(PlanarianDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string Id { get; set; } = null!;
    public string? AccountId { get; set; }
    public string? UserGroupPrefix { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string FullName => $"{FirstName} {LastName}";
    public bool IsAuthenticated { get; private set; }

    public async Task Initialize(string? accountId, string? userId, bool throwOnInvalidAccountId = true)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(e => e.Id == userId)
            .Select(e => new
            {
                e.Id,
                e.FirstName,
                e.LastName,
                e.LastActiveOn,
                IsValidAccountId = e.AccountUsers.Any(au => au.AccountId == accountId)
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            IsAuthenticated = false;
            return;
        }

        var isValidAccountId = user.IsValidAccountId;

        if (!isValidAccountId && !string.IsNullOrWhiteSpace(accountId) && throwOnInvalidAccountId)
        {
            // Intentionally return 401 here so the client clears stale account selection state
            // and forces a fresh login/session bootstrap instead of continuing with a bad account.
            throw ApiExceptionDictionary.Unauthorized("the accountId doesn't exist or is invalid for this user");
        }

        Id = user.Id;
        FirstName = user.FirstName;
        LastName = user.LastName;
        IsAuthenticated = true;


        if (isValidAccountId)
        {
            AccountId = accountId;
            UserGroupPrefix = $"{userId}-{AccountId}";
        }

        var utcNow = DateTime.UtcNow;
        var lastActiveThreshold = utcNow.Subtract(LastActiveOnUpdateInterval);

        if (user.LastActiveOn == null || user.LastActiveOn < lastActiveThreshold)
        {
            await _dbContext.Users
                .Where(e => e.Id == user.Id && (e.LastActiveOn == null || e.LastActiveOn < lastActiveThreshold))
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(e => e.LastActiveOn, utcNow));
        }


    }

    public string AccountUploadFileChunkName =>
        $"account-{AccountId?.ToLower() ?? throw new NullReferenceException($" {nameof(AccountId)} is null")}";
    
    public string AccountContainerName => AccountUploadFileChunkName;

    /// <summary>
    /// Determines if the user has a specific permission, but not if they have permission for a specific cave, county, or all
    /// </summary>
    /// <param name="permissionKey"></param>
    /// <param name="throw"></param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public async Task<bool> HasCavePermission(string permissionKey, bool @throw = true)
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(permissionKey) || string.IsNullOrWhiteSpace(AccountId))
            return false;

        var userPermissions = await _dbContext.UserPermissions
            .Where(e =>
                e.UserId == Id
                && (e.AccountId == AccountId || e.AccountId == null)
            )
            .Select(e => e.Permission!.Key).ToListAsync();

        var hasUserPermission = permissionKey switch
        {
            PermissionKey.View => userPermissions.Contains(PermissionPolicyKey.View) ||
                                  userPermissions.Contains(PermissionPolicyKey.Manager) ||
                                  userPermissions.Contains(PermissionPolicyKey.Admin) ||
                                  userPermissions.Contains(PermissionPolicyKey.PlanarianAdmin),
            PermissionKey.Manager => userPermissions.Contains(PermissionPolicyKey.Manager) ||
                                     userPermissions.Contains(PermissionPolicyKey.Admin) ||
                                     userPermissions.Contains(PermissionPolicyKey.PlanarianAdmin),
            PermissionKey.Admin => userPermissions.Contains(PermissionPolicyKey.Admin) ||
                                   userPermissions.Contains(PermissionPolicyKey.PlanarianAdmin),
            PermissionKey.PlanarianAdmin => userPermissions.Contains(PermissionPolicyKey.PlanarianAdmin),
            _ => false
        };

        if (permissionKey == PermissionPolicyKey.AdminManager)
        {
            hasUserPermission = userPermissions.Contains(PermissionPolicyKey.Admin) ||
                                userPermissions.Contains(PermissionPolicyKey.PlanarianAdmin);


            if (!hasUserPermission)
            {
                hasUserPermission = await _dbContext.CavePermissions
                    .AnyAsync(e =>
                        string.IsNullOrWhiteSpace(e.StateId)
                        && string.IsNullOrWhiteSpace(e.CaveId)
                        && string.IsNullOrWhiteSpace(e.CountyId)
                        && e.UserId == Id
                        && e.AccountId == AccountId
                        && e.Permission!.Key == permissionKey
                    );
            }
        }

        if (permissionKey == PermissionPolicyKey.Export)
        {
            hasUserPermission = _dbContext.Accounts.Where(e => e.Id == AccountId).Select(e => e.ExportEnabled)
                .FirstOrDefault();

            if (!hasUserPermission)
            {
                hasUserPermission = userPermissions.Contains(PermissionPolicyKey.Admin) ||
                                    userPermissions.Contains(PermissionPolicyKey.PlanarianAdmin);
            }
        }

        if (hasUserPermission)
            return true;

        var hasCavePermission = await _dbContext.UserCavePermissionView
            .AnyAsync(e =>
                e.AccountId == AccountId
                && e.UserId == Id
                && permissionKey == e.Permission!.Key
            );

        if (!hasCavePermission && @throw)
        {
            throw ApiExceptionDictionary.Forbidden("You don't have permission to perform this action");
        }

        return hasCavePermission;
    }

    public async Task<bool> HasCavePermission(string permissionKey, string? caveId, string? countyId, string? stateId,
        bool @throw = true)
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(permissionKey) || string.IsNullOrWhiteSpace(AccountId))
            throw ApiExceptionDictionary.BadRequest("Invalid request. IsAuthenticated, permissionKey, and AccountId are required");

        var hasPermission = false;
        if (string.IsNullOrWhiteSpace(caveId) && string.IsNullOrWhiteSpace(countyId) &&
            string.IsNullOrWhiteSpace(stateId))
        {
            hasPermission = await _dbContext.CavePermissions
                .AnyAsync(e =>
                    string.IsNullOrWhiteSpace(e.StateId)
                    && string.IsNullOrWhiteSpace(e.CaveId)
                    && string.IsNullOrWhiteSpace(e.CountyId)
                    && e.UserId == Id
                    && e.AccountId == AccountId
                    && e.Permission!.Key == permissionKey
                );
            if (!hasPermission)
            {
                hasPermission = await HasCavePermission(PermissionPolicyKey.Admin);
            }
        }
        else
        {
            hasPermission = await _dbContext.UserCavePermissionView
                .AnyAsync(e =>
                    e.UserId == Id
                    && e.AccountId == AccountId
                    && e.PermissionKey == permissionKey
                    && ((!string.IsNullOrWhiteSpace(caveId) && e.CaveId == caveId) ||
                        (!string.IsNullOrWhiteSpace(countyId) && e.CountyId == countyId) ||
                        (!string.IsNullOrWhiteSpace(stateId) && e.StateId == stateId))
                );
        }


        if (!hasPermission && @throw)
        {
            throw ApiExceptionDictionary.Forbidden("You don't have permission to perform this action");
        }

        return hasPermission;
    }

}
