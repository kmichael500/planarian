using Planarian.Model.Shared;
using Planarian.Modules.Settings.Repositories;
using Planarian.Modules.Users.Models;
using Planarian.Shared.Base;
using Planarian.Shared.Services;

namespace Planarian.Modules.Settings.Services;

public class SettingsService : ServiceBase<SettingsRepository>
{
    private readonly BlobService _blobService;

    public SettingsService(SettingsRepository repository, RequestUser requestUser, BlobService blobService) : base(
        repository, requestUser)
    {
        _blobService = blobService;
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetObjectiveTypes()
    {
        return await Repository.GetObjectiveTypes();
    }

    public async Task<string> GetObjectiveTypeName(string objectiveTypeId)
    {
        return await Repository.GetObjectiveTypeName(objectiveTypeId);
    }

    public async Task<NameProfilePhotoVm> GetUsersName(string userId)
    {
        var user = await Repository.GetUserNameProfilePhoto(userId);

        if (string.IsNullOrWhiteSpace(user.BlobKey)) return user;
        
        
        var url = _blobService.GetSasUrl(user.BlobKey);
        user.ProfilePhotoUrl = url?.AbsoluteUri;

        return user;
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetUsers()
    {
        return await Repository.GetUsers();
    }
}