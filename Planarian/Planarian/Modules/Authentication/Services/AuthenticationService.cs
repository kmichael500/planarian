using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Repositories;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Exceptions;
using Planarian.Shared.Options;

namespace Planarian.Modules.Authentication.Services;

public class AuthenticationService : ServiceBase<AuthenticationRepository>
{
    private readonly AuthOptions _options;
    private readonly TokenService _tokenService;
    private readonly UserRepository _userRepository;

    public AuthenticationService(AuthenticationRepository repository, RequestUser requestUser,
        TokenService tokenService, AuthOptions options, UserRepository userRepository) : base(repository, requestUser)
    {
        _tokenService = tokenService;
        _options = options;
        _userRepository = userRepository;
    }

    public async Task<string> AuthenticateEmailPassword(string email, string password)
    {
        var user = await _userRepository.GetUserByEmail(email);

        if (user == null) throw ApiExceptionDictionary.EmailDoesNotExist;

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