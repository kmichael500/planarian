using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;

namespace Planarian.Modules.Users.Services;

public sealed class InvitationLifecycleService : ServiceBase<UserRepository>
{
    public InvitationLifecycleService(UserRepository repository, RequestUser requestUser)
        : base(repository, requestUser)
    {
    }

    public async Task Claim(User existingUser, string? invitationCode, CancellationToken cancellationToken)
    {
        var invitation = await Repository.GetInvitationEntities(invitationCode);
        if (invitation.AccountUser == null || invitation.User == null)
            throw ApiExceptionDictionary.NotFound("Invitation");

        var existingMembership = await Repository.GetUserAccountMembership(
            existingUser.Id, invitation.AccountUser.AccountId);

        if (existingMembership?.AccessRevokedOn != null)
            throw ApiExceptionDictionary.Conflict(
                "This invitation cannot restore revoked account access. Ask an account administrator to restore it.");

        if (existingMembership != null)
        {
            // Keep the existing membership's permissions unchanged.
            await Repository.DeletePendingInvitationAccess(
                invitation.User.Id, invitation.AccountUser.AccountId, cancellationToken);
            return;
        }

        var accountUser = new AccountUser
        {
            AccountId = invitation.AccountUser.AccountId,
            UserId = existingUser.Id,
            InvitationAcceptedOn = DateTime.UtcNow,
            InvitationSentOn = invitation.AccountUser.InvitationSentOn,
            CreatedByUserId = invitation.AccountUser.CreatedByUserId,
            ModifiedByUserId = invitation.AccountUser.ModifiedByUserId
        };
        Repository.Add(accountUser);

        if (invitation.CavePermissions != null)
        {
            foreach (var permission in invitation.CavePermissions)
                permission.User = existingUser;
        }

        if (invitation.UserPermissions != null)
        {
            foreach (var permission in invitation.UserPermissions)
                permission.User = existingUser;
        }

        await Repository.SaveChangesAsync(cancellationToken);

        await Repository.ReassignInvitationMessageLogs(
            invitation.AccountUser.AccountId, invitation.AccountUser.UserId,
            existingUser.Id, cancellationToken);

        await Repository.DeletePendingInvitationAccess(
            invitation.User.Id, invitation.AccountUser.AccountId, cancellationToken);
    }

    public async Task Decline(string code, CancellationToken cancellationToken = default)
    {
        // Set-based cleanup must commit atomically.
        await using var transaction = await Repository.BeginTransactionAsync(cancellationToken);
        var invitation = await Repository.GetInvitationEntities(code);
        if (invitation.AccountUser == null || invitation.User == null)
            throw ApiExceptionDictionary.NotFound("Invitation");

        await Repository.DeletePendingInvitationAccess(
            invitation.User.Id, invitation.AccountUser.AccountId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
