using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Authentication.Repositories;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Email.Services;
using Planarian.Shared.Exceptions;

namespace Planarian.Modules.Authentication.Services;

public class AuthenticationService : ServiceBase<AuthenticationRepository>
{
    private readonly EmailService _emailService;
    private readonly TokenService _tokenService;
    private readonly UserRepository _userRepository;

    public AuthenticationService(AuthenticationRepository repository, RequestUser requestUser,
        TokenService tokenService, UserRepository userRepository, EmailService emailService) :
        base(repository, requestUser)
    {
        _tokenService = tokenService;
        _userRepository = userRepository;
        _emailService = emailService;
    }

    public async Task<string> AuthenticateEmailPassword(string email, string password)
    {
        var user = await _userRepository.GetUserByEmail(email);

        if (user == null) throw ApiExceptionDictionary.EmailDoesNotExist;
        if (user.EmailConfirmedOn == null)
        {
            await Repository.SaveChangesAsync();
            if (user.EmailConfirmationCode != null)
                await _emailService.SendEmailConfirmationEmail(email, user.FullName, user.EmailConfirmationCode);
            else
            {
                throw ApiExceptionDictionary.InternalServerError("Email confirmation code is does not exist.");
            }
            throw ApiExceptionDictionary.EmailNotConfirmed;
        }

        if (string.IsNullOrWhiteSpace(user.HashedPassword)) throw ApiExceptionDictionary.InvalidPassword;

        var (isValid, _) = PasswordService.Check(user.HashedPassword, password);
        if (!isValid) throw ApiExceptionDictionary.InvalidPassword;
        
        var accountIds = await Repository.GetAccountIdsByUserId(user.Id);
        var accountId = accountIds.FirstOrDefault();

        var userForToken = new UserToken(user.FullName, user.Id, accountId);

        var token = _tokenService.BuildToken(userForToken);

        return token;
    }

    public bool ValidateToken(string token)
    {
        var isValid = _tokenService.IsTokenValid(token);

        return isValid;
    }


    public async Task Logout(HttpContext httpContext)
    {
    }
}