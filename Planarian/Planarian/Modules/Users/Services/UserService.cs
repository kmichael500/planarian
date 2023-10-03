using Planarian.Library.Constants;
using Planarian.Library.Extensions.String;
using Planarian.Library.Options;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Users.Models;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Email.Services;
using Planarian.Shared.Exceptions;

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
            EmailConfirmationCode = IdGenerator.Generate(PropertyLength.EmailConfirmationCode)
        };

        Repository.Add(entity);

        await Repository.SaveChangesAsync();

        await _emailService.SendEmailConfirmationEmail(entity.EmailAddress, entity.FullName,
            entity.EmailConfirmationCode);
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
}