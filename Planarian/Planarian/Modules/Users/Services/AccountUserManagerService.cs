using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.String;
using Planarian.Library.Options;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Users.Models;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Email.Services;

namespace Planarian.Modules.Users.Services;

public class AccountUserManagerService : ServiceBase<UserRepository>
{
    private readonly EmailService _emailService;
    private readonly AccountRepository _accountRepository;

    public AccountUserManagerService(
        UserRepository repository,
        RequestUser requestUser,
        EmailService emailService,
        ServerOptions serverOptions, AccountRepository accountRepository) : base(repository, requestUser)
    {
        _emailService = emailService;
        _accountRepository = accountRepository;
    }

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
}