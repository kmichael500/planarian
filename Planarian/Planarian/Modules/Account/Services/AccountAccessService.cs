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
        if (membership?.AccessRevokedOn.HasValue == true)
            throw ApiExceptionDictionary.Conflict(
                "This user already belongs to the account with revoked access. Restore access instead of inviting them again.");

        if (membership != null)
            throw ApiExceptionDictionary.UserAlreadyInAccount;
    }

    public async Task RevokeAccess(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw ApiExceptionDictionary.NoAccount;

        if (userId == RequestUser.Id)
            throw ApiExceptionDictionary.BadRequest("You cannot revoke your own account access.");

        try
        {
            await using var transaction =
                await _accountRepository.BeginAccountAccessTransactionAsync(cancellationToken);

            var membership = await _accountRepository.GetAccountUser(userId, RequestUser.AccountId);
            if (membership == null)
                throw ApiExceptionDictionary.NotFound("User");

            if (membership.AccessRevokedOn.HasValue)
                return;

            var user = await Repository.Get(userId);
            if (user?.IsTemporary == true &&
                !membership.InvitationAcceptedOn.HasValue &&
                !string.IsNullOrWhiteSpace(membership.InvitationCode))
            {
                if (!await Repository.DeletePendingInvitationAccess(userId, RequestUser.AccountId, cancellationToken))
                    throw ApiExceptionDictionary.NotFound("User");
            }
            else
            {
                if (await _accountRepository.WouldRevokeLastAdministrator(
                        userId, RequestUser.AccountId, cancellationToken))
                    throw ApiExceptionDictionary.Conflict(
                        "Cannot revoke access for the account's last active administrator.");

                membership.AccessRevokedOn = DateTime.UtcNow;
                await _accountRepository.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (IsSerializationFailure(exception))
        {
            throw ApiExceptionDictionary.Conflict("Account access changed concurrently. Please try again.");
        }
    }

    public async Task RestoreAccess(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw ApiExceptionDictionary.NoAccount;

        var membership = await _accountRepository.GetAccountUser(userId, RequestUser.AccountId);
        if (membership == null)
            throw ApiExceptionDictionary.NotFound("User");

        if (!membership.AccessRevokedOn.HasValue)
            return;

        membership.AccessRevokedOn = null;
        await _accountRepository.SaveChangesAsync(cancellationToken);
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
                await _accountRepository.WouldRevokeLastAdministrator(userId, RequestUser.AccountId, cancellationToken))
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
