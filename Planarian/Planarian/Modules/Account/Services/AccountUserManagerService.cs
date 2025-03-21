using System.Data;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.EntityFrameworkCore.Storage;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.String;
using Planarian.Library.Options;
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
    private readonly UserRepository _userRepository;

    public AccountUserManagerService(
        UserRepository repository,
        RequestUser requestUser,
        EmailService emailService,
        ServerOptions serverOptions, AccountRepository accountRepository,
        PermissionRepository permissionRepository, UserRepository userRepository) : base(repository, requestUser)
    {
        _emailService = emailService;
        _accountRepository = accountRepository;
        _permissionRepository = permissionRepository;
        _userRepository = userRepository;
    }

    #region User Manager

    public async Task<List<UserManagerGridVm>> GetAccountUsers()
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;


        return await Repository.GetAccountUsers(RequestUser.AccountId);
    }

    public async Task<string> InviteUser(InviteUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;

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

        var dbTransaction = await Repository.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = new User(request.FirstName, request.LastName, request.EmailAddress) { IsTemporary = true };
            Repository.Add(user);

            var invitationCode = IdGenerator.Generate(PropertyLength.InvitationCode);

            var accountUser = new AccountUser
            {
                User = user,
                AccountId = RequestUser.AccountId,
                InvitationCode = invitationCode,
                InvitationSentOn = DateTime.UtcNow,
            };

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

            var accountName = await _accountRepository.GetAccountName(RequestUser.AccountId);

            await _emailService.SendAccountInvitationEmail(user, accountUser,
                accountName);
            await dbTransaction.CommitAsync(cancellationToken);
            
            return user.Id;
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task RevokeAccess(string userId)
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

        Repository.Delete(accountUser);
        await Repository.SaveChangesAsync();
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

        if (accountUser.InvitationAcceptedOn.HasValue)
        {
            throw ApiExceptionDictionary.BadRequest("User has already accepted the invitation.");
        }

        if (!accountUser.InvitationSentOn.HasValue)
        {
            throw ApiExceptionDictionary.BadRequest("The user was not invited in the first place.");
        }

        var user = await Repository.Get(userId);
        if (user == null)
        {
            throw ApiExceptionDictionary.NotFound("User");
        }

        accountUser.InvitationSentOn = DateTime.UtcNow;
        await Repository.SaveChangesAsync();

        var accountName = await _accountRepository.GetAccountName(RequestUser.AccountId);

        await _emailService.SendAccountInvitationEmail(user, accountUser,
            accountName);
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
                // Look for an existing "all locations" record (both CountyId and CaveId null)
                var allLocationPermission = existingPermissions.FirstOrDefault(p =>
                    string.IsNullOrWhiteSpace(p.CountyId) && string.IsNullOrWhiteSpace(p.CaveId));
                if (allLocationPermission == null)
                {
                    // No record exists, so create one
                    await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, null, null);
                    allLocationPermission = new CavePermission
                    {
                        UserId = userId,
                        AccountId = RequestUser.AccountId,
                        CountyId = null,
                        CaveId = null,
                        Permission = permission
                    };
                    Repository.Add(allLocationPermission);

                    var others = existingPermissions.Where(p =>
                        !string.IsNullOrWhiteSpace(p.CountyId) || !string.IsNullOrWhiteSpace(p.CaveId));
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
                    string.IsNullOrWhiteSpace(p.CountyId) && string.IsNullOrWhiteSpace(p.CaveId));

                if (allLocationPermission != null)
                {
                    await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, null, null);
                    Repository.Delete(allLocationPermission);
                }

            }

            // --- Process county-level permissions ---
            var desiredCountyIds = vm.CountyIds?.Distinct().ToList() ?? new List<string>();
            var existingCountyPermissions = existingPermissions
                .Where(p => !string.IsNullOrWhiteSpace(p.CountyId) && string.IsNullOrWhiteSpace(p.CaveId)).ToList();

            var existingCountyIds = existingCountyPermissions.Select(p => p.CountyId).ToList();

            // Identify which county permissions need to be removed and which added
            var countiesToRemove = existingCountyIds.Except(desiredCountyIds).ToList();
            var countiesToAdd = desiredCountyIds.Except(existingCountyIds).ToList();

            // Remove county permissions that are no longer desired
            foreach (var per in existingCountyPermissions.Where(p => countiesToRemove.Contains(p.CountyId)))
            {
                await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, null, per.CountyId);
                Repository.Delete(per);
            }

            // Add new county permissions
            foreach (var countyId in countiesToAdd)
            {
                // Ensure the current user has the necessary rights for this county
                await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, null, countyId);
                var newCountyPerm = new CavePermission
                {
                    UserId = userId,
                    AccountId = RequestUser.AccountId,
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
                    .Where(p => !string.IsNullOrWhiteSpace(p.CaveId) && string.IsNullOrWhiteSpace(p.CountyId)).ToList();
            var existingCaveIds = existingCavePermissions.Select(p => p.CaveId).ToList();

            // Identify which cave permissions need to be removed and which added
            var cavesToRemove = existingCaveIds.Except(desiredCaveIds).ToList();
            var cavesToAdd = desiredCaveIds.Except(existingCaveIds).ToList();

            // Remove cave permissions that are no longer desired
            foreach (var perm in existingCavePermissions.Where(p => cavesToRemove.Contains(p.CaveId)))
            {
                await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, perm.CaveId, null);
                Repository.Delete(perm);
            }

            // Add new cave permissions
            foreach (var caveId in cavesToAdd)
            {
                await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, caveId, null);
                var newCavePerm = new CavePermission
                {
                    UserId = userId,
                    AccountId = RequestUser.AccountId,
                    CaveId = caveId,
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

        var permissions = await _permissionRepository.GetPermissionSelectList(permissionType);
        return permissions;
    }
    
      public async Task<IEnumerable<UserPermissionVm>> GetUserPermissions(string userId)
        {
            if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            {
                throw ApiExceptionDictionary.NoAccount;
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
            
            if (RequestUser.Id.Equals(userId))
            {
                throw ApiExceptionDictionary.BadRequest("You cannot change your own permissions.");
            }

            var userHasPermission = await _userRepository.GetUserPermission(userId, permissionKey);
            if (userHasPermission != null)
            {
                throw ApiExceptionDictionary.BadRequest("User already has this permission.");
            }

            var permission = await _permissionRepository.GetPermissionByKey(permissionKey);
            if (permission == null)
            {
                throw ApiExceptionDictionary.NotFound("Permission");
            }

            var entity = new UserPermission
            {
                UserId = userId,
                AccountId = RequestUser.AccountId,
                Permission = permission
            };

            _userRepository.Add(entity);
            await _userRepository.SaveChangesAsync();
        }

        public async Task RemoveUserPermission(string userId, string permissionKey)
        {
            if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            {
                throw ApiExceptionDictionary.NoAccount;
            }

            var userPermission = await _userRepository.GetUserPermission(userId, permissionKey);
            if (userPermission == null)
            {
                throw ApiExceptionDictionary.NotFound("User Permission");
            }

            _userRepository.Delete(userPermission);
            await _userRepository.SaveChangesAsync();
        }
    
}