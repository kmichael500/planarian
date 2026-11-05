using Npgsql;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;
using PermissionKey = Planarian.Model.Database.Entities.RidgeWalker.PermissionKey;

namespace Planarian.Modules.Account.Services;

public sealed class AccountAccessService : ServiceBase<UserRepository>
{
    private readonly AccountRepository _accountRepository;

    public AccountAccessService(UserRepository repository, AccountRepository accountRepository,
        RequestUser requestUser) : base(repository, requestUser)
    {
        _accountRepository = accountRepository;
    }

    public async Task EnsureCanInvite(string emailAddress)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw ApiExceptionDictionary.NoAccount;

        var existingUser = await Repository.GetUserByEmail(emailAddress);
        if (existingUser == null)
            return;

        var membership = await _accountRepository.GetAccountUser(existingUser.Id, RequestUser.AccountId);
        if (membership?.AccessDisabledOn.HasValue == true)
            throw ApiExceptionDictionary.Conflict(
                "This user already belongs to the account with disabled access. Restore access instead of inviting them again.");

        if (membership != null)
            throw ApiExceptionDictionary.UserAlreadyInAccount;
    }

    public async Task<bool> DisableAccess(string userId, CancellationToken cancellationToken = default,
        string? disableMessage = null, bool sendEmail = true)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw ApiExceptionDictionary.NoAccount;

        if (userId == RequestUser.Id)
            throw ApiExceptionDictionary.BadRequest("You cannot disable your own account access.");

        disableMessage = sendEmail ? disableMessage?.Trim() : null;
        if (disableMessage?.Length > 1000)
            throw ApiExceptionDictionary.BadRequest("The notification message must be 1,000 characters or fewer.");

        try
        {
            await using var transaction =
                await _accountRepository.BeginAccountAccessTransactionAsync(cancellationToken);

            var membership = await _accountRepository.GetAccountUser(userId, RequestUser.AccountId);
            if (membership == null)
                throw ApiExceptionDictionary.NotFound("User");

            if (membership.AccessDisabledOn.HasValue)
                return false;

            var user = await Repository.Get(userId);
            if (user?.IsTemporary == true &&
                !membership.InvitationAcceptedOn.HasValue &&
                !string.IsNullOrWhiteSpace(membership.InvitationCode))
            {
                if (!await Repository.DeleteAccountUserAccess(userId, RequestUser.AccountId, cancellationToken))
                    throw ApiExceptionDictionary.NotFound("User");
            }
            else
            {
                if (sendEmail && string.IsNullOrWhiteSpace(disableMessage))
                    throw ApiExceptionDictionary.BadRequest("A notification message is required when sending email.");

                if (await _accountRepository.WouldLeaveNoActiveAdministrator(
                        userId, RequestUser.AccountId, cancellationToken))
                    throw ApiExceptionDictionary.Conflict(
                        "Cannot disable access for the account's last active administrator.");

                membership.AccessDisabledOn = DateTime.UtcNow;
                membership.AccessDisabledMessage = disableMessage;
                await _accountRepository.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return user?.IsTemporary == false;
        }
        catch (Exception exception) when (IsSerializationFailure(exception))
        {
            throw ApiExceptionDictionary.Conflict("Account access changed concurrently. Please try again.");
        }
    }

    public async Task RevokeMembership(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw ApiExceptionDictionary.NoAccount;

        if (userId == RequestUser.Id)
            throw ApiExceptionDictionary.BadRequest("You cannot revoke your own account membership.");

        try
        {
            await using var transaction =
                await _accountRepository.BeginAccountAccessTransactionAsync(cancellationToken);

            var membership = await _accountRepository.GetAccountUser(userId, RequestUser.AccountId);
            if (membership == null)
                throw ApiExceptionDictionary.NotFound("User");

            if (await _accountRepository.WouldLeaveNoActiveAdministrator(
                    userId, RequestUser.AccountId, cancellationToken))
                throw ApiExceptionDictionary.Conflict(
                    "Cannot revoke the membership of the account's last active administrator.");

            if (!await Repository.DeleteAccountUserAccess(
                    userId, RequestUser.AccountId, cancellationToken))
                throw ApiExceptionDictionary.NotFound("User");

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (IsSerializationFailure(exception))
        {
            throw ApiExceptionDictionary.Conflict("Account access changed concurrently. Please try again.");
        }
    }

    public async Task<bool> RestoreAccess(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw ApiExceptionDictionary.NoAccount;

        var membership = await _accountRepository.GetAccountUser(userId, RequestUser.AccountId);
        if (membership == null)
            throw ApiExceptionDictionary.NotFound("User");

        if (!membership.AccessDisabledOn.HasValue)
            return false;

        membership.AccessDisabledOn = null;
        membership.AccessDisabledMessage = null;
        await _accountRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RemoveUserPermission(string userId, string permissionKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw ApiExceptionDictionary.NoAccount;

        await RequestUser.HasCavePermission(PermissionPolicyKey.Admin);

        if (userId == RequestUser.Id)
            throw ApiExceptionDictionary.BadRequest("You cannot change your own permissions.");

        if (permissionKey == PermissionKey.PlanarianAdmin)
            throw ApiExceptionDictionary.BadRequest(
                "Planarian Admin cannot be managed from account user management.");

        try
        {
            await using var transaction =
                await _accountRepository.BeginAccountAccessTransactionAsync(cancellationToken);

            var membership = await _accountRepository.GetAccountUser(userId, RequestUser.AccountId);
            if (membership == null)
                throw ApiExceptionDictionary.NotFound("User");

            var userPermission = await Repository.GetUserPermission(userId, permissionKey);
            if (userPermission == null)
                throw ApiExceptionDictionary.NotFound("User Permission");

            if (permissionKey == PermissionKey.Admin &&
                !await _accountRepository.HasGlobalAdministratorPermission(userId, cancellationToken) &&
                await _accountRepository.WouldLeaveNoActiveAdministrator(userId, RequestUser.AccountId, cancellationToken))
                throw ApiExceptionDictionary.Conflict(
                    "Cannot remove the last active administrator's Admin permission.");

            Repository.Delete(userPermission);
            await Repository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (IsSerializationFailure(exception))
        {
            throw ApiExceptionDictionary.Conflict("Account permissions changed concurrently. Please try again.");
        }
    }

    private static bool IsSerializationFailure(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is PostgresException postgres &&
                postgres.SqlState == PostgresErrorCodes.SerializationFailure)
                return true;
        }

        return false;
    }
}
