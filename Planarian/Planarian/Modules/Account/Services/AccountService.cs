using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Account.Controller;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Services;
using Planarian.Modules.FeatureSettings.Repositories;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Notifications.Services;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Base;

namespace Planarian.Modules.Account.Services;

[Authorize(Policy = PermissionPolicyKey.Admin)]
public class AccountService : ServiceBase<AccountRepository>
{
    private readonly FileService _fileService;
    private readonly FileRepository _fileRepository;
    private readonly NotificationService _notificationService;
    private readonly TagRepository _tagRepository;
    private readonly FeatureSettingRepository _featureSettingRepository;
    private readonly CaveRepository _caveRepository;

    public AccountService(AccountRepository repository, RequestUser requestUser, FileService fileService,
        FileRepository fileRepository, NotificationService notificationService, TagRepository tagRepository, FeatureSettingRepository featureSettingRepository, CaveRepository caveRepository) : base(
        repository, requestUser)
    {
        _fileService = fileService;
        _fileRepository = fileRepository;
        _notificationService = notificationService;
        _tagRepository = tagRepository;
        _featureSettingRepository = featureSettingRepository;
        _caveRepository = caveRepository;
    }
    public async Task<string> CreateAccount(CreateAccountVm account, CancellationToken cancellationToken)
    {
        var entity = new Planarian.Model.Database.Entities.RidgeWalker.Account
        {
            Name = account.Name,
        };
        
        foreach (var key in Enum.GetValues<FeatureKey>())
        {
            var isEnabled = key switch
            {
                FeatureKey.EnabledFieldCaveGeologicAgeTags => false,
                _ => true
            };

            entity.FeatureSettings.Add(new FeatureSetting
            {
                AccountId = entity.Id,
                Key = key,
                IsEnabled = isEnabled
            });
        }

        entity.AccountUsers.Add(new AccountUser
        {
            UserId = RequestUser.Id,
        });
        

        Repository.Add(entity);
        await Repository.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    public async Task ResetAccount(CancellationToken cancellationToken)
    {
        var dbTransaction = await Repository.BeginTransactionAsync(cancellationToken);
        
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

            await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
                "Deleting associated files.");
            await _fileService.DeleteContainer(RequestUser.AccountContainerName);

            await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
                "Done deleting associated files.");

            await dbTransaction.CommitAsync(cancellationToken);
    }

    #region Tags

    public async Task<IEnumerable<TagTypeTableVm>> GetTagsForTable(string tagTypeKey,
        CancellationToken cancellationToken)
    {
        return await Repository.GetTagsForTable(tagTypeKey, cancellationToken);
    }

    public async Task<TagTypeTableVm> CreateOrUpdateTagType(CreateEditTagTypeVm tag, string tagTypeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tag.Name)) throw ApiExceptionDictionary.BadRequest("Name cannot be empty.");
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw ApiExceptionDictionary.NoAccount;

        var transaction = await Repository.BeginTransactionAsync(cancellationToken);

        try
        {
            var isNewTagType = string.IsNullOrWhiteSpace(tagTypeId);
            var entity = !isNewTagType ? await _tagRepository.GetTag(tagTypeId) : new TagType();

            if (entity == null) throw ApiExceptionDictionary.NotFound("Tag Type Id");

            if (entity.IsDefault) throw ApiExceptionDictionary.Unauthorized("Cannot modify default tag types.");

            var oldTagName = entity.Name;

            entity.Name = tag.Name;

            if (isNewTagType)
            {
                if (!TagTypeKeyConstant.IsValidAccountTagKey(tag.Key))
                    throw ApiExceptionDictionary.BadRequest("Invalid tag key.");

                entity.Key = tag.Key;
                entity.AccountId = RequestUser.AccountId;
                _tagRepository.Add(entity);
            }
            
            await _tagRepository.SaveChangesAsync(cancellationToken);
            
            var tagTypesAffected = new List<(string CaveLogPropertyName, bool IsCaveTag, bool IsEntranceTag)>();
            switch (entity.Key)
            {
                case TagTypeKeyConstant.LocationQuality:
                    tagTypesAffected.Add((CaveLogPropertyNames.EntranceLocationQualityTagName, false, true));
                    break;
                case TagTypeKeyConstant.Geology:
                    tagTypesAffected.Add((CaveLogPropertyNames.GeologyTagName, true, false));
                    break;
                case TagTypeKeyConstant.EntranceStatus:
                    tagTypesAffected.Add((CaveLogPropertyNames.EntranceStatusTagName, false, true));
                    break;
                case TagTypeKeyConstant.FieldIndication:
                    tagTypesAffected.Add((CaveLogPropertyNames.EntranceFieldIndicationTagName, false, true));
                    break;
                case TagTypeKeyConstant.EntranceHydrology:
                    tagTypesAffected.Add((CaveLogPropertyNames.EntranceHydrologyTagName, false, true));
                    break;
                case TagTypeKeyConstant.File:
                    tagTypesAffected.Add((CaveLogPropertyNames.FileName, true, false));
                    break;
                case TagTypeKeyConstant.People:
                    tagTypesAffected.Add((CaveLogPropertyNames.CartographerNameTagName, true, false));
                    tagTypesAffected.Add((CaveLogPropertyNames.ReportedByNameTagName, true, false));
                    tagTypesAffected.Add((CaveLogPropertyNames.EntranceReportedByNameTagName, false, true));
                    break;
                case TagTypeKeyConstant.Biology:
                    tagTypesAffected.Add((CaveLogPropertyNames.BiologyTagName, true, false));
                    break;
                case TagTypeKeyConstant.Archeology:
                    tagTypesAffected.Add((CaveLogPropertyNames.ArcheologyTagName, true, false));
                    break;
                case TagTypeKeyConstant.MapStatus:
                    tagTypesAffected.Add((CaveLogPropertyNames.MapStatusTagName, true, false));
                    break;
                case TagTypeKeyConstant.CaveOther:
                    tagTypesAffected.Add((CaveLogPropertyNames.OtherTagName, true, false));
                    break;
                case TagTypeKeyConstant.GeologicAge:
                    tagTypesAffected.Add((CaveLogPropertyNames.GeologicAgeTagName, true, false));
                    break;
                case TagTypeKeyConstant.PhysiographicProvince:
                    tagTypesAffected.Add((CaveLogPropertyNames.PhysiographicProvinceTagName, true, false));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entity.Key), "Unknown tag type key");
            }

            var renameRequest = new CaveChangeRequest
            {
                Id = IdGenerator.Generate(),
                AccountId = RequestUser.AccountId,
                Type = ChangeRequestType.Rename,
                Status = ChangeRequestStatus.Approved,
                ReviewedOn = DateTime.UtcNow,
                CaveId = null,
                ReviewedByUserId = RequestUser.Id,
                Notes = null
            };

            var builder = new ChangeLogBuilder(
                accountId: RequestUser.AccountId,
                caveId: null,
                changedByUserId: RequestUser.Id,
                approvedByUserId: RequestUser.Id,
                changeRequestId: renameRequest.Id
            );

            foreach (var tagTypeAffected in tagTypesAffected)
            {
                if (tagTypeAffected.IsCaveTag)
                {
                    var cavesAffected =
                        (await _tagRepository.GetAffectedCaveTagsIEnumerable(tagTypeId, cancellationToken)).ToList();

                    if (cavesAffected.Count != 0)
                    {
                        foreach (var caveId in cavesAffected)
                        {
                            builder.AddNamedArrayField(tagTypeAffected.CaveLogPropertyName, [(entity.Id, oldTagName)], [
                                (entity.Id, tag.Name)
                            ], overrideCaveId: caveId);
                        }
                    }
                }
                if (!tagTypeAffected.IsEntranceTag) continue;
                
                var entrancesAffected =
                    (await _tagRepository.GetEntrancesWithTagType(tagTypeId, cancellationToken)).ToList();
                if (entrancesAffected.Count == 0) continue;
                    
                foreach (var entrance in entrancesAffected)
                {
                    if (tagTypeAffected.CaveLogPropertyName == CaveLogPropertyNames.EntranceLocationQualityTagName)
                    {

                        builder.AddNamedIdFieldAsync(tagTypeAffected.CaveLogPropertyName,
                            (entity.Id, oldTagName), (entity.Id, entity.Name), entranceId: entrance.EntranceId,
                            overrideCaveId: entrance.CaveId);
                    }
                    else
                    {
                        builder.AddNamedArrayField(
                            tagTypeAffected.CaveLogPropertyName, [(entity.Id, oldTagName)], [
                                (entity.Id, tag.Name)
                            ], overrideCaveId: entrance.CaveId);
                    }
                }
            }

            var changeLogs = builder.Build();
            if (changeLogs.Any())
            {
                Repository.Add(renameRequest);
                await Repository.SaveChangesAsync(cancellationToken);
                
                foreach (var changeLog in changeLogs)
                {
                    changeLog.Id = IdGenerator.Generate();
                    changeLog.CreatedByUserId = RequestUser.Id;
                }
                await Repository.BulkInsertAsync(changeLogs, cancellationToken: cancellationToken);
            }
            
            await Repository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            
            var result = new TagTypeTableVm
            {
                TagTypeId = entity.Id,
                Name = entity.Name,
                IsUserModifiable = !string.IsNullOrWhiteSpace(entity.AccountId),
                Occurrences = await Repository.GetNumberOfOccurrences(entity.Id)
            };

            return result;
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<int> DeleteTagTypes(IEnumerable<string> tagTypeIds, CancellationToken cancellationToken)
    {

        tagTypeIds = tagTypeIds.ToList();

        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
        {
            throw ApiExceptionDictionary.NoAccount;
        }

        var transaction = await Repository.BeginTransactionAsync(cancellationToken);
        try
        {

            var changeRequest = new CaveChangeRequest
            {
                Id = IdGenerator.Generate(),
                AccountId = RequestUser.AccountId,
                Type = ChangeRequestType.Delete,
                Status = ChangeRequestStatus.Approved,
                ReviewedOn = DateTime.UtcNow,
                CaveId = null,
                ReviewedByUserId = RequestUser.Id,
                Notes = null
            };

            var builder = new ChangeLogBuilder(
                accountId: RequestUser.AccountId,
                caveId: null,
                changedByUserId: RequestUser.Id,
                approvedByUserId: RequestUser.Id,
                changeRequestId: changeRequest.Id
            );

            foreach (var tagTypeId in tagTypeIds)
            {
                var tagType = await _tagRepository.GetTag(tagTypeId);
                if (tagType == null) throw ApiExceptionDictionary.NotFound("Tag Type Id");
                if (tagType.IsDefault) throw ApiExceptionDictionary.Unauthorized("Cannot delete default tag types.");

                #region Cave Tags

                var caveLogPropertyNamesAffected = new List<string>();
                if (TagTypeKeyConstant.Archeology.Equals(tagType.Key))
                {
                    caveLogPropertyNamesAffected.Add(CaveLogPropertyNames.ArcheologyTagName);
                }
                else if (TagTypeKeyConstant.Biology.Equals(tagType.Key))
                {

                }
                else if (TagTypeKeyConstant.People.Equals(tagType.Key))
                {
                    caveLogPropertyNamesAffected.Add(CaveLogPropertyNames.CartographerNameTagName);
                    caveLogPropertyNamesAffected.Add(CaveLogPropertyNames.ReportedByNameTagName);
                    caveLogPropertyNamesAffected.Add(CaveLogPropertyNames.EntranceReportedByNameTagName);
                }
                else if (TagTypeKeyConstant.CaveOther.Equals(tagType.Key))
                {
                    caveLogPropertyNamesAffected.Add(CaveLogPropertyNames.OtherTagName);

                }
                else if (TagTypeKeyConstant.File.Equals(tagType.Key))
                {
                    caveLogPropertyNamesAffected.Add(CaveLogPropertyNames.FileName);

                }
                else if (TagTypeKeyConstant.GeologicAge.Equals(tagType.Key))
                {
                    caveLogPropertyNamesAffected.Add(CaveLogPropertyNames.GeologicAgeTagName);

                }
                else if (TagTypeKeyConstant.Geology.Equals(tagType.Key))
                {
                    caveLogPropertyNamesAffected.Add(CaveLogPropertyNames.GeologyTagName);

                }
                else if (TagTypeKeyConstant.MapStatus.Equals(tagType.Key))
                {
                    caveLogPropertyNamesAffected.Add(CaveLogPropertyNames.MapStatusTagName);

                }
                else if (TagTypeKeyConstant.PhysiographicProvince.Equals(tagType.Key))
                {
                    caveLogPropertyNamesAffected.Add(CaveLogPropertyNames.PhysiographicProvinceTagName);

                }
                else if (TagTypeKeyConstant.EntranceHydrology.Equals(tagType.Key))
                {
                    caveLogPropertyNamesAffected.Add(CaveLogPropertyNames.EntranceHydrologyTagName);

                }
                else if (TagTypeKeyConstant.EntranceStatus.Equals(tagType.Key))
                {
                    caveLogPropertyNamesAffected.Add(CaveLogPropertyNames.EntranceStatusTagName);

                }
                else if (TagTypeKeyConstant.FieldIndication.Equals(tagType.Key))
                {
                    caveLogPropertyNamesAffected.Add(CaveLogPropertyNames.EntranceFieldIndicationTagName);
                }

                #endregion

                foreach (var cageLogPropertyName in caveLogPropertyNamesAffected)
                {
                    var cavesAffected =
                        (await _tagRepository.GetAffectedCaveTagsIEnumerable(tagTypeId, cancellationToken)).ToList();

                    if (cavesAffected.Count != 0)
                    {
                        foreach (var caveId in cavesAffected)
                        {
                            builder.AddNamedArrayField(cageLogPropertyName, [(tagTypeId, tagType.Name)], null,
                                overrideCaveId: caveId);
                        }
                    }

                    var entrancesAffected =
                        (await _tagRepository.GetEntrancesWithTagType(tagTypeId, cancellationToken)).ToList();
                    if (entrancesAffected.Count == 0) continue;

                    foreach (var entrance in entrancesAffected)
                    {
                        builder.AddNamedArrayField(
                            cageLogPropertyName, [(tagTypeId, tagType.Name)], null, overrideCaveId: entrance.CaveId);
                    }
                }

                await Repository.DeleteAffectedTagsFromEntrancesIEnumerable(tagTypeId, cancellationToken);
                await Repository.DeleteAffectedCaveTagsIEnumerable(tagTypeId, cancellationToken);

            }
            
            var changeLogs = builder.Build();

            if (changeLogs.Any())
            {
                Repository.Add(changeRequest);
                await Repository.SaveChangesAsync(cancellationToken);
                
                foreach (var changeLog in changeLogs)
                {
                    changeLog.Id = IdGenerator.Generate();
                    changeLog.CreatedByUserId = RequestUser.Id;
                }
            }
            
          

            await Repository.BulkInsertAsync(changeLogs, cancellationToken: cancellationToken);
            
            var result = await Repository.DeleteTagsAsync(tagTypeIds, cancellationToken);
            
            await Repository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task MergeTagTypes(string[] tagTypeIds, string destinationTagTypeId,
        CancellationToken cancellationToken)
    {
        await Repository.MergeTagTypes(tagTypeIds, destinationTagTypeId, cancellationToken);
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
        var entity = !isNewCounty ? await Repository.GetCounty(countyId, cancellationToken) : new County();

        if (entity == null) throw ApiExceptionDictionary.NotFound("County Id");

        var isDuplicateCountyCode = isNewCounty &&
            await Repository.IsDuplicateCountyCode(county.CountyDisplayId, stateId, cancellationToken);

        if (isDuplicateCountyCode)
            throw ApiExceptionDictionary.BadRequest(
                $"The county code '{county.CountyDisplayId}' is already in use for the selected state.");

        entity.Name = county.Name;
        entity.DisplayId = county.CountyDisplayId;
        entity.StateId = stateId;

        if (isNewCounty)
        {
            entity.AccountId = RequestUser.AccountId ?? throw new InvalidOperationException();
            Repository.Add(entity);
        }

        await Repository.SaveChangesAsync(cancellationToken);
        

        var result = new TagTypeTableCountyVm
        {
            TagTypeId = entity.Id,
            Name = entity.Name,
            IsUserModifiable = true,
            Occurrences = 0,
            CountyDisplayId = entity.DisplayId
        };

        return result;
    }

    public async Task DeleteCounties(IEnumerable<string> countyIds, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
        {
            throw ApiExceptionDictionary.NoAccount;
        }
        countyIds = countyIds.ToList();
        var transaction = await Repository.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var countyId in countyIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var canDelete = await Repository.CanDeleteCounty(countyId);
                if (!canDelete)
                {
                    throw ApiExceptionDictionary.BadRequest("Cannot delete county because it is in use.");
                }

                Repository.Delete(new County { Id = countyId, AccountId = RequestUser.AccountId});
            }

            await Repository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

    }

    public async Task MergeCounties(string[] countyIds, string destinationCountyId, CancellationToken cancellationToken)
    {
        // TODO: Need to update county ids
        throw new NotImplementedException();
        foreach (var id in countyIds)
        {
            await Repository.MergeCounties(countyIds, destinationCountyId);
        }
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
            throw ApiExceptionDictionary.Unauthorized("Cannot modify default feature settings.");

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
    
    #endregion
}