using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
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
using Planarian.Modules.Caves.Services;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Import.Models;
using Planarian.Modules.Notifications.Services;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Models;
using Planarian.Shared.Services;

namespace Planarian.Modules.Account.Services;

[Authorize(Policy = PermissionPolicyKey.Admin)]
public class AccountService : ServiceBase<AccountRepository>
{
    private readonly FileService _fileService;
    private readonly FileRepository _fileRepository;
    private readonly NotificationService _notificationService;
    private readonly TagRepository _tagRepository;
    private readonly FeatureSettingRepository _featureSettingRepository;
    private readonly CaveService _caveService;
    private readonly ArchiveJobCoordinator _archiveJobCoordinator;
    private readonly RequestThrottleService _requestThrottleService;
    private readonly TagTypeMergeExecutionRepository _tagMerge;
    private readonly TagTypeDeleteExecutionRepository _tagDelete;
    private readonly CountyReferenceLockRepository _countyLocks;
    private readonly CountyDeleteExecutionRepository _countyDelete;

    public AccountService(AccountRepository repository, RequestUser requestUser, FileService fileService,
        FileRepository fileRepository, NotificationService notificationService, TagRepository tagRepository,
        FeatureSettingRepository featureSettingRepository, CaveService caveService,
        ArchiveJobCoordinator archiveJobCoordinator, RequestThrottleService requestThrottleService,
        TagTypeMergeExecutionRepository tagMerge, TagTypeDeleteExecutionRepository tagDelete,
        CountyReferenceLockRepository countyLocks, CountyDeleteExecutionRepository countyDelete) : base(
        repository, requestUser)
    {
        _fileService = fileService;
        _fileRepository = fileRepository;
        _notificationService = notificationService;
        _tagRepository = tagRepository;
        _featureSettingRepository = featureSettingRepository;
        _caveService = caveService;
        _archiveJobCoordinator = archiveJobCoordinator;
        _requestThrottleService = requestThrottleService;
        _tagMerge = tagMerge;
        _tagDelete = tagDelete;
        _countyLocks = countyLocks;
        _countyDelete = countyDelete;
    }
    public async Task ResetAccount(CancellationToken cancellationToken)
    {
        await using var dbTransaction = await Repository.BeginTransactionAsync(cancellationToken);
        var fileObjects = await Repository.GetFileObjectAddressesForResetAsync(cancellationToken);

        var deleteAllCavesSignalRGroupName = $"{RequestUser.UserGroupPrefix}-DeleteAllCaves";
        await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
            "Getting associated files.");

        await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
            "Done getting associated files.");
        await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
            "Deleted 0 of 0 caves.");

        await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName, "Deleting associated cave permissions.");
        await Repository.DeleteAllCavePermissions();

        async void DeleteCavesProgressHandler(string message)
        {
            await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName, message);
        }

        await Repository.DeleteCaveWithRelatedData(new Progress<string>(DeleteCavesProgressHandler),
            cancellationToken);

        await Repository.DeleteAllTagTypes(new Progress<string>(DeleteCavesProgressHandler), cancellationToken);

        await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
            "Deleting associated counties");
        await Repository.DeleteAllCounties();
        await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
            "Finished deleting associated counties");

        await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
            "Deleting associated states");
        await Repository.DeleteAllAccountStates();
        await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
            "Finished deleting associated states");

        // Commit the relational purge before deleting external object storage. If object
        // cleanup fails, the durable database outcome must not be rolled back while the
        // bytes have already been destroyed.
        await dbTransaction.CommitAsync(cancellationToken);

        await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
            "Deleting associated files.");
        foreach (var fileObject in fileObjects)
            await _fileService.DeleteObjectBestEffortAsync(
                new StorageObjectAddress(fileObject.Partition, fileObject.Key));

        // Legacy account-owned assets (archives/photos/import transport) still share the
        // Azure account container. Keep that broader cleanup separate from the durable
        // provider-neutral Cave File contract above.
        await _fileService.DeleteContainer(RequestUser.AccountContainerName);

        await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
            "Done deleting associated files.");
    }

    #region Tags

    public async Task<IEnumerable<TagTypeTableVm>> GetTagsForTable(string tagTypeKey,
        CancellationToken cancellationToken)
    {
        return await Repository.GetTagsForTable(tagTypeKey, cancellationToken);
    }

    public async Task<TagTypeTableVm> CreateOrUpdateTagType(CreateEditTagTypeVm tag, string tagTypeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag.Name)) throw ApiExceptionDictionary.BadRequest("Name cannot be empty.");

        var isNewTagType = string.IsNullOrWhiteSpace(tagTypeId);
        var entity = !isNewTagType
            ? await _tagRepository.GetAccountAdministrativeTagAsync(tagTypeId, cancellationToken)
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
            _tagRepository.Add(entity);
        }

        await _tagRepository.SaveChangesAsync();

        var result = new TagTypeTableVm
        {
            TagTypeId = entity.Id,
            Name = entity.Name,
            IsUserModifiable = !string.IsNullOrWhiteSpace(entity.AccountId),
            Occurrences = await Repository.GetNumberOfOccurrences(entity.Id)
        };

        return result;
    }

    public async Task<int> DeleteTagTypes(IEnumerable<string> tagTypeIds)
    {
        var result = await _tagDelete.ExecuteAsync(tagTypeIds, CancellationToken.None);

        return result;
    }

    public async Task MergeTagTypes(string[] tagTypeIds, string destinationTagTypeId,
        CancellationToken cancellationToken)
    {
        await _tagMerge.ExecuteAsync(tagTypeIds, destinationTagTypeId, cancellationToken);
    }


    #endregion

    #region Counties
    public async Task<IEnumerable<TagTypeTableCountyVm>> GetCountiesForTable(string stateId,
        CancellationToken cancellationToken)
    {
        return await Repository.GetCountiesForTable(stateId, cancellationToken);
    }

    public async Task<TagTypeTableCountyVm> CreateOrUpdateCounty(string stateId, CreateCountyVm county,
        string? countyId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(county.Name)) throw ApiExceptionDictionary.BadRequest("Name cannot be empty.");
        if (string.IsNullOrWhiteSpace(county.CountyDisplayId))
            throw ApiExceptionDictionary.BadRequest("County Code cannot be empty.");

        var isNewCounty = string.IsNullOrWhiteSpace(countyId);
        if (isNewCounty)
        {
            await ValidateCountyMutationAsync(stateId, county.CountyDisplayId, null, cancellationToken);
            var entity = new County
            {
                Name = county.Name,
                DisplayId = county.CountyDisplayId,
                StateId = stateId,
                AccountId = RequestUser.AccountId ?? throw new InvalidOperationException()
            };
            Repository.Add(entity);
            await Repository.SaveChangesAsync();
            return CountyResult(entity);
        }

        await using var transaction = await Repository.BeginTransactionAsync(cancellationToken);
        try
        {
            var locked = await _countyLocks.LockForMutationAsync([countyId!], cancellationToken);
            if (locked.Count != 1) throw ApiExceptionDictionary.NotFound("County Id");

            var entity = await Repository.GetCounty(countyId, cancellationToken);
            if (entity == null) throw ApiExceptionDictionary.NotFound("County Id");

            await ValidateCountyMutationAsync(stateId, county.CountyDisplayId, entity.Id, cancellationToken);
            if (entity.StateId != stateId &&
                await Repository.IsCountyReferencedByCave(entity.Id, cancellationToken))
                throw ApiExceptionDictionary.BadRequest(
                    "Cannot move county to another state because it is in use.");

            entity.Name = county.Name;
            entity.DisplayId = county.CountyDisplayId;
            entity.StateId = stateId;
            await Repository.SaveChangesAsync();
            await transaction.CommitAsync(cancellationToken);
            return CountyResult(entity);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task ValidateCountyMutationAsync(string stateId, string displayId, string? excludedCountyId,
        CancellationToken cancellationToken)
    {
        if (!await Repository.StateExistsAsync(stateId, cancellationToken))
            throw ApiExceptionDictionary.BadRequest("The selected State does not exist.");
        if (await Repository.IsDuplicateCountyCode(displayId, stateId, excludedCountyId, cancellationToken))
            throw ApiExceptionDictionary.BadRequest(
                $"The county code '{displayId}' is already in use for the selected state.");
    }

    private static TagTypeTableCountyVm CountyResult(County entity) => new()
    {
        TagTypeId = entity.Id,
        Name = entity.Name,
        IsUserModifiable = true,
        Occurrences = 0,
        CountyDisplayId = entity.DisplayId
    };

    public async Task DeleteCounties(IEnumerable<string> countyIds, CancellationToken cancellationToken)
    {
        await _countyDelete.ExecuteAsync(countyIds, cancellationToken);
    }

    public async Task MergeCounties(string[] countyIds, string destinationCountyId, CancellationToken cancellationToken)
    {
        // TODO: County merge must be implemented as a published bulk Cave mutation that validates State/County and
        // County-number invariants and atomically advances every affected Cave revision.
        throw new NotImplementedException();
    }

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

        await Repository.SaveChangesAsync(cancellationToken);
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
        var transaction = await Repository.BeginTransactionAsync(cancellationToken);
        var account = await Repository.GetAccount(cancellationToken);

        if (account == null) throw ApiExceptionDictionary.NotFound("Account");

        try
        {
            account.Name = values.AccountName;
            account.CountyIdDelimiter = values.CountyIdDelimiter;

            account.DefaultViewAccessAllCaves = values.DefaultViewAccessAllCaves;
            account.ExportEnabled = values.ExportEnabled;

            // check which states are missing
            var newStateIds = values.StateIds.Except(account.AccountStates.Select(x => x.StateId)).ToList();

            // check which states are new
            var deletedStateIds = account.AccountStates.Select(x => x.StateId).Except(values.StateIds).ToList();

            foreach (var deletedStateId in deletedStateIds)
            {
                var numberOfCavesForState =
                    await Repository.GetNumberOfCavesForState(deletedStateId, cancellationToken);

                if (numberOfCavesForState > 0)
                {
                    throw ApiExceptionDictionary.BadRequest(
                        $"One or more states have caves associated with them. Please remove the caves before removing the state.");
                }
                var accountState = await Repository.GetAccountState(account.Id, deletedStateId);
                if (accountState == null)
                {
                    throw ApiExceptionDictionary.NotFound("Account State");
                }

                Repository.Delete(accountState);
                await Repository.SaveChangesAsync(cancellationToken);
            }

            foreach (var newStateId in newStateIds)
            {
                var state = new AccountState()
                {
                    StateId = newStateId,
                    AccountId = RequestUser.AccountId ?? throw new InvalidOperationException()
                };

                Repository.Add(state);
            }

            await Repository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return account.Id;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
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
