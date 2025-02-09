using Planarian.Library.Constants;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.String;
using Planarian.Library.Options;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Users.Models;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Email.Services;

namespace Planarian.Modules.Users.Services;

public class UserService : ServiceBase<UserRepository>
{
    private const int PasswordResetExpirationMinutes = 30;
    private readonly EmailService _emailService;
    private readonly ServerOptions _serverOptions;

    public UserService(UserRepository repository, RequestUser requestUser, EmailService emailService,
        ServerOptions serverOptions) : base(repository,
        requestUser)
    {
        _emailService = emailService;
        _serverOptions = serverOptions;
    }

    public async Task UpdateCurrentUser(UserVm user)
    {
        var entity = await Repository.Get(RequestUser.Id);

        if (entity == null) throw new NullReferenceException("User not found");

        var emailExists = await Repository.EmailExists(user.EmailAddress, true);

        if (emailExists) throw ApiExceptionDictionary.EmailAlreadyExists;

        entity.FirstName = user.FirstName;
        entity.LastName = user.LastName;
        entity.EmailAddress = user.EmailAddress;
        entity.PhoneNumber = user.PhoneNumber;

        await Repository.SaveChangesAsync();
    }

    public async Task<UserVm?> GetUserVm(string id)
    {
        var user = await Repository.GetUserVm(id);

        return user;
    }

    public async Task UpdateCurrentUserPassword(string password)
    {
        if (!password.IsValidPassword()) throw ApiExceptionDictionary.InvalidPasswordComplexity;

        var entity = await Repository.Get(RequestUser.Id);

        if (entity == null) throw ApiExceptionDictionary.NotFound("User");

        entity.HashedPassword = PasswordService.Hash(password);

        await Repository.SaveChangesAsync();
    }

    public async Task RegisterUser(RegisterUserVm user, CancellationToken cancellationToken)
    {
        var exists = await Repository.EmailExists(user.EmailAddress);

        if (exists) throw ApiExceptionDictionary.EmailAlreadyExists;

        user.PhoneNumber = user.PhoneNumber.ExtractPhoneNumber();

        if (!user.PhoneNumber.IsValidPhoneNumber()) throw ApiExceptionDictionary.InvalidPhoneNumber;

        if (!user.Password.IsValidPassword()) throw ApiExceptionDictionary.InvalidPasswordComplexity;

        var dbTransaction = await Repository.BeginTransactionAsync(cancellationToken);

        try
        {
            var entity = new User(user.FirstName, user.LastName, user.EmailAddress, user.PhoneNumber)
            {
                HashedPassword = PasswordService.Hash(user.Password),
                EmailConfirmationCode = IdGenerator.Generate(PropertyLength.InvitationCode)
            };

            Repository.Add(entity);
            await Repository.SaveChangesAsync(cancellationToken);

            if (!user.InvitationCode.IsNullOrWhiteSpace())
            {
                await ClaimInvitation(entity, user.InvitationCode, cancellationToken);
            }

            await _emailService.SendEmailConfirmationEmail(entity.EmailAddress, entity.FullName,
                entity.EmailConfirmationCode);

            await dbTransaction.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
    
    
    public async Task ClaimInvitation(string invitationCode, string userEmail, CancellationToken cancellationToken)
    {
        var user = await Repository.GetUserByEmail(userEmail);
        if (user == null) throw ApiExceptionDictionary.NotFound("User");

        var dbTransaction = await Repository.BeginTransactionAsync(cancellationToken);
        try
        {
            await ClaimInvitation(user, invitationCode, cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task ClaimInvitation(User existingUser, string? invitationCode, CancellationToken cancellationToken)
    {
        var invitation = await Repository.GetInvitationEntities(invitationCode);

        if (invitation.AccountUser == null || invitation.User == null)
            throw ApiExceptionDictionary.NotFound("Invitation");

        var userAlreadyInAccount = await Repository.UserInAccount(existingUser.Id, invitation.AccountUser.AccountId);

        if (userAlreadyInAccount) throw ApiExceptionDictionary.UserAlreadyInAccount;

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
        await Repository.SaveChangesAsync(cancellationToken);

        await DeleteInvitation(invitation.AccountUser, invitation.User);

        await Repository.SaveChangesAsync(cancellationToken);
    }

    public async Task SendResetPasswordEmail(string email)
    {
        var user = await Repository.GetUserByEmail(email);
        if (user == null) throw ApiExceptionDictionary.EmailDoesNotExist;

        var resetCode = PasswordService.GenerateResetCode();
        var expiresOn = DateTime.UtcNow.AddMinutes(PasswordResetExpirationMinutes);
        user.PasswordResetCode = resetCode;
        user.PasswordResetCodeExpiration = expiresOn;

        await Repository.SaveChangesAsync();

        await _emailService.SendPasswordResetEmail(user.EmailAddress, user.FullName, resetCode);
    }

    public async Task ResetPassword(string code, string password)
    {
        var user = await Repository.GetUserByPasswordResetCode(code);
        if (user == null) throw ApiExceptionDictionary.InvalidPasswordResetCode;

        if (user.PasswordResetCodeExpiration < DateTime.UtcNow) throw ApiExceptionDictionary.PasswordResetCodeExpired;

        if (!password.IsValidPassword()) throw ApiExceptionDictionary.InvalidPasswordComplexity;

        user.PasswordResetCode = null;
        user.PasswordResetCodeExpiration = null;
        user.HashedPassword = PasswordService.Hash(password);

        await Repository.SaveChangesAsync();

        await _emailService.SendPasswordChangedEmail(user.EmailAddress, user.FullName);
    }

    public async Task ConfirmEmail(string code)
    {
        var user = await Repository.GetUserByPasswordEmailConfirmationCode(code);
        if (user == null) throw ApiExceptionDictionary.InvalidEmailConfirmationCode;

        user.EmailConfirmationCode = null;
        user.EmailConfirmedOn = DateTime.UtcNow;

        await Repository.SaveChangesAsync();
    }

    public async Task<AcceptInvitationVm?> GetInvitation(string code)
    {
        var invitation = await Repository.GetInvitation(code);

        if (invitation == null) throw ApiExceptionDictionary.NotFound("Invitation");

        return invitation;
    }

    public async Task DeclineInvitation(string code)
    {
        var invitation = await Repository.GetInvitationEntities(code);

        await DeleteInvitation(invitation.AccountUser, invitation.User);
    }
    
    private async Task DeleteInvitation(AccountUser? accountUser, User? user)
    {
        if (accountUser == null || user == null) throw ApiExceptionDictionary.NotFound("Invitation");
        Repository.Delete(accountUser);
        Repository.Delete(user);
        await Repository.SaveChangesAsync();
    }

    public async Task AcceptInvitation(string invitationCode, CancellationToken cancellationToken)
    {
        var user = await Repository.Get(RequestUser.Id);
        if (user == null) throw ApiExceptionDictionary.NotFound("User");

        var dbTransaction = await Repository.BeginTransactionAsync(cancellationToken);
        try
        {
            await ClaimInvitation(user, invitationCode, cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}