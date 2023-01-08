using System.Text.RegularExpressions;
using Planarian.Library.Constants;
using Planarian.Model.Shared;
using Planarian.Modules.Users.Models;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Services;

namespace Planarian.Modules.Users.Services;

public class UserService : ServiceBase<UserRepository>
{
    public UserService(UserRepository repository, RequestUser requestUser) : base(repository, requestUser)
    {
    }

    public async Task UpdateCurrentUser(UserVm user)
    {
        var entity = await Repository.Get(RequestUser.Id);

        if (entity == null)
        {
            throw new NullReferenceException("User not found");
        }
        
        var emailExists = await Repository.EmailExists(user.EmailAddress, true);

        if (emailExists)
        {
            throw ApiExceptionDictionary.EmailAlreadyExists;
        }
        
        entity.FirstName = user.FirstName;
        entity.LastName = user.LastName;
        entity.EmailAddress = user.EmailAddress;
        entity.PhoneNumber = user.PhoneNumber;

        await Repository.SaveChangesAsync();
    }

    public async Task<UserVm> GetUserVm(string id)
    {
        var user = await Repository.GetUserVm(id);

        return user;
    }

    public async Task UpdateCurrentUserPassword(string password)
    {
        if (!Regex.IsMatch(password, RegularExpressions.PasswordValidation))
        {
            throw ApiExceptionDictionary.InvalidPassword;
        }

        var entity = await Repository.Get(RequestUser.Id);
        
        if (entity == null)
        {
            throw ApiExceptionDictionary.NotFound("User");
        }
        
    }
}