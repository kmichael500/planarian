using Microsoft.EntityFrameworkCore;
using Planarian.Library.Constants;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;

namespace Planarian.Model.Shared;

public class RequestUser
{
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

    public async Task Initialize(string? accountId, string? userId)
    {
        var user = await _dbContext.Users.Include(e => e.AccountUsers).FirstOrDefaultAsync(e => e.Id == userId);
        if (user == null)
        {
            IsAuthenticated = false;
            return;
        }

        var isValidAccountId = user.AccountUsers.Select(e => e.AccountId).Contains(accountId);

        if (!isValidAccountId && !string.IsNullOrWhiteSpace(accountId))
        {
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
    }

    public string AccountContainerName =>
        $"account-{AccountId?.ToLower() ?? throw new NullReferenceException($" {nameof(AccountId)} is null")}";

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
                && e.AccountId == AccountId || e.AccountId == null
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
                        string.IsNullOrWhiteSpace(e.CaveId) 
                        && string.IsNullOrWhiteSpace(e.CountyId)
                        && e.UserId == Id
                        && e.AccountId == AccountId
                        && e.Permission!.Key == permissionKey
                    );            }
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
            throw ApiExceptionDictionary.Unauthorized("You don't have permission to perform this action");
        }
        
        return hasCavePermission;
    }

    public async Task<bool> HasCavePermission(string permissionKey, string? caveId, string? countyId, bool @throw = true)
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(permissionKey) || string.IsNullOrWhiteSpace(AccountId))
            throw ApiExceptionDictionary.BadRequest("Invalid request. IsAuthenticated, permissionKey, and AccountId are required");

        var hasPermission = false;
        if (string.IsNullOrWhiteSpace(caveId) && string.IsNullOrWhiteSpace(countyId))
        {
            hasPermission = await _dbContext.CavePermissions
                .AnyAsync(e =>
                    string.IsNullOrWhiteSpace(e.CaveId) 
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
                    && (e.CaveId == caveId || e.CountyId == countyId)
                );
        }
        

        if (!hasPermission && @throw)
        {
            throw ApiExceptionDictionary.Unauthorized("You don't have permission to perform this action");
        }

        return hasPermission;
    }
    
}