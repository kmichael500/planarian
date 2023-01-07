using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
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
}