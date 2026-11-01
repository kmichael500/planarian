using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Library.Exceptions;
using Planarian.Model.Shared.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;

namespace Planarian.Model.Shared;

public class RequestUser
{
    private static readonly TimeSpan LastActiveOnUpdateInterval = TimeSpan.FromMinutes(10);
    private readonly PlanarianDbContext _dbContext;

    public RequestUser(PlanarianDbContext dbContext)
    {
        if (dbContext.RequestUser is not null)
            throw new InvalidOperationException("A DbContext can only be bound to one RequestUser instance.");

        _dbContext = dbContext;
        dbContext.RequestUser = this;
    }

    public string Id { get; set; } = null!;
    public string? AccountId { get; set; }
    public string? UserGroupPrefix { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string FullName => $"{FirstName} {LastName}";
    public bool IsAuthenticated { get; private set; }

    public async Task Initialize(string? accountId, string? userId, int sessionVersion = 0,
        bool throwOnInvalidAccountId = true)
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
                e.SessionVersion,
                IsValidAccountId = e.AccountUsers.Any(au => au.AccountId == accountId)
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            IsAuthenticated = false;
            return;
        }

        if (user.SessionVersion != sessionVersion)
        {
            IsAuthenticated = false;
            return;
        }

        var isValidAccountId = user.IsValidAccountId;

        if (!isValidAccountId && !string.IsNullOrWhiteSpace(accountId) && throwOnInvalidAccountId)
        {
            // Return 401 so the client clears stale account selection and bootstraps a fresh session.
            throw RequestPolicyErrors.Unauthorized("the accountId doesn't exist or is invalid for this user");
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

    public static string BuildAccountContainerName(string accountId) =>
        $"account-{accountId.ToLowerInvariant()}";

    public string AccountUploadFileChunkName =>
        BuildAccountContainerName(AccountId ?? throw new NullReferenceException($" {nameof(AccountId)} is null"));

    public string AccountContainerName => AccountUploadFileChunkName;

    /// <summary>
    /// Determines whether the user has the effective capability anywhere in the current Account.
    /// Suitable for named policy/account-level checks; resource authorization uses the scoped overload.
    /// </summary>
    public async Task<bool> HasCavePermission(string permissionKey, bool @throw = true)
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(permissionKey))
            return false;

        if (permissionKey == PermissionPolicyKey.PlanarianAdmin)
        {
            return await _dbContext.UserPermissions.AnyAsync(e =>
                e.UserId == Id &&
                e.AccountId == null &&
                e.Permission!.PermissionType == PermissionType.User &&
                e.Permission.Key == PermissionPolicyKey.PlanarianAdmin);
        }

        if (string.IsNullOrWhiteSpace(AccountId))
            return false;

        var userPermissions = await _dbContext.UserPermissions
            .Where(e => e.UserId == Id
                        && e.AccountId == AccountId
                        && e.Permission!.PermissionType == PermissionType.User)
            .Select(e => e.Permission!.Key).ToListAsync();

        var satisfyingPermissionKeys = PermissionPolicy.GetSatisfyingUserPermissionKeys(permissionKey);
        var satisfyingCavePermissionKeys = PermissionPolicy.GetSatisfyingCavePermissionKeys(permissionKey);
        var hasUserPermission = userPermissions.Any(satisfyingPermissionKeys.Contains);

        if (permissionKey == PermissionPolicyKey.AdminManager)
        {
            var satisfyingManagerUserPermissionKeys =
                PermissionPolicy.GetSatisfyingUserPermissionKeys(PermissionPolicyKey.Manager);
            var satisfyingManagerCavePermissionKeys =
                PermissionPolicy.GetSatisfyingCavePermissionKeys(PermissionPolicyKey.Manager);
            hasUserPermission = userPermissions.Any(satisfyingManagerUserPermissionKeys.Contains);

            if (!hasUserPermission)
            {
                hasUserPermission = await _dbContext.CavePermissions
                    .AnyAsync(e =>
                        string.IsNullOrWhiteSpace(e.StateId)
                        && string.IsNullOrWhiteSpace(e.CaveId)
                        && string.IsNullOrWhiteSpace(e.CountyId)
                        && e.UserId == Id
                        && e.AccountId == AccountId
                        && e.Permission!.PermissionType == PermissionType.Cave
                        && satisfyingManagerCavePermissionKeys.Contains(e.Permission.Key)
                    );
            }
        }

        if (permissionKey == PermissionPolicyKey.Export)
        {
            hasUserPermission = await _dbContext.Accounts
                .Where(e => e.Id == AccountId)
                .Select(e => e.ExportEnabled)
                .FirstOrDefaultAsync();

            if (!hasUserPermission)
            {
                var satisfyingAdminUserPermissionKeys =
                    PermissionPolicy.GetSatisfyingUserPermissionKeys(PermissionPolicyKey.Admin);
                hasUserPermission = userPermissions.Any(satisfyingAdminUserPermissionKeys.Contains);
            }
        }

        if (hasUserPermission)
            return true;

        // Query grant rows directly so State/all-location capabilities survive empty scopes.
        var hasCavePermission = await _dbContext.CavePermissions
            .AnyAsync(e =>
                e.AccountId == AccountId
                && e.UserId == Id
                && e.Permission!.PermissionType == PermissionType.Cave
                && satisfyingCavePermissionKeys.Contains(e.Permission.Key)
            );

        if (!hasCavePermission && @throw)
        {
            throw RequestPolicyErrors.Forbidden("You don't have permission to perform this action");
        }

        return hasCavePermission;
    }

    /// <summary>
    /// Authorizes a concrete resource scope. When multiple identifiers are supplied, the most
    /// specific scope wins: Cave, then County, then State, then Account-wide. Broader grants may
    /// satisfy narrower checks, but narrower grants never broaden upward or sideways.
    /// </summary>
    public async Task<bool> HasCavePermission(string permissionKey, string? caveId, string? countyId, string? stateId,
        bool @throw = true)
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(permissionKey))
            throw RequestPolicyErrors.BadRequest("Invalid request. IsAuthenticated and permissionKey are required");

        // Platform and synthetic account capabilities are not resource grants. Route them
        // through the canonical capability resolver so malformed persisted rows cannot turn
        // PlanarianAdmin/AdminManager/Export into scoped UserPermission authorities.
        if (PermissionPolicy.BypassesResourceScopeResolution(permissionKey))
            return await HasCavePermission(permissionKey, @throw);

        if (string.IsNullOrWhiteSpace(AccountId))
            throw RequestPolicyErrors.BadRequest("Invalid request. AccountId is required for scoped Cave authorization");

        var satisfyingPermissionKeys = PermissionPolicy.GetSatisfyingUserPermissionKeys(permissionKey);
        var satisfyingCavePermissionKeys = PermissionPolicy.GetSatisfyingCavePermissionKeys(permissionKey);
        var hasPermission = await _dbContext.UserPermissions.AnyAsync(e =>
            e.UserId == Id
            && e.AccountId == AccountId
            && e.Permission!.PermissionType == PermissionType.User
            && satisfyingPermissionKeys.Contains(e.Permission.Key));

        if (!hasPermission && !string.IsNullOrWhiteSpace(caveId))
        {
            // The view expands County, State, and all-location CavePermission rows to each
            // affected Cave. Matching only CaveId therefore honors broader scopes without
            // allowing an unrelated Cave row's County/State metadata to broaden access.
            hasPermission = await _dbContext.UserCavePermissionView.AnyAsync(e =>
                e.UserId == Id
                && e.AccountId == AccountId
                && e.CaveId == caveId
                && satisfyingCavePermissionKeys.Contains(e.PermissionKey));
        }
        else if (!hasPermission && !string.IsNullOrWhiteSpace(countyId))
        {
            // County authority must come from the owning grant rows rather than the expanded
            // Cave view. An empty County has no Cave rows to expand, but a County/State/account
            // Manager still owns that resource. Derive State inheritance from the authoritative
            // County row so a client-supplied StateId can never broaden a mismatched County.
            hasPermission = await _dbContext.CavePermissions.AnyAsync(e =>
                e.UserId == Id
                && e.AccountId == AccountId
                && string.IsNullOrWhiteSpace(e.CaveId)
                && e.Permission!.PermissionType == PermissionType.Cave
                && satisfyingCavePermissionKeys.Contains(e.Permission.Key)
                && (e.CountyId == countyId
                    || (string.IsNullOrWhiteSpace(e.CountyId) && string.IsNullOrWhiteSpace(e.StateId))
                    || (string.IsNullOrWhiteSpace(e.CountyId) && !string.IsNullOrWhiteSpace(e.StateId)
                        && _dbContext.Counties.Any(county =>
                            county.AccountId == AccountId && county.Id == countyId && county.StateId == e.StateId))));
        }
        else if (!hasPermission && !string.IsNullOrWhiteSpace(stateId))
        {
            // StateId is descriptive metadata on Cave/County view rows, so querying the
            // view by StateId alone would incorrectly promote narrower Cave/County grants.
            hasPermission = await _dbContext.CavePermissions.AnyAsync(e =>
                e.UserId == Id
                && e.AccountId == AccountId
                && string.IsNullOrWhiteSpace(e.CaveId)
                && string.IsNullOrWhiteSpace(e.CountyId)
                && (e.StateId == stateId || string.IsNullOrWhiteSpace(e.StateId))
                && e.Permission!.PermissionType == PermissionType.Cave
                && satisfyingCavePermissionKeys.Contains(e.Permission.Key));
        }
        else if (!hasPermission)
        {
            hasPermission = await _dbContext.CavePermissions.AnyAsync(e =>
                e.UserId == Id
                && e.AccountId == AccountId
                && string.IsNullOrWhiteSpace(e.CaveId)
                && string.IsNullOrWhiteSpace(e.CountyId)
                && string.IsNullOrWhiteSpace(e.StateId)
                && e.Permission!.PermissionType == PermissionType.Cave
                && satisfyingCavePermissionKeys.Contains(e.Permission.Key));
        }

        if (!hasPermission && @throw)
        {
            throw RequestPolicyErrors.Forbidden("You don't have permission to perform this action");
        }

        return hasPermission;
    }

}
