using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Modules.Users.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Users.Repositories;

public class UserRepository : RepositoryBase
{
    public UserRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<User?> GetUserByEmail(string email)
    {
        return await DbContext.Users.Where(e => e.EmailAddress == email).FirstOrDefaultAsync();
    }

    public async Task<User?> Get(string id)
    {
        return await DbContext.Users.Where(e => e.Id == id && e.Id == id).FirstOrDefaultAsync();
    }

    public async Task<bool> EmailExists(string email, bool ignoreCurrentUser = false)
    {
        var query = DbContext.Users.Where(e => e.EmailAddress == email);
        if (ignoreCurrentUser) query = query.Where(e => e.Id != RequestUser.Id);

        return await query.AnyAsync();
    }

    public async Task<UserVm?> GetUserVm(string id)
    {
        return await DbContext.Users.Where(e => e.Id == id && RequestUser.Id == id)
            .Select(e => new UserVm(e))
            .FirstOrDefaultAsync();
    }

    public async Task<User?> GetUserByPasswordResetCode(string code)
    {
        return await DbContext.Users.Where(e => e.PasswordResetCode == code).FirstOrDefaultAsync();
    }

    public async Task<User?> GetUserByPasswordEmailConfirmationCode(string code)
    {
        throw new NotImplementedException();
    }
}