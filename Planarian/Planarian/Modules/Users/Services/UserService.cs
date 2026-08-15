using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Library.Constants;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.String;
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
using Planarian.Shared.Helpers;
using Planarian.Shared.Models;
using Planarian.Shared.Services;

namespace Planarian.Modules.Users.Services;

public class UserService : ServiceBase<UserRepository>
{
    private const int PasswordResetExpirationMinutes = 30;
    private readonly EmailService _emailService;
    private readonly RequestThrottleService _requestThrottleService;
    private readonly IApiRequestOrigin _apiRequestOrigin;
    private readonly BlobService _blobService;

    public UserService(UserRepository repository, RequestUser requestUser, EmailService emailService,
        IApiRequestOrigin apiRequestOrigin, RequestThrottleService requestThrottleService, BlobService blobService) : base(repository,
        requestUser)
    {
        _emailService = emailService;
        _apiRequestOrigin = apiRequestOrigin;
        _requestThrottleService = requestThrottleService;
        _blobService = blobService;
    }

    public async Task UpdateCurrentUser(UpdateCurrentUserVm user, CancellationToken cancellationToken = default)
    {
        var entity = await Repository.Get(RequestUser.Id);
        if (entity == null) throw ApiExceptionDictionary.NotFound("User");

        var normalizedEmail = user.EmailAddress.Trim().ToLowerInvariant();
        var emailChanged = !string.Equals(entity.EmailAddress, normalizedEmail, StringComparison.OrdinalIgnoreCase);

        if (emailChanged)
        {
            if (string.IsNullOrWhiteSpace(user.CurrentPassword) || string.IsNullOrWhiteSpace(entity.HashedPassword))
                throw ApiExceptionDictionary.InvalidPassword;

            var passwordCheck = PasswordService.Check(entity.HashedPassword, user.CurrentPassword);
            if (!passwordCheck.Verified) throw ApiExceptionDictionary.InvalidPassword;
            if (passwordCheck.NeedsUpgrade) entity.HashedPassword = PasswordService.Hash(user.CurrentPassword);

            if (await Repository.EmailExists(normalizedEmail, true))
                throw ApiExceptionDictionary.EmailAlreadyExists;

            entity.PendingEmailAddress = normalizedEmail;
            entity.EmailConfirmationCode = IdGenerator.Generate(PropertyLength.InvitationCode);
            entity.EmailConfirmationMessageLogId = null;
        }

        entity.FirstName = user.FirstName;
        entity.LastName = user.LastName;
        entity.PhoneNumber = user.PhoneNumber;

        await Repository.SaveChangesAsync(cancellationToken);

        if (emailChanged)
        {
            await _emailService.SendEmailConfirmationEmail(
                entity.PendingEmailAddress!,
                entity.FullName,
                entity.EmailConfirmationCode!,
                (messageLogId, token) => Repository.TrySetEmailConfirmationMessageLog(
                    entity.Id, entity.EmailConfirmationCode!, null, messageLogId, token),
                CancellationToken.None);
        }
    }

    public async Task<UserVm?> GetUserVm(string id)
    {
        var user = await Repository.GetUserVm(id);

        return user;
    }

    public async Task<NameProfilePhotoVm> GetUserDisplayInfo(string userId)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;

        var user = await Repository.GetUserDisplayInfo(userId);
        if (user == null) throw ApiExceptionDictionary.NotFound("User");

        if (string.IsNullOrWhiteSpace(user.BlobKey)) return user;

        user.ProfilePhotoUrl = UrlHelper.Build(
            _apiRequestOrigin.GetOrigin(),
            $"/api/users/{userId}/photo",
            RequestUser.AccountId);
        return user;
    }

    public async Task<AuthenticatedFileResponse> GetUserProfilePhotoResponse(string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;

        var blobKey = await Repository.GetUserProfilePhotoBlobKey(userId);
        if (string.IsNullOrWhiteSpace(blobKey))
            throw ApiExceptionDictionary.NotFound("Profile photo");

        return await _blobService.CreateBlobResponse(
            blobKey,
            $"user-{userId}-profile-photo",
            false,
            cancellationToken,
            fallbackContentType: "image/jpeg");
    }

    public async Task UpdateCurrentUserPassword(UpdateCurrentUserPasswordVm request)
    {
        if (!request.Password.IsValidPassword()) throw ApiExceptionDictionary.InvalidPasswordComplexity;

        var entity = await Repository.Get(RequestUser.Id);
        if (entity == null) throw ApiExceptionDictionary.NotFound("User");
        if (string.IsNullOrWhiteSpace(entity.HashedPassword) ||
            !PasswordService.Check(entity.HashedPassword, request.CurrentPassword).Verified)
        {
            throw ApiExceptionDictionary.InvalidPassword;
        }

        entity.HashedPassword = PasswordService.Hash(request.Password);
        entity.SessionVersion++;

        await Repository.SaveChangesAsync();
        await _emailService.SendPasswordChangedEmail(entity.EmailAddress, entity.FullName, CancellationToken.None);
    }

    public async Task<RegisterUserResultVm> RegisterUser(RegisterUserVm user, CancellationToken cancellationToken)
    {
        var exists = await Repository.EmailExists(user.EmailAddress);

        if (exists) throw ApiExceptionDictionary.EmailAlreadyExists;

        user.PhoneNumber = user.PhoneNumber.ExtractPhoneNumber();

        if (!user.PhoneNumber.IsValidPhoneNumber()) throw ApiExceptionDictionary.InvalidPhoneNumber;

        if (!user.Password.IsValidPassword()) throw ApiExceptionDictionary.InvalidPasswordComplexity;

        var entity = new User(user.FirstName, user.LastName, user.EmailAddress, user.PhoneNumber)
        {
            HashedPassword = PasswordService.Hash(user.Password),
            EmailConfirmationCode = IdGenerator.Generate(PropertyLength.InvitationCode)
        };

        await using (var dbTransaction = await Repository.BeginTransactionAsync(cancellationToken))
        {
            try
            {
                Repository.Add(entity);
                await Repository.SaveChangesAsync(cancellationToken);

                if (!user.InvitationCode.IsNullOrWhiteSpace())
                {
                    await ClaimInvitation(entity, user.InvitationCode, cancellationToken);
                }

                await dbTransaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        var sendResult = await _emailService.SendEmailConfirmationEmail(
            entity.EmailAddress,
            entity.FullName,
            entity.EmailConfirmationCode,
            (messageLogId, token) => Repository.TrySetEmailConfirmationMessageLog(
                entity.Id, entity.EmailConfirmationCode!, null, messageLogId, token),
            CancellationToken.None);

        return new RegisterUserResultVm
        {
            ConfirmationEmailDeliveryStatus = sendResult.DeliveryStatus
        };
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

        if (invitation.CavePermissions != null)
        {
            foreach (var cavePermission in invitation.CavePermissions)
            {
                cavePermission.User = existingUser;
            }
        }
        
        if (invitation.UserPermissions != null)
        {
            foreach (var userPermission in invitation.UserPermissions)
            {
                userPermission.User = existingUser;
            }
        }
        
        await Repository.SaveChangesAsync(cancellationToken);

        await Repository.ReassignInvitationMessageLogs(
            invitation.AccountUser.AccountId, invitation.AccountUser.UserId, existingUser.Id, cancellationToken);

        await DeleteInvitation(invitation.AccountUser, invitation.User);

        await Repository.SaveChangesAsync(cancellationToken);
    }

    public async Task SendResetPasswordEmail(string email)
    {
        await _requestThrottleService.CountAttempt(ThrottleProfile.PasswordReset, email);

        var user = await Repository.GetUserByEmail(email);
        if (user == null)
        {
            // SECURITY/PRODUCT DECISION: Planarian intentionally returns account-existence feedback here
            // for usability. Do not replace this with a generic response without explicit product approval.
            // Request throttling still limits automated discovery.
            throw ApiExceptionDictionary.EmailDoesNotExist;
        }

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

        if (user.PasswordResetCodeExpiration == null || user.PasswordResetCodeExpiration < DateTime.UtcNow)
        {
            throw ApiExceptionDictionary.PasswordResetCodeExpired;
        }

        if (!password.IsValidPassword()) throw ApiExceptionDictionary.InvalidPasswordComplexity;

        user.PasswordResetCode = null;
        user.PasswordResetCodeExpiration = null;
        user.HashedPassword = PasswordService.Hash(password);
        user.SessionVersion++;

        await Repository.SaveChangesAsync();

        await _emailService.SendPasswordChangedEmail(user.EmailAddress, user.FullName);
    }

    public async Task<EmailConfirmationResult> ConfirmEmail(string code)
    {
        var user = await Repository.GetUserByPasswordEmailConfirmationCode(code);
        if (user == null) throw ApiExceptionDictionary.InvalidEmailConfirmationCode;

        var sessionVersionChanged = false;
        if (!string.IsNullOrWhiteSpace(user.PendingEmailAddress))
        {
            if (await Repository.EmailExists(user.PendingEmailAddress, true))
                throw ApiExceptionDictionary.EmailAlreadyExists;

            user.EmailAddress = user.PendingEmailAddress;
            user.PendingEmailAddress = null;
            user.SessionVersion++;
            sessionVersionChanged = true;
        }

        user.EmailConfirmationCode = null;
        user.EmailConfirmedOn = DateTime.UtcNow;

        // The pre-check above is for a friendly fast failure; the unique index remains authoritative
        // when two pending-email confirmations race. Translate only that specific constraint violation.
        try
        {
            await Repository.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when
            (exception.InnerException is PostgresException
             { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Users_EmailAddress" })
        {
            throw ApiExceptionDictionary.EmailAlreadyExists;
        }

        return new EmailConfirmationResult(user.EmailAddress, user.Id, sessionVersionChanged);
    }

    public async Task ResendEmailConfirmation(string emailAddress, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = emailAddress.Trim();
        await _requestThrottleService.CountAttempt(ThrottleProfile.EmailConfirmation, normalizedEmail);

        var user = await Repository.GetUserByConfirmationEmail(normalizedEmail);
        if (user == null || string.IsNullOrWhiteSpace(user.EmailConfirmationCode))
        {
            return;
        }

        var confirmationEmail = user.PendingEmailAddress ?? user.EmailAddress;
        var expectedMessageLogId = user.EmailConfirmationMessageLogId;
        await _emailService.SendEmailConfirmationEmail(
            confirmationEmail,
            user.FullName,
            user.EmailConfirmationCode,
            (messageLogId, token) => Repository.TrySetEmailConfirmationMessageLog(
                user.Id, user.EmailConfirmationCode, expectedMessageLogId, messageLogId, token),
            CancellationToken.None);
    }

    public async Task<AcceptInvitationVm?> GetInvitation(string code)
    {
        var invitation = await Repository.GetInvitation(code);

        if (invitation == null) throw ApiExceptionDictionary.NotFound("Invitation");

        return invitation;
    }

    public async Task<List<AcceptInvitationVm>> GetPendingInvitationsForCurrentUser()
    {
        return await Repository.GetPendingInvitationsForCurrentUser();
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
