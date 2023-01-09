using System.Text.RegularExpressions;
using Planarian.Library.Constants;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Users.Models;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Exceptions;
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
        if (!password.IsValidPassword())
        {
            throw ApiExceptionDictionary.InvalidPasswordComplexity;
        }

        var entity = await Repository.Get(RequestUser.Id);

        if (entity == null)
        {
            throw ApiExceptionDictionary.NotFound("User");
        }

    }

    public async Task RegisterUser(RegisterUserVm user)
    {
        var exists = await Repository.EmailExists(user.EmailAddress);

        if (exists)
        {
            throw ApiExceptionDictionary.EmailAlreadyExists;
        }

        user.PhoneNumber = user.PhoneNumber.ExtractPhoneNumber();

        if (!user.PhoneNumber.IsValidPhoneNumber())
        {
            throw ApiExceptionDictionary.InvalidPhoneNumber;
        }

        if (!user.Password.IsValidPassword())
        {
            throw ApiExceptionDictionary.InvalidPasswordComplexity;
        }

        var entity = new User(user.FirstName, user.LastName, user.EmailAddress, user.PhoneNumber)
        {
            HashedPassword = PasswordService.Hash(user.Password)
        };

        Repository.Add(entity);

        await Repository.SaveChangesAsync();
    }
}