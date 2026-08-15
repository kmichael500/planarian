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

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public async Task<User?> GetUserByEmail(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        return await DbContext.Users
            .Where(e => e.EmailAddress.ToLower() == normalizedEmail && !e.IsTemporary)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> UpgradePasswordHash(string userId, string expectedHash, string upgradedHash,
        CancellationToken cancellationToken = default)
    {
        var updated = await DbContext.Users
            .Where(e => e.Id == userId && e.HashedPassword == expectedHash && !e.IsTemporary)
            .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.HashedPassword, upgradedHash), cancellationToken);

        return updated == 1;
    }

    public async Task<User?> GetUserByConfirmationEmail(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        return await DbContext.Users
            .Where(e => !e.IsTemporary && e.EmailConfirmationCode != null &&
                        ((e.EmailConfirmedOn == null && e.EmailAddress.ToLower() == normalizedEmail) ||
                         (e.PendingEmailAddress != null && e.PendingEmailAddress.ToLower() == normalizedEmail)))
            .FirstOrDefaultAsync();
    }

    public async Task<bool> TrySetEmailConfirmationMessageLog(string userId, string expectedConfirmationCode,
        string? expectedMessageLogId, string messageLogId, CancellationToken cancellationToken = default)
    {
        var updated = await DbContext.Users
            .Where(e => e.Id == userId && !e.IsTemporary &&
                        (e.EmailConfirmedOn == null || e.PendingEmailAddress != null) &&
                        e.EmailConfirmationCode == expectedConfirmationCode &&
                        e.EmailConfirmationMessageLogId == expectedMessageLogId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.EmailConfirmationMessageLogId, messageLogId), cancellationToken);

        return updated > 0;
    }

    public async Task ReassignInvitationMessageLogs(string accountId, string fromUserId, string toUserId,
        CancellationToken cancellationToken = default)
    {
        await DbContext.MessageLogs
            .Where(e => e.AccountInvitationAccountId == accountId &&
                        e.AccountInvitationUserId == fromUserId &&
                        e.Purpose == MessagePurpose.AccountInvitation)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.AccountInvitationUserId, toUserId), cancellationToken);
    }

    public async Task<User?> Get(string id)
    {
        return await DbContext.Users.Where(e => e.Id == id && e.Id == id).FirstOrDefaultAsync();
    }

    public async Task<bool> EmailExists(string email, bool ignoreCurrentUser = false)
    {
        var normalizedEmail = NormalizeEmail(email);
        var query = DbContext.Users.Where(e => !e.IsTemporary && e.EmailAddress.ToLower() == normalizedEmail);
        if (ignoreCurrentUser) query = query.Where(e => e.Id != RequestUser.Id);

        return await query.AnyAsync();
    }

    public async Task<UserVm?> GetUserVm(string id)
    {
        return await DbContext.Users.Where(e => e.Id == id && RequestUser.Id == id)
            .Select(e => new UserVm(e))
            .FirstOrDefaultAsync();
    }

    public async Task<NameProfilePhotoVm?> GetUserDisplayInfo(string userId)
    {
        return await DbContext.AccountUsers
            .Where(e => e.AccountId == RequestUser.AccountId && e.UserId == userId && e.User != null)
            .Select(e => new NameProfilePhotoVm(e.User!.FullName, e.User.ProfilePhotoBlobKey))
            .FirstOrDefaultAsync();
    }

    public async Task<string?> GetUserProfilePhotoBlobKey(string userId)
    {
        return await DbContext.AccountUsers
            .Where(e => e.AccountId == RequestUser.AccountId && e.UserId == userId && e.User != null)
            .Select(e => e.User!.ProfilePhotoBlobKey)
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
                InvitationCode = e.InvitationCode!,
                FirstName = e.User!.FirstName,
                LastName = e.User.LastName,
                Email = e.User.EmailAddress,
                Regions = e.Account!.AccountStates.OrderByDescending(ee => ee.State.Name).Select(ee => ee.State.Name),
                AccountName = e.Account.Name,
                AccountId = e.Account.Id
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<AcceptInvitationVm>> GetPendingInvitationsForCurrentUser()
    {
        var email = await DbContext.Users
            .Where(e => e.Id == RequestUser.Id && !e.IsTemporary && e.EmailConfirmedOn != null)
            .Select(e => e.EmailAddress)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(email))
        {
            return new List<AcceptInvitationVm>();
        }

        var normalizedEmail = NormalizeEmail(email);
        return await DbContext.AccountUsers
            .Where(e =>
                !string.IsNullOrWhiteSpace(e.InvitationCode) &&
                e.InvitationAcceptedOn == null &&
                e.User != null &&
                e.User.IsTemporary &&
                e.User.EmailAddress.ToLower() == normalizedEmail)
            .OrderByDescending(e => e.InvitationSentOn)
            .Select(e => new AcceptInvitationVm
            {
                InvitationCode = e.InvitationCode!,
                FirstName = e.User!.FirstName,
                LastName = e.User.LastName,
                Email = e.User.EmailAddress,
                Regions = e.Account!.AccountStates.OrderByDescending(ee => ee.State.Name).Select(ee => ee.State.Name),
                AccountName = e.Account.Name,
                AccountId = e.Account.Id
            })
            .ToListAsync();
    }

    public async
        Task<(AccountUser? AccountUser, User? User, IEnumerable<CavePermission>? CavePermissions,
            IEnumerable<UserPermission>? UserPermissions)>
        GetInvitationEntities(string? invitationCode)
    {
        var accountUser = await DbContext.AccountUsers
            .Include(e => e.User)
            .ThenInclude(e => e!.UserPermissions)
            .Include(e => e.User)
            .ThenInclude(e => e!.CavePermissions)
            .Where(e => e.InvitationCode == invitationCode && e.User != null && e.User.IsTemporary)
            .FirstOrDefaultAsync();

        return (accountUser, accountUser?.User, accountUser?.User?.CavePermissions, accountUser?.User?.UserPermissions);
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

        var data = await permissions.Select(e => new
        {
            CaveName = e.Cave!.Name,
            CaveCountyId = e.Cave!.CountyId,
            e.CaveId,
            e.CountyId,
            StateId = e.StateId ?? e.County!.StateId
        }).ToListAsync();

        var hasAllLocations = data.Any(permission =>
            permission.StateId == null && permission.CountyId == null && permission.CaveId == null);

        var stateCountyValues = new StateCountyValue
        {
            States = data.Where(e => !string.IsNullOrWhiteSpace(e.StateId)).Select(permission => permission.StateId)
                .Distinct().ToList(),
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
                            await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, cave.CaveId, cave.CountyId,
                                cave.StateId, false)
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
            .Where(e => e.UserId == userId && (e.AccountId == accountId || string.IsNullOrWhiteSpace(e.AccountId)))
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
                && string.IsNullOrWhiteSpace(e.StateId)
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

        if (isAdminManger)
        {
            cavePermissions.Add(PermissionPolicyKey.AdminManager);
        }

        var exportEnabled = await RequestUser.HasCavePermission(PermissionPolicyKey.Export, false);
        if (exportEnabled)
        {
            cavePermissions.Add(PermissionPolicyKey.Export);
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
        return query.Select(e => new UserManagerGridVm
        {
            UserId = e.UserId,
            EmailAddress = e.User!.EmailAddress,
            FullName = e.User.FullName,
            InvitationSentOn = e.InvitationSentOn,
            InvitationAcceptedOn = e.InvitationAcceptedOn,
            LastActiveOn = e.User.LastActiveOn,
            HasActiveInvitation = e.InvitationAcceptedOn == null && e.InvitationCode != null,
            InvitationEmailAttemptCount = e.InvitationMessageLogs.Count
        });
    }

    public async Task<List<InvitationEmailAttemptVm>> GetInvitationEmailHistory(string accountId, string userId)
    {
        return await DbContext.MessageLogs
            .Where(e => e.AccountInvitationAccountId == accountId &&
                        e.AccountInvitationUserId == userId &&
                        e.Purpose == MessagePurpose.AccountInvitation)
            .OrderByDescending(e => e.CreatedOn)
            .ThenByDescending(e => e.Id)
            .Select(e => new InvitationEmailAttemptVm
            {
                MessageLogId = e.Id,
                CreatedOn = e.CreatedOn,
                DeliveryStatus = e.DeliveryStatus,
                DeliveryStatusOn = e.DeliveryStatusOn,
                Events = e.Events
                    .OrderBy(evt => evt.OccurredOn)
                    .ThenBy(evt => evt.Id)
                    .Select(evt => new InvitationEmailEventVm
                    {
                        EventType = evt.EventType,
                        OccurredOn = evt.OccurredOn,
                        Bot = evt.Bot,
                        Severity = evt.Severity,
                        Reason = evt.Reason,
                        DeliveryCode = evt.DeliveryCode,
                        EnhancedDeliveryCode = evt.EnhancedDeliveryCode,
                        AttemptNumber = evt.AttemptNumber,
                        IsDelayedBounce = evt.IsDelayedBounce
                    }).ToList()
            })
            .ToListAsync();
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
            .Where(e => e.UserId == userId && e.Permission!.Key == permissionKey &&
                        e.AccountId == RequestUser.AccountId)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> DeleteAccountUserAccess(string userId, string accountId,
        CancellationToken cancellationToken = default)
    {
        await DbContext.CavePermissions
            .Where(e => e.UserId == userId && e.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken);

        await DbContext.UserPermissions
            .Where(e => e.UserId == userId && e.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken);

        var deletedAccountUsers = await DbContext.AccountUsers
            .Where(e => e.UserId == userId && e.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken);

        return deletedAccountUsers > 0;
    }

    public async Task<DateTime?> GetLastActiveOn(string userId)
    {
        return await DbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.LastActiveOn)
            .FirstOrDefaultAsync();
    }

 }
