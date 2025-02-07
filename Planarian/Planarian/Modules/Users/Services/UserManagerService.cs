using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.String;
using Planarian.Library.Options;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Users.Models;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Email.Services;

namespace Planarian.Modules.Users.Services;

public class UserManagerService : ServiceBase<UserRepository>
{
    private readonly EmailService _emailService;
    private readonly AccountRepository _accountRepository;

    public UserManagerService(
        UserRepository repository,
        RequestUser requestUser,
        EmailService emailService,
        ServerOptions serverOptions, AccountRepository accountRepository) : base(repository, requestUser)
    {
        _emailService = emailService;
        _accountRepository = accountRepository;
    }

    public async Task<List<AccountUserVm>> GetAccountUsers()
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;


        return await Repository.GetAccountUsers(RequestUser.AccountId);
    }

    public async Task InviteUser(InviteUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;


        // Validate email
        if (!request.EmailAddress.IsValidEmail())
        {
            throw ApiExceptionDictionary.BadRequest("Invalid email address.");
        }

        if (!request.FirstName.IsNullOrWhiteSpace())
        {
            throw ApiExceptionDictionary.BadRequest("First name is required.");
        }

        if (!request.LastName.IsNullOrWhiteSpace())
        {
            throw ApiExceptionDictionary.BadRequest("Last name is required.");
        }

        var dbTransaction = await Repository.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = new User(request.FirstName, request.LastName, request.EmailAddress);
            Repository.Add(user);

            var invitationCode = Guid.NewGuid().ToString("N");

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
}