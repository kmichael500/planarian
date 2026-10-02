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
            // SECURITY/PRODUCT DECISION: Planarian intentionally distinguishes an unknown email
            // for usability. Do not replace this with a generic login error without explicit product approval.
            // Login throttling still limits automated discovery.
            throw ApiExceptionDictionary.EmailDoesNotExist;
        }
        
        if (string.IsNullOrWhiteSpace(user.HashedPassword))
        {
            throw ApiExceptionDictionary.InvalidPassword;
        }

        var passwordCheck = PasswordService.Check(user.HashedPassword, password);
        if (!passwordCheck.Verified)
        {
            throw ApiExceptionDictionary.InvalidPassword;
        }

        if (passwordCheck.NeedsUpgrade)
        {
            var upgradedHash = PasswordService.Hash(password);
            var upgraded = await _userRepository.UpgradePasswordHash(user.Id, user.HashedPassword, upgradedHash);
            if (!upgraded)
            {
                var currentUser = await _userRepository.GetUserByEmail(email);
                if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.HashedPassword) ||
                    !PasswordService.Check(currentUser.HashedPassword, password).Verified)
                {
                    throw ApiExceptionDictionary.InvalidPassword;
                }

                user = currentUser;
            }
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

        return await BuildTokenForUser(user.FullName, user.Id, user.SessionVersion);
    }

    internal async Task SetAuthenticatedSessionForConfirmedUser(HttpContext httpContext, string emailAddress)
    {
        var user = await _userRepository.GetUserByEmail(emailAddress);
        if (user == null) throw ApiExceptionDictionary.NotFound("User");
        if (user.EmailConfirmedOn == null) throw ApiExceptionDictionary.EmailNotConfirmed;

        var token = await BuildTokenForUser(user.FullName, user.Id, user.SessionVersion);
        _authCookieService.SetAuthCookie(httpContext, token, rememberMe: false);
    }

    internal async Task RefreshAuthenticatedSessionForUser(HttpContext httpContext, string userId)
    {
        var user = await _userRepository.Get(userId);
        if (user == null) throw ApiExceptionDictionary.NotFound("User");

        var token = await BuildTokenForUser(user.FullName, user.Id, user.SessionVersion);
        _authCookieService.SetAuthCookie(httpContext, token, rememberMe: false);
    }

    private async Task<string> BuildTokenForUser(string fullName, string userId, int sessionVersion)
    {
        var accounts = (await Repository.GetAccountIdsByUserId(userId)).ToList();
        var accountId = accounts.FirstOrDefault();
        return _tokenService.BuildToken(new UserToken(fullName, userId, accountId, sessionVersion));
    }

    public void Logout(HttpContext httpContext)
    {
        _authCookieService.ClearAuthCookie(httpContext);
        _authCookieService.ClearAntiforgeryCookies(httpContext);
    }

}
