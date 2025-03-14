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

    public async Task<bool> HasUserPermission(string permissionKey, string? caveId = null, string? countyId = null)
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(permissionKey) || string.IsNullOrWhiteSpace(AccountId))
            return false;

        var userPermissions = await _dbContext.UserPermissions.Where(e => e.UserId == Id && e.AccountId == AccountId)
            .Select(e => e.Permission!.Key).ToListAsync();

        return permissionKey switch
        {
            PermissionKey.PlanarianAdmin => userPermissions.Contains(PermissionKey.PlanarianAdmin),
            PermissionKey.Admin => userPermissions.Contains(PermissionKey.Admin) ||
                                   userPermissions.Contains(PermissionKey.PlanarianAdmin),
            _ => throw new ArgumentOutOfRangeException(nameof(permissionKey), permissionKey, null)
        };
    }
}