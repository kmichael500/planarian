using Newtonsoft.Json;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Entities.RidgeWalker.ViewModels;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Models;
using JsonConverter = System.Text.Json.Serialization.JsonConverter;

namespace Planarian.Modules.Caves.Services;

public partial class CaveService
{
    public async Task ProposeChange(ProposeChangeRequestVm value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;
        await ValidateCave(value.Cave);

        await using var transaction = await Repository.BeginTransactionAsync(cancellationToken);

        try
        {
            var isNew = string.IsNullOrWhiteSpace(value.Id);

            var entity = isNew
                ? new CaveChangeRequest()
                : await _caveChangeRequestRepository.GetCaveChangeRequest(value.Id!, cancellationToken) ??
                  throw ApiExceptionDictionary.NotFound("Change Request");

            var existingCaveEntity = await Repository.GetCave(value.Cave.Id);
            var isNewCave = existingCaveEntity == null;

            if (isNewCave && value.Cave.Id.IsNullOrWhiteSpace())
            {
                value.Cave.Id = IdGenerator.Generate(PropertyLength.Id);
            }

            foreach (var entrance in value.Cave.Entrances)
            {
                if (string.IsNullOrWhiteSpace(entrance.Id))
                {
                    entrance.Id = IdGenerator.Generate(PropertyLength.Id);
                }
            }

            #region Create People Name Tags

            var tagTypeIds = new List<string>();
            tagTypeIds.AddRange(value.Cave.CartographerNameTagIds);
            tagTypeIds.AddRange(value.Cave.ReportedByNameTagIds);
            tagTypeIds.AddRange(value.Cave.Entrances.SelectMany(e => e.ReportedByNameTagIds));
            tagTypeIds = tagTypeIds.Select(tag => tag.Trim()).Distinct().ToList();
            
            var createdTags = new List<(string Id, string OriginalValue)>();
            
            foreach (var personTagTypeId in tagTypeIds)
            {
                var tagType = await _tagRepository.GetTag(personTagTypeId);
                if (tagType == null)
                {
                    tagType ??= new TagType
                    {
                        Id = IdGenerator.Generate(PropertyLength.Id),
                        Name = personTagTypeId.Trim(),
                        AccountId = RequestUser.AccountId,
                        Key = TagTypeKeyConstant.People,
                    };
                    _tagRepository.Add(tagType);
                    createdTags.Add((tagType.Id, personTagTypeId));
                }
            }

            foreach (var createdTag in createdTags)
            {
                for (var i = 0; i < value.Cave.CartographerNameTagIds.Count; i++)
                {
                    if (value.Cave.CartographerNameTagIds[i].Trim()
                        .Equals(createdTag.OriginalValue, StringComparison.OrdinalIgnoreCase))
                    {
                        value.Cave.CartographerNameTagIds[i] = createdTag.Id;
                    }
                }

                for (var i = 0; i < value.Cave.ReportedByNameTagIds.Count; i++)
                {
                    if (value.Cave.ReportedByNameTagIds[i].Trim()
                        .Equals(createdTag.OriginalValue, StringComparison.OrdinalIgnoreCase))
                    {
                        value.Cave.ReportedByNameTagIds[i] = createdTag.Id;
                    }
                }

                foreach (var entrance in value.Cave.Entrances)
                {
                    for (var i = 0; i < entrance.ReportedByNameTagIds.Count; i++)
                    {
                        if (entrance.ReportedByNameTagIds[i].Trim().Equals(createdTag.OriginalValue,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            entrance.ReportedByNameTagIds[i] = createdTag.Id;
                        }
                    }
                }

            }

            #endregion

            entity.AccountId = RequestUser.AccountId;
            entity.Type = ChangeRequestType.Submission;
            
            if (isNew)
            {
                _caveChangeRequestRepository.Add(entity);
            }

            if (!isNewCave)
            {
                entity.CaveId = value.Cave.Id;
            }
            await _caveChangeLogRepository.SaveChangesAsync(cancellationToken);
            
            var original = isNewCave
                ? null
                : await Repository.GetCave(value.Cave.Id!);
            var changeLogs = await BuildChangeLog(original, value.Cave, RequestUser.Id, RequestUser.Id, entity.Id);
            _caveChangeLogRepository.AddRange(changeLogs);

            await _caveChangeRequestRepository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task ReviewChange(ReviewChangeRequest value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;
        if (string.IsNullOrWhiteSpace(value.Id)) throw ApiExceptionDictionary.BadRequest("Change Request Id");

        var entity = await _caveChangeRequestRepository.GetCaveChangeRequest(value.Id, cancellationToken);
        if (entity == null) throw ApiExceptionDictionary.NotFound("Change Request");
        
        var isNewCave = entity.CaveId.IsNullOrWhiteSpace();

        var countyIdsToCheckPermission = new List<string>();
        if (!isNewCave)
        {
            var currentCountyId = (await Repository.GetCave(entity.CaveId!) ?? throw ApiExceptionDictionary.NotFound("Cave")).CountyId;
            countyIdsToCheckPermission.Add(currentCountyId);
        }

        var changedCountyId = entity.CaveChangeHistory
            .FirstOrDefault(e => e.PropertyName == CaveLogPropertyNames.CountyName)?.PropertyId;
        if (!string.IsNullOrWhiteSpace(changedCountyId))
        {
            countyIdsToCheckPermission.Add(changedCountyId);
        }

        foreach (var countyId in countyIdsToCheckPermission)
        {
            await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, entity.CaveId, countyId);
        }

        var transaction = await _caveChangeRequestRepository.BeginTransactionAsync(cancellationToken);

        try
        {
            entity.Status = value.Approve ? ChangeRequestStatus.Approved : ChangeRequestStatus.Rejected;
            entity.Notes = value.Notes;
            entity.ReviewedByUserId = RequestUser.Id;
            entity.ReviewedOn = DateTime.UtcNow;

            entity.Status = value.Approve switch
            {
                true => ChangeRequestStatus.Approved,
                false => ChangeRequestStatus.Rejected
            };
            
            if (value.Approve)
            {
                var current = await Repository.GetCave(entity.CaveId);
                var updatedCave = await CaveChangeHistoryToAddCave(entity.CaveChangeHistory, current);
                
        
                
                // this is deleting the entranceId from the cahnge log if we removed the entrance...wtf
                
                var caveId = await AddCave(updatedCave, cancellationToken, transaction: transaction);
                entity.CaveId = caveId;
            }
            
            await _caveChangeRequestRepository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await _caveChangeRequestRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<ChangesForReviewVm>> GetChangesForReview()
    {
        var result = await _caveChangeRequestRepository.GetChangesForReview();
        return result;
    }

    public async Task<ProposedChangeRequestVm> GetProposedChange(string id)
    {
        var entity = await _caveChangeRequestRepository.GetCaveChangeRequest(id, CancellationToken.None);
        if (entity == null) throw ApiExceptionDictionary.NotFound("Change Request");

        var changes = await ToChangeLogVm(entity.CaveChangeHistory);
        var original = await Repository.GetCave(entity.CaveId);

        var cave = await CaveChangeHistoryToAddCave(entity.CaveChangeHistory, original);
        
        var result = new ProposedChangeRequestVm
        {
            Id = entity.Id,
            Cave = cave,
            Changes = changes,
            OriginalCave = original,
        };

        return result;
    }

    private async Task<IEnumerable<CaveHistoryRecord>> ToChangeLogVm(IEnumerable<CaveChangeHistory> changes)
    {
        var result = changes.Select(e => new CaveHistoryRecord
        {
            CaveId = e.CaveId,
            EntranceId = e.EntranceId,
            ChangedByUserId = e.ChangedByUserId,
            ApprovedByUserId = e.ApprovedByUserId,
            PropertyName = e.PropertyName,
            ChangeType = e.ChangeType,
            ChangeValueType = e.ChangeValueType,
            ValueString = e.ValueString,
            ValueInt = e.ValueInt,
            ValueDouble = e.ValueDouble,
            ValueBool = e.ValueBool,
            ValueDateTime = e.ValueDateTime
        });

        return result;
    }
    public async Task<List<CaveChangeHistory>> BuildChangeLog(CaveVm? original,
        AddCave modified,
        string approvedByUserId,
        string changedByUserId, string changeRequestId)
    {
        var caveId = modified.Id;

        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw ApiExceptionDictionary.NoAccount;

        if (string.IsNullOrWhiteSpace(caveId))
        {
            throw ApiExceptionDictionary.BadRequest("Cave Id");
        }

        var builder = new ChangeLogBuilder(
            accountId: RequestUser.AccountId,
            caveId: caveId,
            changedByUserId: changedByUserId,
            approvedByUserId: approvedByUserId, changeRequestId: changeRequestId);

        await builder.AddNamedIdFieldAsync(
            CaveLogPropertyNames.CountyName,
            original?.CountyId,
            modified.CountyId,
            _settingsRepository.GetCountyName);

        await builder.AddNamedIdFieldAsync(
            CaveLogPropertyNames.StateName,
            original?.StateId,
            modified.StateId,
            _settingsRepository.GetStateName);

        builder.AddStringFieldAsync(
            CaveLogPropertyNames.Name,
            original?.Name,
            modified.Name);

        builder.AddArrayFieldAsync(
            CaveLogPropertyNames.AlternateNames,
            original?.AlternateNames,
            modified.AlternateNames);

        builder.AddDoubleFieldAsync(
            CaveLogPropertyNames.LengthFeet,
            original?.LengthFeet,
            modified.LengthFeet);

        builder.AddDoubleFieldAsync(
            CaveLogPropertyNames.DepthFeet,
            original?.DepthFeet,
            modified.DepthFeet);

        builder.AddDoubleFieldAsync(
            CaveLogPropertyNames.MaxPitDepthFeet,
            original?.MaxPitDepthFeet,
            modified.MaxPitDepthFeet);

        builder.AddIntFieldAsync(
            CaveLogPropertyNames.NumberOfPits,
            original?.NumberOfPits,
            modified.NumberOfPits);

        builder.AddStringFieldAsync(
            CaveLogPropertyNames.Narrative,
            original?.Narrative,
            modified.Narrative);

        builder.AddDateTimeFieldAsync(
            CaveLogPropertyNames.ReportedOn,
            original?.ReportedOn,
            modified.ReportedOn);

        await builder.AddNamedArrayFieldAsync(
            CaveLogPropertyNames.GeologyTagName,
            original?.GeologyTagIds,
            modified.GeologyTagIds,
            _settingsRepository.GetTagTypeName);

        await builder.AddNamedArrayFieldAsync(
            CaveLogPropertyNames.MapStatusTagName,
            original?.MapStatusTagIds,
            modified.MapStatusTagIds,
            _settingsRepository.GetTagTypeName);

        await builder.AddNamedArrayFieldAsync(
            CaveLogPropertyNames.GeologicAgeTagName,
            original?.GeologicAgeTagIds,
            modified.GeologicAgeTagIds,
            _settingsRepository.GetTagTypeName);

        await builder.AddNamedArrayFieldAsync(
            CaveLogPropertyNames.PhysiographicProvinceTagName,
            original?.PhysiographicProvinceTagIds,
            modified.PhysiographicProvinceTagIds,
            _settingsRepository.GetTagTypeName);

        await builder.AddNamedArrayFieldAsync(
            CaveLogPropertyNames.BiologyTagName,
            original?.BiologyTagIds,
            modified.BiologyTagIds,
            _settingsRepository.GetTagTypeName);

        await builder.AddNamedArrayFieldAsync(
            CaveLogPropertyNames.ArcheologyTagName,
            original?.ArcheologyTagIds,
            modified.ArcheologyTagIds,
            _settingsRepository.GetTagTypeName);

        await builder.AddNamedArrayFieldAsync(
            CaveLogPropertyNames.CartographerNameTagName,
            original?.CartographerNameTagIds,
            modified.CartographerNameTagIds,
            _settingsRepository.GetTagTypeName);

        await builder.AddNamedArrayFieldAsync(
            CaveLogPropertyNames.ReportedByNameTagName,
            original?.ReportedByNameTagIds,
            modified.ReportedByNameTagIds,
            _settingsRepository.GetTagTypeName);

        await builder.AddNamedArrayFieldAsync(
            CaveLogPropertyNames.OtherTagName,
            original?.OtherTagIds,
            modified.OtherTagIds,
            _settingsRepository.GetTagTypeName);

        // TODO: Files, GeoJson

        #region Entrance

        foreach (var modifiedEntrance in modified.Entrances)
        {
            var id = modifiedEntrance.Id ?? IdGenerator.Generate(PropertyLength.Id);
            var originalEntrance = original?.Entrances.FirstOrDefault(e => e.Id == id);

            // name & description
            builder.AddStringFieldAsync(
                CaveLogPropertyNames.EntranceName,
                originalEntrance?.Name,
                modifiedEntrance.Name,
                entranceId: id);
            
            builder.AddDoubleFieldAsync(
                CaveLogPropertyNames.EntranceLatitude,
                originalEntrance?.Latitude,
                modifiedEntrance.Latitude,
                entranceId: id);
            builder.AddDoubleFieldAsync(
                CaveLogPropertyNames.EntranceLongitude,
                originalEntrance?.Longitude,
                modifiedEntrance.Longitude,
                entranceId: id);
            
            builder.AddDoubleFieldAsync(
                CaveLogPropertyNames.EntranceElevationFeet,
                originalEntrance?.ElevationFeet,
                modifiedEntrance.ElevationFeet,
                entranceId: id);

            builder.AddStringFieldAsync(
                CaveLogPropertyNames.EntranceDescription,
                originalEntrance?.Description,
                modifiedEntrance.Description,
                entranceId: id);

            builder.AddBoolFieldAsync(
                CaveLogPropertyNames.EntranceIsPrimary,
                originalEntrance?.IsPrimary,
                modifiedEntrance.IsPrimary,
                entranceId: id);

            // location‐quality tag
            await builder.AddNamedIdFieldAsync(
                CaveLogPropertyNames.EntranceLocationQualityTagName,
                originalEntrance?.LocationQualityTagId,
                modifiedEntrance.LocationQualityTagId,
                _settingsRepository.GetTagTypeName,
                entranceId: id);

            // pit depth
            builder.AddDoubleFieldAsync(
                CaveLogPropertyNames.EntrancePitDepthFeet,
                originalEntrance?.PitFeet,
                modifiedEntrance.PitFeet,
                entranceId: id);

            // reported on
            builder.AddDateTimeFieldAsync(
                CaveLogPropertyNames.EntranceReportedOn,
                originalEntrance?.ReportedOn,
                modifiedEntrance.ReportedOn,
                entranceId: id);

            // status tags
            await builder.AddNamedArrayFieldAsync(
                CaveLogPropertyNames.EntranceStatusTagName,
                originalEntrance?.EntranceStatusTagIds,
                modifiedEntrance.EntranceStatusTagIds,
                _settingsRepository.GetTagTypeName,
                entranceId: id);

            // hydrology tags
            await builder.AddNamedArrayFieldAsync(
                CaveLogPropertyNames.EntranceHydrologyTagName,
                originalEntrance?.EntranceHydrologyTagIds,
                modifiedEntrance.EntranceHydrologyTagIds,
                _settingsRepository.GetTagTypeName,
                entranceId: id);

            // field indications
            await builder.AddNamedArrayFieldAsync(
                CaveLogPropertyNames.EntranceFieldIndicationTagName,
                originalEntrance?.FieldIndicationTagIds,
                modifiedEntrance.FieldIndicationTagIds,
                _settingsRepository.GetTagTypeName,
                entranceId: id);

            // reported-by name tags
            await builder.AddNamedArrayFieldAsync(
                CaveLogPropertyNames.EntranceReportedByNameTagName,
                originalEntrance?.ReportedByNameTagIds,
                modifiedEntrance.ReportedByNameTagIds,
                _settingsRepository.GetTagTypeName,
                entranceId: id);
        }

        var removedEntrances = original?.Entrances
            .Where(e => modified.Entrances.All(m => m.Id != e.Id))
            .Select(e => e.Id)
            .ToList() ?? [];
        
        var addedEntrances = modified.Entrances
            .Where(e => original?.Entrances.All(m => m.Id != e.Id) ?? true)
            .Select(e => e.Id)
            .ToList();
        
        foreach (var removedEntranceId in removedEntrances)
        {
            builder.AddRemoveEntranceLog(removedEntranceId);
        }
        
        foreach (var addedEntranceId in addedEntrances)
        {
            builder.AddAddedEntranceLog(addedEntranceId);
        }

        #endregion

        var changes = builder.Build();
        return changes;
    }

    private async Task<AddCave> CaveChangeHistoryToAddCave(IEnumerable<CaveChangeHistory> changes, CaveVm? current)
    {
        changes = changes.ToList();
        var caveId = changes.First().CaveId;
        
        // copy the current cave if it exists so we don't modify the original
        // cave using system text json
        
        var currentCopy = JsonConvert.DeserializeObject<CaveVm>(JsonConvert.SerializeObject(current));

        currentCopy ??= new CaveVm
        {
            Id = caveId,
            Entrances = []
        };

        foreach (var change in changes)
        {
            EntranceVm? entrance = null;

            var isNewEntrance = false;
            if (!change.EntranceId.IsNullOrWhiteSpace())
            {
                var existingEntrance = currentCopy.Entrances.FirstOrDefault(e => e.Id == change.EntranceId);
                if (existingEntrance != null)
                {
                    entrance = existingEntrance;
                }
                else
                {
                    isNewEntrance = true;
                    entrance = new EntranceVm
                    {
                        Id = change.EntranceId,
                    };
                }

            }
            
            switch (change.PropertyName)
            {
                case CaveLogPropertyNames.Name:
                    currentCopy.Name = change.ValueString!;
                    break;
                case CaveLogPropertyNames.AlternateNames:
                    currentCopy.AlternateNames = MergeList(currentCopy.AlternateNames, change.ValueString!, change.ChangeType)
                        .ToList();
                    break;
                case CaveLogPropertyNames.CountyName:
                    currentCopy.CountyId = change.PropertyId!;
                    break;
                case CaveLogPropertyNames.StateName:
                    currentCopy.StateId = change.PropertyId!;
                    break;
                case CaveLogPropertyNames.LengthFeet:
                    currentCopy.LengthFeet = change.ValueDouble ?? 0;
                    break;
                case CaveLogPropertyNames.DepthFeet:
                    currentCopy.DepthFeet = change.ValueDouble ?? 0;
                    break;
                case CaveLogPropertyNames.MaxPitDepthFeet:
                    currentCopy.MaxPitDepthFeet = change.ValueDouble ?? 0;
                    break;
                case CaveLogPropertyNames.NumberOfPits:
                    currentCopy.NumberOfPits = change.ValueInt ?? 0;
                    break;
                case CaveLogPropertyNames.Narrative:
                    currentCopy.Narrative = change.ValueString;
                    break;
                case CaveLogPropertyNames.ReportedOn:
                    currentCopy.ReportedOn = change.ValueDateTime;
                    break;
                case CaveLogPropertyNames.GeologyTagName:
                    currentCopy.GeologyTagIds =
                        MergeList(currentCopy.GeologyTagIds, change.PropertyId!, change.ChangeType).ToList();
                    break;
                case CaveLogPropertyNames.ReportedByNameTagName:
                    currentCopy.ReportedByNameTagIds =
                        MergeList(currentCopy.ReportedByNameTagIds, change.PropertyId!, change.ChangeType).ToList();
                    break;
                case CaveLogPropertyNames.BiologyTagName:
                    currentCopy.BiologyTagIds =
                        MergeList(currentCopy.BiologyTagIds, change.PropertyId!, change.ChangeType).ToList();
                    break;
                case CaveLogPropertyNames.ArcheologyTagName:
                    currentCopy.ArcheologyTagIds =
                        MergeList(currentCopy.ArcheologyTagIds, change.PropertyId!, change.ChangeType).ToList();
                    break;
                case CaveLogPropertyNames.CartographerNameTagName:
                    currentCopy.CartographerNameTagIds =
                        MergeList(currentCopy.CartographerNameTagIds, change.PropertyId!, change.ChangeType).ToList();
                    break;
                case CaveLogPropertyNames.MapStatusTagName:
                    currentCopy.MapStatusTagIds = MergeList(currentCopy.MapStatusTagIds, change.PropertyId!, change.ChangeType)
                        .ToList();
                    break;
                case CaveLogPropertyNames.GeologicAgeTagName:
                    currentCopy.GeologicAgeTagIds =
                        MergeList(currentCopy.GeologicAgeTagIds, change.PropertyId!, change.ChangeType).ToList();
                    break;
                case CaveLogPropertyNames.PhysiographicProvinceTagName:
                    currentCopy.PhysiographicProvinceTagIds = MergeList(currentCopy.PhysiographicProvinceTagIds,
                        change.PropertyId!, change.ChangeType).ToList();
                    break;
                case CaveLogPropertyNames.OtherTagName:
                    currentCopy.OtherTagIds =
                        MergeList(currentCopy.OtherTagIds, change.PropertyId!, change.ChangeType).ToList();
                    break;
                case CaveLogPropertyNames.EntranceName:
                    entrance!.Name = change.ValueString;
                    break;
                case CaveLogPropertyNames.EntranceLatitude:
                    entrance!.Latitude = change.ValueDouble ?? 0;
                    break;
                case CaveLogPropertyNames.EntranceLongitude:
                    entrance!.Longitude = change.ValueDouble ?? 0;
                    break;
                case CaveLogPropertyNames.EntranceElevationFeet:
                    entrance!.ElevationFeet = change.ValueDouble ?? 0;
                    break;
                case CaveLogPropertyNames.EntranceDescription:
                    entrance!.Description = change.ValueString;
                    break;
                case CaveLogPropertyNames.EntranceIsPrimary:
                    entrance!.IsPrimary = change.ValueBool!.Value;
                    break;
                case CaveLogPropertyNames.EntranceLocationQualityTagName:
                    entrance!.LocationQualityTagId = change.PropertyId;
                    break;
                case CaveLogPropertyNames.EntrancePitDepthFeet:
                    entrance!.PitFeet = change.ValueDouble ?? 0;
                    break;
                case CaveLogPropertyNames.EntranceReportedOn:
                    entrance!.ReportedOn = change.ValueDateTime;
                    break;
                case CaveLogPropertyNames.EntranceStatusTagName:
                    entrance!.EntranceStatusTagIds =
                        MergeList(entrance.EntranceStatusTagIds, change.PropertyId!, change.ChangeType).ToList();
                    break;
                case CaveLogPropertyNames.EntranceHydrologyTagName:
                    entrance!.EntranceHydrologyTagIds = MergeList(entrance.EntranceHydrologyTagIds, change.PropertyId!,
                        change.ChangeType).ToList();
                    break;
                case CaveLogPropertyNames.EntranceFieldIndicationTagName:
                    entrance!.FieldIndicationTagIds =
                        MergeList(entrance.FieldIndicationTagIds, change.PropertyId!, change.ChangeType).ToList();
                    break;
                case CaveLogPropertyNames.EntranceReportedByNameTagName:
                    entrance!.ReportedByNameTagIds =
                        MergeList(entrance.ReportedByNameTagIds, change.PropertyId!, change.ChangeType).ToList();
                    break;
                case CaveLogPropertyNames.Entrance:
                    if (change.ChangeType == ChangeType.Delete)
                    {
                        currentCopy.Entrances.Remove(entrance!);
                    }

                    break;
            }

            if (isNewEntrance)
            {
                currentCopy.Entrances.Add(entrance!);
            }
        }

        return currentCopy.ToAddCave();
    }

    private IEnumerable<T> MergeList<T>(IEnumerable<T> list, T item, string changeType)
    {
        var listCopy = list.ToList();
        switch (changeType)
        {
            case ChangeType.Add:
                listCopy.Add(item);
                break;
            case ChangeType.Delete:
                listCopy.Remove(item);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(changeType), changeType, null);
        }

        return listCopy;
    }
}