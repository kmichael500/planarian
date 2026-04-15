using Planarian.Model.Shared;
using Planarian.Modules.Settings.Repositories;
using Planarian.Shared.Base;

namespace Planarian.Modules.Settings.Services;

public class SettingsService : ServiceBase<SettingsRepository>
{
    public SettingsService(SettingsRepository repository, RequestUser requestUser) : base(
        repository, requestUser)
    {
    }

    public async Task<string> GetTagTypeName(string tagTypeId)
    {
        return await Repository.GetTagTypeName(tagTypeId);
    }

    public async Task<string?> GetCountyName(string countyId)
    {
        return await Repository.GetCountyId(countyId);
    }

    public async Task<string?> GetStateName(string stateId)
    {
        return await Repository.GetStateName(stateId);
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetUsers()
    {
        return await Repository.GetUsers();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetStates(string? permissionKey = null)
    {
        return await Repository.GetStates();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetStateCounties(string stateId,
        string? permissionKey = null)
    {
        return await Repository.GetStateCounties(stateId, permissionKey);
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetTags(string key, string? projectId = null)
    {
        return await Repository.GetTags(key, projectId);
    }

}
