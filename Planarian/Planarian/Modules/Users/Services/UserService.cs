using Planarian.Library.Extensions.String;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Users.Models;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Email;
using Planarian.Shared.Email.Substitutions;
using Planarian.Shared.Exceptions;
using Planarian.Shared.Options;

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

    public async Task RegisterUser(RegisterUserVm user)
    {
        var exists = await Repository.EmailExists(user.EmailAddress);

        if (exists) throw ApiExceptionDictionary.EmailAlreadyExists;

        user.PhoneNumber = user.PhoneNumber.ExtractPhoneNumber();

        if (!user.PhoneNumber.IsValidPhoneNumber()) throw ApiExceptionDictionary.InvalidPhoneNumber;

        if (!user.Password.IsValidPassword()) throw ApiExceptionDictionary.InvalidPasswordComplexity;

        var entity = new User(user.FirstName, user.LastName, user.EmailAddress, user.PhoneNumber)
        {
            HashedPassword = PasswordService.Hash(user.Password),
            EmailConfirmationCode = IdGenerator.Generate(PropertyLength.EmailConfirmationCode),
            IsEmailConfirmed = false
        };

        Repository.Add(entity);

        await Repository.SaveChangesAsync();

        var link = $"{_serverOptions.ClientBaseUrl}/confirm-email?code={entity.EmailConfirmationCode}";

        var paragraphs = new List<string>
        {
            "Welcome to Planarian!",
            "Please confirm your email address by clicking the link below. If you did not sign up for Planarian, please ignore this email."
        };

        await _emailService.SendGenericEmail("Confirm your email address", entity.EmailAddress, entity.FullName,
            new GenericEmailSubstitutions(paragraphs,
                "Confirm your email address", "Confirm Email", link));
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

        const string message = "We have received a request to reset your password for your account. If you did not make this request, please ignore this email. If you did make this request, please click the link below to reset your password. This link will expire in 30 minutes.";

        var link = $"{_serverOptions.ClientBaseUrl}/reset-password?code={resetCode}";

        await _emailService.SendGenericEmail("Planarian Password Reset", user.EmailAddress, user.FullName,
            new GenericEmailSubstitutions(message, "Password Reset", "Reset Password", link));
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

        const string message =
            "You're password was just changed. If you did not make this request, please contact us immediately.";

        await _emailService.SendGenericEmail("Planarian Password Changed", user.EmailAddress, user.FullName,
            new GenericEmailSubstitutions(message, "Planarian Password Changed"));
    }
    
    public async Task ConfirmEmail(string code)
    {
        var user = await Repository.GetUserByPasswordEmailConfirmationCode(code);
        if (user == null) throw ApiExceptionDictionary.InvalidEmailConfirmationCode;

        user.EmailConfirmationCode = null;
        user.IsEmailConfirmed = true;

        await Repository.SaveChangesAsync();
    }
}