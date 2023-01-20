using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Authentication.Repositories;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Email;
using Planarian.Shared.Exceptions;

namespace Planarian.Modules.Authentication.Services;

public class AuthenticationService : ServiceBase<AuthenticationRepository>
{
    private readonly TokenService _tokenService;
    private readonly UserRepository _userRepository;
    private readonly EmailService _emailService;

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
        if (user.IsEmailConfirmed == null || (bool)!user.IsEmailConfirmed)
        {
            user.EmailConfirmationCode = IdGenerator.Generate(PropertyLength.EmailConfirmationCode);
            await Repository.SaveChangesAsync();
            await _emailService.SendEmailConfirmationEmail(email, user.FullName, user.EmailConfirmationCode);
            throw ApiExceptionDictionary.EmailNotConfirmed;
        }

        if (string.IsNullOrWhiteSpace(user.HashedPassword)) throw ApiExceptionDictionary.InvalidPassword;

        var (isValid, _) = PasswordService.Check(user.HashedPassword, password);
        if (!isValid) throw ApiExceptionDictionary.InvalidPassword;

        var userForToken = new UserToken(user.FullName, user.Id);

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