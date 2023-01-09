using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.Authentication.Repositories;

public class AuthenticationRepository : RepositoryBase
{
    public AuthenticationRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext,
        requestUser)
    {
    }
    
 

    public async Task<UserToken?> GetUserForToken(string userId)
    {
        var user = await DbContext.Users.Where(e => e.Id == userId).Select(e => new UserToken(e.FullName, e.Id))
            .FirstOrDefaultAsync();
        return user;
    }
}