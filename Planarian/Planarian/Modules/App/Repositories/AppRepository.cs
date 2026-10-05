using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.App.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.App.Repositories;

public class AppRepository : RepositoryBase
{
    public AppRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext,
        requestUser)
    {
    }

    public async Task<List<SelectListItem<string>>> GetAccountIds()
    {
        return await DbContext.AccountUsers
            .Where(e => e.UserId == RequestUser.Id && e.AccessRevokedOn == null)
            .OrderByDescending(e => e.Account!.Name)
            .Select(e => new SelectListItem<string> { Display = e.Account!.Name, Value = e.AccountId })
            .ToListAsync();
    }

    public async Task<List<RevokedAccountVm>> GetRevokedAccountIds()
    {
        return await DbContext.AccountUsers
            .Where(e => e.UserId == RequestUser.Id && e.AccessRevokedOn != null)
            .OrderByDescending(e => e.Account!.Name)
            .Select(e => new RevokedAccountVm
            {
                Display = e.Account!.Name,
                Value = e.AccountId,
                Reason = e.AccessRevokedReason
            })
            .ToListAsync();
    }
}