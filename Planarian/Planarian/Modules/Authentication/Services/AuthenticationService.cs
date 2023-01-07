using System.Text;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Options;

namespace Planarian.Modules.Authentication.Services;

public class AuthenticationService : ServiceBase<AuthenticationRepository>
{
    private readonly TokenService _tokenService;
    private readonly AuthOptions _options;
    public AuthenticationService(AuthenticationRepository repository, RequestUser requestUser, TokenService tokenService, AuthOptions options) : base(repository, requestUser)
    {
        _tokenService = tokenService;
        _options = options;
    }

    public static async Task AddAuthHeader(HttpContext context, Func<Task> next)
    {
        var token = context.Session.GetString(SessionStorageKeys.TokenKey);
        if (!string.IsNullOrWhiteSpace(token))
        {
            context.Request.Headers.TryAdd("Authorization", "Bearer " + token);
        }

        await next();
    }

    // public static async Task AddAuthHeader(HttpContent context, Func<Task> next)
    // {
    //     var token = context.Session.GetString(SessionStorageKeys.TokenKey);
    //     if (!string.IsNullOrWhiteSpace(token))
    //     {
    //         context.Request.Headers.TryAdd("Authorization", "Bearer " + token);
    //     }
    //     await next();
    //
    // }
    public async Task<string> AuthenticateEmailPassword(string email, string password, HttpContext httpContext)
    {
        // TODO: Verify password and get userId;
        var userId = "uJ9a1oaA10";

        var user = await Repository.GetUserForToken(userId) ?? throw new NullReferenceException();

        var token = _tokenService.BuildToken(user);

        var bytes = Encoding.UTF8.GetBytes(token);
        httpContext.Session.Set(SessionStorageKeys.TokenKey,bytes );

        return token;
    }

    public bool ValidateToken(string token)
    {
        var isValid = _tokenService.IsTokenValid(token);

        return isValid;
    }


    public async Task Logout(HttpContext httpContext)
    {
        httpContext.Session.Remove(SessionStorageKeys.TokenKey);
    }
}