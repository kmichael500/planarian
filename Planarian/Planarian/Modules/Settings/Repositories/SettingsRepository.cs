using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Settings.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Settings.Repositories;

public class SettingsRepository : RepositoryBase
{
    public SettingsRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetTripTags()
    {
        return await DbContext.TagTypes.Select(e => new SelectListItem<string>(e.Name, e.Id)).ToListAsync();
    }

    public async Task<string> GetTagTypeName(string tagTypeId)
    {
        return await DbContext.TagTypes.Where(e => e.Id == tagTypeId).Select(e => e.Name).FirstAsync();
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

    public async Task<IEnumerable<SelectListItem<string>>> GetStates()
    {
        return await DbContext.AccountStates.Where(e => e.AccountId == RequestUser.AccountId)
            .Select(e => e.State)
            .Select(e => new SelectListItem<string>(e.Name, e.Id)).ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetStateCounties(string stateId)
    {
        return await DbContext.Counties.Where(e => e.StateId == stateId && e.AccountId == RequestUser.AccountId)
            .Select(e => new SelectListItem<string>(e.Name, e.Id)).ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetGeologyTags()
    {
        return await DbContext.TagTypes
            .Where(e => e.Key == TagTypeKeyConstant.Geology && e.AccountId == RequestUser.AccountId)
            .Select(e => new SelectListItem<string>(e.Name, e.Id)).ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetLocationQualityTags()
    {
        return await DbContext.TagTypes
            .Where(e => e.Key == TagTypeKeyConstant.LocationQuality &&
                        e.AccountId == RequestUser.AccountId)
            .Select(e => new SelectListItem<string>(e.Name, e.Id)).ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetEntranceStatusTags()
    {
        return await DbContext.TagTypes.Where(e =>
                e.Key == TagTypeKeyConstant.EntranceStatus &&
                (e.AccountId == RequestUser.AccountId || string.IsNullOrWhiteSpace(e.AccountId)))
            .Select(e => new SelectListItem<string>(e.Name, e.Id)).ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetFieldIndicationTags()
    {
        return await DbContext.TagTypes.Where(e =>
                e.Key == TagTypeKeyConstant.FieldIndication &&
                (e.AccountId == RequestUser.AccountId || string.IsNullOrWhiteSpace(e.AccountId)))
            .Select(e => new SelectListItem<string>(e.Name, e.Id)).ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetEntranceHydrologyTags()
    {
        return await DbContext.TagTypes.Where(e =>
                e.Key == TagTypeKeyConstant.EntranceHydrology &&
                (e.AccountId == RequestUser.AccountId || string.IsNullOrWhiteSpace(e.AccountId)))
            .Select(e => new SelectListItem<string>(e.Name, e.Id)).ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetEntranceHydrologyFrequencyTags()
    {
        return await DbContext.TagTypes.Where(e =>
                e.Key == TagTypeKeyConstant.EntranceHydrologyFrequency &&
                (e.AccountId == RequestUser.AccountId || string.IsNullOrWhiteSpace(e.AccountId)))
            .Select(e => new SelectListItem<string>(e.Name, e.Id)).ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetFileTags()
    {
        return await DbContext.TagTypes.Where(e =>
                e.Key == TagTypeKeyConstant.File &&
                (e.AccountId == RequestUser.AccountId || string.IsNullOrWhiteSpace(e.AccountId)))
            .Select(e => new SelectListItem<string>(e.Name, e.Id)).ToListAsync();
    }

    public async Task<string?> GetCountyId(string countyId)
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