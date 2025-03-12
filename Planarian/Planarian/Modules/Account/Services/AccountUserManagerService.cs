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

    public AccountUserManagerService(
        UserRepository repository,
        RequestUser requestUser,
        EmailService emailService,
        ServerOptions serverOptions, AccountRepository accountRepository,
        PermissionRepository permissionRepository) : base(repository, requestUser)
    {
        _emailService = emailService;
        _accountRepository = accountRepository;
        _permissionRepository = permissionRepository;
    }

    #region User Manager

    public async Task<List<UserManagerGridVm>> GetAccountUsers()
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;


        return await Repository.GetAccountUsers(RequestUser.AccountId);
    }

    public async Task InviteUser(InviteUserRequest request, CancellationToken cancellationToken)
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

            var accountName = await _accountRepository.GetAccountName(RequestUser.AccountId);

            await _emailService.SendAccountInvitationEmail(user, accountUser,
                accountName);
            await dbTransaction.CommitAsync(cancellationToken);
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

    public async Task<CavePermissionManagementVm> GetLocationPermissions(string userId, string permissionKey)
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

    /// <summary>
    /// Updates the user's location permissions by creating/removing CavePermission rows.
    /// If HasAllLocations is true, we store a single row with no CountyId or CaveId.
    /// Otherwise we store rows for each county/cave in the request. 
    /// </summary>
    public async Task UpdateLocationPermissions(string userId, string permissionKey, CreateUserCavePermissionsVm vm)
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

        var permission = await _permissionRepository.GetPermissionByKey(permissionKey);
        if (permission == null)
        {
            throw ApiExceptionDictionary.NotFound("Permission");
        }

        var existingPermissions =
            (await Repository.GetCavePermissions(userId, RequestUser.AccountId, permissionKey)).ToList();
        Repository.DeleteRange(existingPermissions);

        if (vm.HasAllLocations)
        {

            // create a single row that indicates "all"
            var row = new CavePermission
            {
                UserId = userId,
                AccountId = RequestUser.AccountId,
                CountyId = null,
                CaveId = null,
                Permission = permission
            };
            Repository.Add(row);

            await Repository.SaveChangesAsync();
            return;
        }

        foreach (var countyId in vm.CountyIds.Distinct())
        {
            var row = new CavePermission
            {
                UserId = userId,
                AccountId = RequestUser.AccountId,
                CountyId = countyId,
                CaveId = null,
                Permission = permission
            };
            Repository.Add(row);
        }

        foreach (var caveId in vm.CaveIds.Distinct())
        {
            var row = new CavePermission
            {
                UserId = userId,
                AccountId = RequestUser.AccountId,
                CaveId = caveId,
                CountyId = null,
                Permission = permission
            };
            Repository.Add(row);
        }

        await Repository.SaveChangesAsync();
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

    public async Task<IEnumerable<SelectListItemDescriptionData<string, PermissionSelectListData>>> GetPermissionSelectList()
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
        {
            throw ApiExceptionDictionary.NoAccount;
        }

        var permissions = await _permissionRepository.GetPermissionSelectList();
        return permissions;
    }
}