using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Authentication.Repositories;

public class AuthenticationRepository : RepositoryBase
{
    public AuthenticationRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext,
        requestUser)
    {
    }

    public async Task<IEnumerable<string>> GetAccountIdsByUserId(string userId)
    {
        return await DbContext.AccountUsers
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Account!.Name)
            .Select(e => e.AccountId)
            .ToListAsync();
    }
}