using Planarian.Library.Constants;
using Planarian.Library.Exceptions;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Authentication.Repositories;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Email.Services;

namespace Planarian.Modules.Authentication.Services;

public class AuthenticationService : ServiceBase<AuthenticationRepository>
{
    private readonly AuthCookieService _authCookieService;
    private readonly RequestThrottleService _requestThrottleService;
    private readonly TokenService _tokenService;
    private readonly UserRepository _userRepository;
    private readonly MessageLogRepository _messageLogRepository;

    public AuthenticationService(AuthenticationRepository repository, RequestUser requestUser,
        TokenService tokenService, UserRepository userRepository, AuthCookieService authCookieService,
        RequestThrottleService requestThrottleService, MessageLogRepository messageLogRepository) :
        base(repository, requestUser)
    {
        _authCookieService = authCookieService;
        _tokenService = tokenService;
        _userRepository = userRepository;
        _requestThrottleService = requestThrottleService;
        _messageLogRepository = messageLogRepository;
    }

    public async Task AuthenticateEmailPassword(HttpContext httpContext, string email, string password, bool rememberMe)
    {
        var token = await AuthenticateEmailPassword(email, password);
        _authCookieService.SetAuthCookie(httpContext, token, rememberMe);
    }

    public async Task<string> AuthenticateEmailPassword(string email, string password)
    {
        await _requestThrottleService.CountAttempt(ThrottleProfile.Login, email);

        var user = await _userRepository.GetUserByEmail(email);

        if (user == null)
        {
            throw ApiExceptionDictionary.EmailDoesNotExist;
        }
        
        if (string.IsNullOrWhiteSpace(user.HashedPassword))
        {
            throw ApiExceptionDictionary.InvalidPassword;
        }

        var (isValid, _) = PasswordService.Check(user.HashedPassword, password);
        if (!isValid)
        {
            throw ApiExceptionDictionary.InvalidPassword;
        }

        if (user.EmailConfirmedOn == null)
        {
            var exception = ApiExceptionDictionary.EmailNotConfirmed;
            exception.Data = new EmailNotConfirmedDataVm
            {
                ConfirmationEmailDeliveryStatus = await _messageLogRepository.GetDeliveryStatus(
                    user.EmailConfirmationMessageLogId)
            };
            throw exception;
        }

        var accounts = (await Repository.GetAccountIdsByUserId(user.Id)).ToList();
        var accountId = accounts.FirstOrDefault();

        var userForToken = new UserToken(user.FullName, user.Id, accountId);

        return _tokenService.BuildToken(userForToken);
    }

    public void Logout(HttpContext httpContext)
    {
        _authCookieService.ClearAuthCookie(httpContext);
        _authCookieService.ClearAntiforgeryCookies(httpContext);
    }

}
