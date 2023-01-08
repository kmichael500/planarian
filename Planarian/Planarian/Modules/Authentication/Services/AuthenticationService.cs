using System.Text;
using Microsoft.Extensions.Options;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Authentication.Repositories;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Exceptions;
using Planarian.Shared.Options;
using Planarian.Shared.Services;

namespace Planarian.Modules.Authentication.Services;

public class AuthenticationService : ServiceBase<AuthenticationRepository>
{
    private readonly TokenService _tokenService;
    private readonly UserRepository _userRepository;
    private readonly AuthOptions _options;

    public AuthenticationService(AuthenticationRepository repository, RequestUser requestUser,
        TokenService tokenService, AuthOptions options, UserRepository userRepository) : base(repository, requestUser)
    {
        _tokenService = tokenService;
        _options = options;
        _userRepository = userRepository;
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
        var user = await _userRepository.GetUserByEmail(email);

        if (user == null)
        {
            throw ApiExceptionDictionary.EmailDoesNotExist;
        }

        if (string.IsNullOrWhiteSpace(user.HashedPassword))
        {
            throw ApiExceptionDictionary.NotFound("Password");
        }

        var (isValid, _) = PasswordService.Check(user.HashedPassword, password);
        if (!isValid)
        {
            throw ApiExceptionDictionary.InvalidPassword;
        }

        var userForToken = new UserToken(user.FullName, user.Id);
        
        var token = _tokenService.BuildToken(userForToken);

        var bytes = Encoding.UTF8.GetBytes(token);
        httpContext.Session.Set(SessionStorageKeys.TokenKey, bytes);

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