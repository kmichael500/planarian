using Planarian.Model.Shared;
using Planarian.Modules.Settings.Models;
using Planarian.Modules.Settings.Repositories;
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

    public async Task<string> GetTagTypeName(string tagTypeId)
    {
        return await Repository.GetTagTypeName(tagTypeId);
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

    public async Task<IEnumerable<SelectListItem<string>>> GetStates()
    {
        return await Repository.GetStates();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetStateCounties(string stateId)
    {
        return await Repository.GetStateCounties(stateId);
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetTripTags()
    {
        return await Repository.GetTripTags();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetGeologyTags()
    {
        return await Repository.GetGeologyTags();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetLocationQualityTags()
    {
        return await Repository.GetLocationQualityTags();
    }

    public async Task<IEnumerable<SelectListItem<string>>?> GetEntranceStatusTags()
    {
        return await Repository.GetEntranceStatusTags();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetFieldIndicationTags()
    {
        return await Repository.GetFieldIndicationTags();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetEntranceHydrologyTags()
    {
        return await Repository.GetEntranceHydrologyTags();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetEntranceHydrologyFrequencyTags()
    {
        return await Repository.GetEntranceHydrologyFrequencyTags();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetFileTags()
    {
        return await Repository.GetFileTags();
    }

    public async Task<string?> GetCountyName(string countyId)
    {
        return await Repository.GetCountyId(countyId);
    }

    public async Task<string?> GetStateName(string stateId)
    {
        return await Repository.GetStateName(stateId);
    }
}