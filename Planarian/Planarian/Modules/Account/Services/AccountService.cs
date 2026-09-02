using Microsoft.AspNetCore.Authorization;
using Azure;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Archive.Models;
using Planarian.Modules.Account.Archive.Services;
using Planarian.Modules.Account.Controller;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Import.Models;
using Planarian.Modules.Notifications.Services;
using Planarian.Shared.Base;
using Planarian.Shared.Models;
using Planarian.Shared.Services;

namespace Planarian.Modules.Account.Services;

[Authorize(Policy = PermissionPolicyKey.Admin)]
public class AccountService : ServiceBase<AccountRepository>
{
    private readonly FileService _fileService;
    private readonly NotificationService _notificationService;
    private readonly FeatureSettingRepository _featureSettingRepository;
    private readonly ArchiveJobCoordinator _archiveJobCoordinator;
    private readonly RequestThrottleService _requestThrottleService;
    private readonly TagTypeAdministrationRepository _tagTypes;
    private readonly CountyAdministrationRepository _counties;

    public AccountService(AccountRepository repository, RequestUser requestUser, FileService fileService,
        NotificationService notificationService, FeatureSettingRepository featureSettingRepository,
        ArchiveJobCoordinator archiveJobCoordinator, RequestThrottleService requestThrottleService,
        TagTypeAdministrationRepository tagTypes, CountyAdministrationRepository counties) : base(repository, requestUser)
    {
        _fileService = fileService;
        _notificationService = notificationService;
        _featureSettingRepository = featureSettingRepository;
        _archiveJobCoordinator = archiveJobCoordinator;
        _requestThrottleService = requestThrottleService;
        _tagTypes = tagTypes;
        _counties = counties;
    }
    public async Task ResetAccount(CancellationToken cancellationToken)
    {
        var group = $"{RequestUser.UserGroupPrefix}-DeleteAllCaves";
        await _notificationService.SendNotificationToGroupAsync(group, "Resetting cave and account data.");
        var fileObjects = await Repository.ExecuteResetAsync(cancellationToken);
        await _notificationService.SendNotificationToGroupAsync(group, "Finished resetting cave and account data.");

        await _notificationService.SendNotificationToGroupAsync(group, "Deleting associated files.");
        foreach (var fileObject in fileObjects)
            await _fileService.DeleteObjectBestEffortAsync(
                new StorageObjectAddress(fileObject.Partition, fileObject.Key));

        // Legacy account-owned assets (archives/photos/import transport) still share the
        // Azure account container. Keep that cleanup after the relational reset commits.
        await _fileService.DeleteContainer(RequestUser.AccountContainerName);
        await _notificationService.SendNotificationToGroupAsync(group, "Done deleting associated files.");
    }

    #region Tags

    public async Task<IEnumerable<TagTypeTableVm>> GetTagsForTable(string tagTypeKey,
        CancellationToken cancellationToken)
    {
        return await _tagTypes.GetTagsForTable(tagTypeKey, cancellationToken);
    }

    public async Task<TagTypeTableVm> CreateOrUpdateTagType(CreateEditTagTypeVm tag, string tagTypeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag.Name)) throw ApiExceptionDictionary.BadRequest("Name cannot be empty.");

        var isNewTagType = string.IsNullOrWhiteSpace(tagTypeId);
        var entity = !isNewTagType
            ? await _tagTypes.GetAccountAdministrativeTagAsync(tagTypeId, cancellationToken)
            : new TagType();

        if (entity == null) throw ApiExceptionDictionary.NotFound("Tag Type Id");

        if (entity.IsDefault) throw ApiExceptionDictionary.Forbidden("Cannot modify default tag types.");


        entity.Name = tag.Name;

        if (isNewTagType)
        {
            if (!TagTypeKeyConstant.IsValidAccountTagKey(tag.Key))
                throw ApiExceptionDictionary.BadRequest("Invalid tag key.");

            entity.Key = tag.Key;
            entity.AccountId = RequestUser.AccountId;
            _tagTypes.Add(entity);
        }

        await _tagTypes.SaveChangesAsync(cancellationToken);

        var result = new TagTypeTableVm
        {
            TagTypeId = entity.Id,
            Name = entity.Name,
            IsUserModifiable = !string.IsNullOrWhiteSpace(entity.AccountId),
            Occurrences = await _tagTypes.GetNumberOfOccurrences(entity.Id, cancellationToken)
        };

        return result;
    }

    public async Task<int> DeleteTagTypes(IEnumerable<string> tagTypeIds)
    {
        var result = await _tagTypes.DeleteAsync(tagTypeIds, CancellationToken.None);

        return result;
    }

    public async Task MergeTagTypes(string[] tagTypeIds, string destinationTagTypeId,
        CancellationToken cancellationToken)
    {
        await _tagTypes.MergeAsync(tagTypeIds, destinationTagTypeId, cancellationToken);
    }


    #endregion

    #region Counties
    public async Task<IEnumerable<TagTypeTableCountyVm>> GetCountiesForTable(string stateId,
        CancellationToken cancellationToken)
    {
        return await _counties.GetCountiesForTable(stateId, cancellationToken);
    }

    public async Task<TagTypeTableCountyVm> CreateOrUpdateCounty(string stateId, CreateCountyVm county,
        string? countyId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(county.Name)) throw ApiExceptionDictionary.BadRequest("Name cannot be empty.");
        if (string.IsNullOrWhiteSpace(county.CountyDisplayId))
            throw ApiExceptionDictionary.BadRequest("County Code cannot be empty.");

        var result = await _counties.CreateOrUpdateAsync(stateId, county.Name, county.CountyDisplayId, countyId,
            cancellationToken);
        return new TagTypeTableCountyVm
        {
            TagTypeId = result.Id,
            Name = result.Name,
            IsUserModifiable = true,
            Occurrences = 0,
            CountyDisplayId = result.DisplayId
        };
    }

    public async Task DeleteCounties(IEnumerable<string> countyIds, CancellationToken cancellationToken)
    {
        await _counties.DeleteAsync(countyIds, cancellationToken);
    }

    public Task MergeCounties(string stateId, string[] countyIds, string destinationCountyId,
        CancellationToken cancellationToken) =>
        _counties.MergeAsync(stateId, countyIds, destinationCountyId, cancellationToken);

    #endregion

    #region Settings

    public async Task<IEnumerable<FeatureSettingVm>> GetFeatureSettings(CancellationToken cancellationToken)
    {
        var featureSettings = (await _featureSettingRepository.GetFeatureSettings(cancellationToken)).ToList();

        return featureSettings;
    }

    public async Task UpdateFeatureSetting(FeatureKey key, bool isEnabled, CancellationToken cancellationToken)
    {

        var featureSetting = await _featureSettingRepository.GetFeatureSetting(key, cancellationToken);
        var isNew = featureSetting == null;

        if (!isNew && featureSetting!.IsDefault)
            throw ApiExceptionDictionary.Forbidden("Cannot modify default feature settings.");

        featureSetting ??= new FeatureSetting
        {
            AccountId = RequestUser.AccountId,
            Key = key
        };
        featureSetting.IsEnabled = isEnabled;

        if (isNew)
        {
            _featureSettingRepository.Add(featureSetting);
        }

        await _featureSettingRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetAllStates(CancellationToken cancellationToken)
    {
        return await Repository.GetAllStates(cancellationToken);
    }

    public async Task<MiscAccountSettingsVm?> GetMiscAccountSettingsVm(CancellationToken cancellationToken)
    {
        return await Repository.GetMiscAccountSettingsVm(cancellationToken);
    }

    public async Task<string> UpdateMiscAccountSettingsVm(MiscAccountSettingsVm values,
        CancellationToken cancellationToken)
    {
        var account = await Repository.GetAccount(cancellationToken);
        if (account == null) throw ApiExceptionDictionary.NotFound("Account");

        account.Name = values.AccountName;
        account.CountyIdDelimiter = values.CountyIdDelimiter;
        account.DefaultViewAccessAllCaves = values.DefaultViewAccessAllCaves;
        account.ExportEnabled = values.ExportEnabled;

        var newStateIds = values.StateIds.Except(account.AccountStates.Select(x => x.StateId)).ToList();
        var deletedStateIds = account.AccountStates.Select(x => x.StateId).Except(values.StateIds).ToList();

        foreach (var deletedStateId in deletedStateIds)
        {
            if (await Repository.GetNumberOfCavesForState(deletedStateId, cancellationToken) > 0)
                throw ApiExceptionDictionary.BadRequest(
                    "One or more states have caves associated with them. Please remove the caves before removing the state.");

            var accountState = await Repository.GetAccountState(account.Id, deletedStateId)
                ?? throw ApiExceptionDictionary.NotFound("Account State");
            Repository.Delete(accountState);
        }

        foreach (var newStateId in newStateIds)
            Repository.Add(new AccountState
            {
                StateId = newStateId,
                AccountId = RequestUser.AccountId ?? throw new InvalidOperationException()
            });

        await Repository.SaveChangesAsync(cancellationToken);
        return account.Id;
    }

    #region Archive

    public void StartArchive()
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
        {
            throw ApiExceptionDictionary.NoAccount;
        }

        var started = _archiveJobCoordinator.StartArchiveJob(RequestUser.AccountId, RequestUser.AccountContainerName);
        if (!started)
        {
            throw ApiExceptionDictionary.BadRequest("An archive is already running for this account.");
        }
    }

    public void CancelArchive()
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
        {
            throw ApiExceptionDictionary.NoAccount;
        }

        _archiveJobCoordinator.CancelArchiveJob(RequestUser.AccountId);
    }

    public ArchiveProgressVm? GetArchiveStatus()
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
        {
            throw ApiExceptionDictionary.NoAccount;
        }

        return _archiveJobCoordinator.GetArchiveStatus(RequestUser.AccountId);
    }

    public async Task<IEnumerable<ArchiveListItemVm>> GetRecentArchives(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
        {
            throw ApiExceptionDictionary.NoAccount;
        }

        var accountBlobContainerClient = await _fileService.GetBlobContainerClient(RequestUser.AccountContainerName);
        var archiveBlobs = new List<(string BlobKey, DateTimeOffset CreatedAt)>();
        var activeArchiveBlobKey = _archiveJobCoordinator.GetActiveArchiveBlobKey(RequestUser.AccountId);

        try
        {
            await foreach (var blobItem in accountBlobContainerClient.GetBlobsAsync(prefix: "archives/", cancellationToken: cancellationToken))
            {
                if (blobItem.Name.StartsWith(ArchiveJobCoordinator.TempArchivePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(blobItem.Name, activeArchiveBlobKey, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!blobItem.Properties.LastModified.HasValue)
                {
                    continue;
                }

                archiveBlobs.Add((blobItem.Name, blobItem.Properties.LastModified.Value));
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return [];
        }

        var recentArchives = archiveBlobs
            .OrderByDescending(x => x.CreatedAt)
            .Take(ArchiveJobCoordinator.MaxRetainedArchives)
            .ToList();

        var result = new List<ArchiveListItemVm>(recentArchives.Count);
        foreach (var archiveBlob in recentArchives)
        {
            result.Add(new ArchiveListItemVm
            {
                BlobKey = archiveBlob.BlobKey,
                FileName = Path.GetFileName(archiveBlob.BlobKey),
                CreatedAt = archiveBlob.CreatedAt
            });
        }

        return result;
    }

    public async Task<AuthenticatedFileResponse> CreateArchiveDownloadResponse(string blobKey,
        CancellationToken cancellationToken)
    {
        EnsureValidArchiveBlobKey(blobKey);
        EnsureArchiveBlobIsNotActive(blobKey, "Archive is not available while it is still running.");

        await _requestThrottleService.CountAttempt(ThrottleProfile.FileAccess, blobKey);

        return await _fileService.CreateBlobResponse(
            blobKey,
            RequestUser.AccountContainerName,
            Path.GetFileName(blobKey),
            isDownload: true,
            cancellationToken: cancellationToken);
    }

    public async Task DeleteArchive(string blobKey, CancellationToken cancellationToken)
    {
        EnsureValidArchiveBlobKey(blobKey);
        EnsureArchiveBlobIsNotActive(blobKey, "Cannot delete an archive while it is still running.");

        await _fileService.DeleteFile(blobKey, RequestUser.AccountContainerName);
    }

    private void EnsureValidArchiveBlobKey(string blobKey)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
        {
            throw ApiExceptionDictionary.NoAccount;
        }

        if (string.IsNullOrWhiteSpace(blobKey) ||
            !blobKey.StartsWith(ArchiveJobCoordinator.ArchivePrefix, StringComparison.OrdinalIgnoreCase) ||
            blobKey.StartsWith(ArchiveJobCoordinator.TempArchivePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw ApiExceptionDictionary.BadRequest("Invalid archive.");
        }
    }

    private void EnsureArchiveBlobIsNotActive(string blobKey, string activeArchiveMessage)
    {
        var activeArchiveBlobKey = _archiveJobCoordinator.GetActiveArchiveBlobKey(RequestUser.AccountId!);
        if (string.Equals(blobKey, activeArchiveBlobKey, StringComparison.Ordinal))
        {
            throw ApiExceptionDictionary.BadRequest(activeArchiveMessage);
        }
    }
    #endregion

    #endregion
}
