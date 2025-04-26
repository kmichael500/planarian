using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Settings.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Settings.Repositories;

public class SettingsRepository<TDbContext> : RepositoryBase<TDbContext> where TDbContext : PlanarianDbContextBase
{
    public SettingsRepository(TDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<string> GetTagTypeNameOrFail(string tagTypeId)
    {
        return await DbContext.TagTypes
            .Where(e => e.Id == tagTypeId && (e.AccountId == RequestUser.AccountId || e.IsDefault)).Select(e => e.Name)
            .FirstAsync();
    }
    
    public async Task<string?> GetTagTypeName(string tagTypeId)
    {
        return await DbContext.TagTypes
            .Where(e => e.Id == tagTypeId && (e.AccountId == RequestUser.AccountId || e.IsDefault)).Select(e => e.Name)
            .FirstOrDefaultAsync();
    }

    public async Task<NameProfilePhotoVm> GetUserNameProfilePhoto(string userId)
    {
        return await DbContext.Users.Where(e => e.Id == userId)
            .Select(e => new NameProfilePhotoVm(e.FullName, e.ProfilePhotoBlobKey)).FirstAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetUsers()
    {
        return await DbContext.Users.Select(e => new SelectListItem<string>(e.FullName, e.Id)).ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetStates(string? permissionKey = null)
    {
        return await DbContext.AccountStates.Where(e=>e.AccountId == RequestUser.AccountId)
            .Select(accountState => new SelectListItem<string>(accountState.State!.Name, accountState.State.Id))
            .ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetStateCounties(string stateId,
        string? permissionKey = null)
    {
        var query =
            DbContext.UserCavePermissionView.Where(e =>
                e.AccountId == RequestUser.AccountId
                && e.UserId == RequestUser.Id
            );

        IQueryable<County> countyQuery;
        if (permissionKey == PermissionKey.Manager)
        {
            countyQuery = query.Where(e => e.Permission.Key == PermissionKey.Manager)
                .Select(e => e.County!);
        }
        else if (permissionKey == PermissionKey.View)
        {
            countyQuery = query
                .Select(e => e.Cave!.County!);
        }
        else
        {
            countyQuery = DbContext.Counties.Where(e => e.AccountId == RequestUser.AccountId);
        }

        return await countyQuery
            .Where(e => e.StateId == stateId)
            .Distinct()
            .Select(e => new SelectListItem<string>(e.Name, e.Id))
            .ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetTags(string key, string? projectId = null)
    {
        return string.IsNullOrWhiteSpace(projectId)
            // Cave Tags
            ? await DbContext.TagTypes
                .Where(e => e.Key == key && (e.AccountId == RequestUser.AccountId || e.IsDefault))
                .Select(e => new SelectListItem<string>(e.Name, e.Id)).ToListAsync()
            // Project Tags
            : await DbContext.TagTypes
                .Where(e => e.Key == key && e.AccountId == null &&
                            e.ProjectId == projectId)
                .Select(e => new SelectListItem<string>(e.Name, e.Id)).ToListAsync();
    }

    public async Task<string?> GetCountyName(string countyId)
    {
        return await DbContext.Counties.Where(e => e.Id == countyId && e.AccountId == RequestUser.AccountId)
            .Select(e => e.Name).FirstOrDefaultAsync();
    }

    public async Task<string?> GetStateName(string stateId)
    {
        return await DbContext.States.Where(e => e.Id == stateId).Select(e => e.Name).FirstOrDefaultAsync();
    }

    public async Task<State?> GetStateByNameOrAbbreviation(string caveRecordState)
    {
        var state = await DbContext.States.Where(e => e.Name == caveRecordState || e.Abbreviation == caveRecordState)
            .FirstOrDefaultAsync();

        return state;
    }

    public async Task<County?> GetCountyByDisplayId(string displayId, string stateId)
    {
        var county = await DbContext.Counties
            .Where(e => e.DisplayId == displayId && e.StateId == stateId).FirstOrDefaultAsync();

        return county;
    }
}

public class SettingsRepository : SettingsRepository<PlanarianDbContext>
{
    public SettingsRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }
}