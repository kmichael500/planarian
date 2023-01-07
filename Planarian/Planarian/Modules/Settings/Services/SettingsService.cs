using Planarian.Model.Shared;
using Planarian.Modules.Settings.Repositories;
using Planarian.Shared.Base;

namespace Planarian.Modules.Settings.Services;

public class SettingsService : ServiceBase<SettingsRepository>
{
    public SettingsService(SettingsRepository repository, RequestUser requestUser) : base(repository, requestUser)
    {
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetObjectiveTypes()
    {
        return await Repository.GetObjectiveTypes();
    }

    public async Task<string> GetObjectiveTypeName(string objectiveTypeId)
    {
        return await Repository.GetObjectiveTypeName(objectiveTypeId);
    }

    public async Task<string> GetUsersName(string userId)
    {
        return await Repository.GetUsersName(userId);
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetUsers()
    {
        return await Repository.GetUsers();
    }
}