using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.EntityFrameworkCore.Storage;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Users.Models;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Email.Services;
using PermissionKey = Planarian.Model.Database.Entities.RidgeWalker.PermissionKey;

namespace Planarian.Modules.Account.Services;

public class AccountUserManagerService : ServiceBase<UserRepository>
{
    private readonly EmailService _emailService;
    private readonly AccountRepository _accountRepository;
    private readonly PermissionRepository _permissionRepository;
    private readonly AccountAccessService _accountAccessService;
    private readonly ILogger<AccountUserManagerService> _logger;

    public AccountUserManagerService(
        UserRepository repository,
        RequestUser requestUser,
        EmailService emailService,
        AccountRepository accountRepository,
        PermissionRepository permissionRepository,
        AccountAccessService accountAccessService, ILogger<AccountUserManagerService> logger) : base(repository, requestUser)
    {
        _emailService = emailService;
        _accountRepository = accountRepository;
        _permissionRepository = permissionRepository;
        _accountAccessService = accountAccessService;
        _logger = logger;
    }

    #region User Manager

    public async Task<List<UserManagerGridVm>> GetAccountUsers()
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;


        return await Repository.GetAccountUsers(RequestUser.AccountId);
    }

    public async Task<List<InvitationEmailAttemptVm>> GetInvitationEmailHistory(string userId)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;

        var accountUser = await _accountRepository.GetAccountUser(userId, RequestUser.AccountId);
        if (accountUser == null) throw ApiExceptionDictionary.NotFound("User");

        return await Repository.GetInvitationEmailHistory(RequestUser.AccountId, userId);
    }

    public async Task<InviteUserResultVm> InviteUser(InviteUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;

        request.EmailAddress = request.EmailAddress?.Trim() ?? string.Empty;
        if (!request.EmailAddress.IsValidEmail())
        {
            throw ApiExceptionDictionary.BadRequest("Invalid email address.");
        }

        if (request.FirstName.IsNullOrWhiteSpace())
        {
            throw ApiExceptionDictionary.BadRequest("First name is required.");
        }

        if (request.LastName.IsNullOrWhiteSpace())
        {
            throw ApiExceptionDictionary.BadRequest("Last name is required.");
        }

        var user = new User(request.FirstName, request.LastName, request.EmailAddress) { IsTemporary = true };
        var accountUser = new AccountUser
        {
            User = user,
            AccountId = RequestUser.AccountId,
            InvitationCode = IdGenerator.Generate(PropertyLength.InvitationCode)
        };
        string? accountName;

        await using (var dbTransaction = await Repository.BeginTransactionAsync(cancellationToken))
        {
            try
            {
                // Serialize invitation creation for the same account and email.
                await Repository.LockInvitationEmail(RequestUser.AccountId, user.EmailAddress, cancellationToken);
                await _accountAccessService.EnsureCanInvite(user.EmailAddress);
                var pendingUserId = await Repository.GetPendingInvitationUserId(
                    RequestUser.AccountId, user.EmailAddress, cancellationToken);
                if (pendingUserId != null)
                {
                    await dbTransaction.CommitAsync(cancellationToken);
                    return new InviteUserResultVm { UserId = pendingUserId, AlreadyInvited = true };
                }

                Repository.Add(user);
                Repository.Add(accountUser);
                await Repository.SaveChangesAsync(cancellationToken);

                var defaultAccessViewAll = await _accountRepository.GetDefaultViewAccess();
                if (defaultAccessViewAll)
                {
                    await UpdateCavePermissions(user.Id, PermissionKey.View, new CreateUserCavePermissionsVm
                    {
                        HasAllLocations = true
                    }, cancellationToken, dbTransaction: dbTransaction);
                }

                accountName = await _accountRepository.GetAccountName(RequestUser.AccountId);
                await dbTransaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        var sendResult = await _emailService.SendAccountInvitationEmail(
            user, accountUser, accountName, CancellationToken.None);
        if (sendResult.WasSubmitted)
        {
            try
            {
                await Repository.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                // The invitation itself is already committed and Mailgun accepted the message.
                // Do not turn a successful create into a retryable API failure just because
                // the convenience InvitationSentOn timestamp could not be persisted.
                _logger.LogError(exception,
                    "Unable to persist InvitationSentOn after invitation {UserId} was submitted to the email provider.",
                    user.Id);
            }
        }

        return new InviteUserResultVm
        {
            UserId = user.Id,
            InvitationEmailDeliveryStatus = sendResult.DeliveryStatus
        };
    }

    public async Task<DisableAccessResultVm> DisableAccess(string userId, string? disableMessage = null,
        CancellationToken cancellationToken = default, bool sendEmail = true)
    {
        disableMessage = sendEmail ? disableMessage?.Trim() : null;
        var accountId = RequestUser.AccountId;
        var shouldNotify = await _accountAccessService.DisableAccess(
            userId, cancellationToken, disableMessage, sendEmail);
        if (!shouldNotify || !sendEmail)
            return new DisableAccessResultVm();

        try
        {
            var user = await Repository.Get(userId);
            var accountName = await _accountRepository.GetAccountName(accountId!);
            if (user == null)
                throw new InvalidOperationException("Disabled user could not be found for notification.");

            var result = await _emailService.SendAccountAccessDisabledEmail(
                user, accountName, disableMessage, CancellationToken.None);
            return new DisableAccessResultVm { NotificationEmailDeliveryStatus = result.DeliveryStatus };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to send account-access disabling notification for {UserId}.",
                userId);
            return new DisableAccessResultVm { NotificationEmailDeliveryStatus = MessageDeliveryStatus.SendFailed };
        }
    }

    public Task RestoreAccess(string userId, CancellationToken cancellationToken = default) =>
        _accountAccessService.RestoreAccess(userId, cancellationToken);

    public async Task<RevokeMembershipResultVm> RevokeMembership(string userId,
        CancellationToken cancellationToken = default, string? notificationMessage = null, bool sendEmail = true)
    {
        notificationMessage = sendEmail ? notificationMessage?.Trim() : null;
        if (sendEmail && string.IsNullOrWhiteSpace(notificationMessage))
            throw ApiExceptionDictionary.BadRequest("A notification message is required when sending email.");
        if (notificationMessage?.Length > 1000)
            throw ApiExceptionDictionary.BadRequest("The notification message must be 1,000 characters or fewer.");

        await _accountAccessService.RevokeMembership(userId, cancellationToken);
        if (!sendEmail)
            return new RevokeMembershipResultVm();

        try
        {
            var recipient = await Repository.Get(userId);
            if (recipient?.IsTemporary != false)
                return new RevokeMembershipResultVm();

            var accountName = await _accountRepository.GetAccountName(RequestUser.AccountId!);
            var result = await _emailService.SendAccountMembershipRevokedEmail(
                recipient, accountName, notificationMessage, CancellationToken.None);
            return new RevokeMembershipResultVm { NotificationEmailDeliveryStatus = result.DeliveryStatus };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to send membership revocation notification for {UserId}.",
                userId);
            return new RevokeMembershipResultVm
            {
                NotificationEmailDeliveryStatus = MessageDeliveryStatus.SendFailed
            };
        }
    }

    public async Task ResendInvitation(string userId)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
        {
            throw ApiExceptionDictionary.NoAccount;
        }

        var accountUser = await _accountRepository.GetAccountUser(userId, RequestUser.AccountId);
        if (accountUser == null)
        {
            throw ApiExceptionDictionary.NotFound("User");
        }

        var user = await Repository.Get(userId);
        if (user == null)
        {
            throw ApiExceptionDictionary.NotFound("User");
        }

        if (!user.IsTemporary || accountUser.AccessDisabledOn.HasValue ||
            accountUser.InvitationAcceptedOn.HasValue || string.IsNullOrWhiteSpace(accountUser.InvitationCode))
        {
            throw ApiExceptionDictionary.BadRequest("The user does not have an active invitation.");
        }

        var accountName = await _accountRepository.GetAccountName(RequestUser.AccountId);

        var sendResult = await _emailService.SendAccountInvitationEmail(user, accountUser, accountName);
        if (!sendResult.WasSubmitted) throw ApiExceptionDictionary.EmailFailedToSend;

        await Repository.SaveChangesAsync();
    }

    #endregion

    #region Permissions

    public async Task<CavePermissionManagementVm> GetcavePermissions(string userId, string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
        {
            throw ApiExceptionDictionary.NoAccount;
        }

        var accountUser = await _accountRepository.GetAccountUser(userId, RequestUser.AccountId);
        if (accountUser == null)
        {
            throw ApiExceptionDictionary.NotFound("User");
        }

        var cavePermissions = await Repository.GetCavePermissionsVm(userId, RequestUser.AccountId, permissionKey);


        return cavePermissions;
    }

    public async Task UpdateCavePermissions(string userId, string permissionKey, CreateUserCavePermissionsVm vm,
        CancellationToken cancellationToken, IDbContextTransaction? dbTransaction = null)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
        {
            throw ApiExceptionDictionary.NoAccount;
        }

        if (RequestUser.Id.Equals(userId))
        {
            throw ApiExceptionDictionary.BadRequest("You cannot change your own permissions.");
        }

        var accountUser = await _accountRepository.GetAccountUser(userId, RequestUser.AccountId);
        if (accountUser == null)
        {
            throw ApiExceptionDictionary.NotFound("User");
        }

        var permission = await _permissionRepository.GetPermissionByKey(permissionKey);
        if (permission == null)
        {
            throw ApiExceptionDictionary.NotFound("Permission");
        }

        if (permission.PermissionType != PermissionType.Cave)
        {
            throw ApiExceptionDictionary.BadRequest("Invalid permission type.");
        }

        // Retrieve existing permissions for this user and permission key
        var existingPermissions =
            (await Repository.GetCavePermissions(userId, RequestUser.AccountId, permissionKey)).ToList();

        var isExistingTransaction = dbTransaction != null;
        dbTransaction ??= await Repository.BeginTransactionAsync(cancellationToken);

        try
        {
            // Handle the "all locations" case
            if (vm.HasAllLocations)
            {
                // Look for an existing "all locations" record (all location scopes null)
                var allLocationPermission = existingPermissions.FirstOrDefault(p =>
                    string.IsNullOrWhiteSpace(p.StateId) && string.IsNullOrWhiteSpace(p.CountyId) &&
                    string.IsNullOrWhiteSpace(p.CaveId));
                if (allLocationPermission == null)
                {
                    // No record exists, so create one
                    await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, null, null, null);
                    allLocationPermission = new CavePermission
                    {
                        UserId = userId,
                        AccountId = RequestUser.AccountId,
                        StateId = null,
                        CountyId = null,
                        CaveId = null,
                        Permission = permission
                    };
                    Repository.Add(allLocationPermission);

                    var others = existingPermissions.Where(p =>
                        !string.IsNullOrWhiteSpace(p.StateId) || !string.IsNullOrWhiteSpace(p.CountyId) ||
                        !string.IsNullOrWhiteSpace(p.CaveId));
                    Repository.DeleteRange(others);

                    await Repository.SaveChangesAsync(cancellationToken);

                    if (!isExistingTransaction)
                    {
                        await dbTransaction.CommitAsync(cancellationToken);
                    }

                    return;
                }
            }
            else
            {
                // Remove any "all locations" if it exists
                var allLocationPermission = existingPermissions.FirstOrDefault(p =>
                    string.IsNullOrWhiteSpace(p.StateId) && string.IsNullOrWhiteSpace(p.CountyId) &&
                    string.IsNullOrWhiteSpace(p.CaveId));

                if (allLocationPermission != null)
                {
                    await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, null, null, null);
                    Repository.Delete(allLocationPermission);
                }

            }

            // --- Process state-level permissions ---
            var desiredStateIds = vm.StateIds?.Distinct().ToList() ?? new List<string>();
            var existingStatePermissions = existingPermissions
                .Where(p => !string.IsNullOrWhiteSpace(p.StateId) &&
                            string.IsNullOrWhiteSpace(p.CountyId) &&
                            string.IsNullOrWhiteSpace(p.CaveId)).ToList();

            var existingStateIds = existingStatePermissions.Select(p => p.StateId).ToList();

            var statesToRemove = existingStateIds.Except(desiredStateIds).ToList();
            var statesToAdd = desiredStateIds.Except(existingStateIds).ToList();

            foreach (var per in existingStatePermissions.Where(p => statesToRemove.Contains(p.StateId)))
            {
                await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, null, null, per.StateId);
                Repository.Delete(per);
            }

            foreach (var stateId in statesToAdd)
            {
                await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, null, null, stateId);
                var newStatePerm = new CavePermission
                {
                    UserId = userId,
                    AccountId = RequestUser.AccountId,
                    StateId = stateId,
                    CountyId = null,
                    CaveId = null,
                    Permission = permission
                };
                Repository.Add(newStatePerm);
            }

            // --- Process county-level permissions ---
            var desiredCountyIds = vm.CountyIds?.Distinct().ToList() ?? new List<string>();
            var existingCountyPermissions = existingPermissions
                .Where(p => string.IsNullOrWhiteSpace(p.StateId) &&
                            !string.IsNullOrWhiteSpace(p.CountyId) &&
                            string.IsNullOrWhiteSpace(p.CaveId)).ToList();

            var existingCountyIds = existingCountyPermissions.Select(p => p.CountyId).ToList();

            // Identify which county permissions need to be removed and which added
            var countiesToRemove = existingCountyIds.Except(desiredCountyIds).ToList();
            var countiesToAdd = desiredCountyIds.Except(existingCountyIds).ToList();

            // Remove county permissions that are no longer desired
            foreach (var per in existingCountyPermissions.Where(p => countiesToRemove.Contains(p.CountyId)))
            {
                await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, null, per.CountyId, per.StateId);
                Repository.Delete(per);
            }

            // Add new county permissions
            foreach (var countyId in countiesToAdd)
            {
                // Ensure the current user has the necessary rights for this county
                await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, null, countyId, null);
                var newCountyPerm = new CavePermission
                {
                    UserId = userId,
                    AccountId = RequestUser.AccountId,
                    StateId = null,
                    CountyId = countyId,
                    CaveId = null,
                    Permission = permission
                };
                Repository.Add(newCountyPerm);
            }

            // --- Process cave-level permissions ---
            var desiredCaveIds = vm.CaveIds.Distinct().ToList() ?? [];
            var existingCavePermissions =
                existingPermissions
                    .Where(p => string.IsNullOrWhiteSpace(p.StateId) &&
                                !string.IsNullOrWhiteSpace(p.CaveId) &&
                                string.IsNullOrWhiteSpace(p.CountyId)).ToList();
            var existingCaveIds = existingCavePermissions.Select(p => p.CaveId).ToList();

            // Identify which cave permissions need to be removed and which added
            var cavesToRemove = existingCaveIds.Except(desiredCaveIds).ToList();
            var cavesToAdd = desiredCaveIds.Except(existingCaveIds).ToList();

            // Remove cave permissions that are no longer desired
            foreach (var perm in existingCavePermissions.Where(p => cavesToRemove.Contains(p.CaveId)))
            {
                await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, perm.CaveId, null, null);
                Repository.Delete(perm);
            }

            // Add new cave permissions
            foreach (var caveId in cavesToAdd)
            {
                await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, caveId, null, null);
                var newCavePerm = new CavePermission
                {
                    UserId = userId,
                    AccountId = RequestUser.AccountId,
                    CaveId = caveId,
                    StateId = null,
                    CountyId = null,
                    Permission = permission
                };
                Repository.Add(newCavePerm);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            if (!isExistingTransaction)
            {
                await dbTransaction.CommitAsync(cancellationToken);
            }
        }
        catch (Exception)
        {
            if (!isExistingTransaction)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
    }


    #endregion

    public async Task<UserManagerGridVm> GetUserById(string userId)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
        {
            throw ApiExceptionDictionary.NoAccount;
        }

        var user = await Repository.GetUserById(userId, RequestUser.AccountId);
        if (user == null)
        {
            throw ApiExceptionDictionary.NotFound("User");
        }

        return user;
    }

    public async Task<IEnumerable<SelectListItemDescriptionData<string, PermissionSelectListData>>>
        GetPermissionSelectList(string permissionType)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
        {
            throw ApiExceptionDictionary.NoAccount;
        }

        IEnumerable<string>? allowedPermissionKeys = null;
        if (permissionType == PermissionType.User)
        {
            await RequestUser.HasCavePermission(PermissionPolicyKey.Admin);
        }
        else if (permissionType == PermissionType.Cave)
        {
            allowedPermissionKeys = [PermissionKey.View, PermissionKey.Manager];
        }

        var permissions = await _permissionRepository.GetPermissionSelectList(permissionType, allowedPermissionKeys);
        return permissions;
    }

    public async Task<IEnumerable<UserPermissionVm>> GetUserPermissions(string userId)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
        {
            throw ApiExceptionDictionary.NoAccount;
        }

        await RequestUser.HasCavePermission(PermissionPolicyKey.Admin);

        var accountUser = await _accountRepository.GetAccountUser(userId, RequestUser.AccountId);
        if (accountUser == null)
        {
            throw ApiExceptionDictionary.NotFound("User");
        }

        var permissions = await Repository.GetUserPermissions(userId);
        return permissions;
    }

    public async Task AddUserPermission(string userId, string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
        {
            throw ApiExceptionDictionary.NoAccount;
        }

        await RequestUser.HasCavePermission(PermissionPolicyKey.Admin);

        if (RequestUser.Id.Equals(userId))
        {
            throw ApiExceptionDictionary.BadRequest("You cannot change your own permissions.");
        }

        var accountUser = await _accountRepository.GetAccountUser(userId, RequestUser.AccountId);
        if (accountUser == null)
        {
            throw ApiExceptionDictionary.NotFound("User");
        }

        var userHasPermission = await Repository.GetUserPermission(userId, permissionKey);
        if (userHasPermission != null)
        {
            throw ApiExceptionDictionary.BadRequest("User already has this permission.");
        }

        var permission = await _permissionRepository.GetPermissionByKey(permissionKey);
        if (permission == null)
        {
            throw ApiExceptionDictionary.NotFound("Permission");
        }

        if (permission.Key == PermissionKey.PlanarianAdmin)
        {
            throw ApiExceptionDictionary.BadRequest("Planarian Admin cannot be assigned from account user management.");
        }

        if (permission.PermissionType != PermissionType.User)
        {
            throw ApiExceptionDictionary.BadRequest("Invalid permission type.");
        }

        var entity = new UserPermission
        {
            UserId = userId,
            AccountId = RequestUser.AccountId,
            Permission = permission
        };

        Repository.Add(entity);
        await Repository.SaveChangesAsync();
    }

    public Task RemoveUserPermission(string userId, string permissionKey) =>
        _accountAccessService.RemoveUserPermission(userId, permissionKey);

}
