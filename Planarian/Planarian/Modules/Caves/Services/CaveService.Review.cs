using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Entities.RidgeWalker.ViewModels;
using Planarian.Modules.Caves.Models;

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

            var isNewCave = string.IsNullOrWhiteSpace(value.Cave.Id);

            entity.CaveId = value.Cave.Id;
            entity.AccountId = RequestUser.AccountId;
            entity.Json = value.Cave;

            if (isNew)
            {
                _caveChangeRequestRepository.Add(entity);
            }

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

        await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, entity.CaveId, entity.Json.CountyId);

        var transaction = await _caveChangeRequestRepository.BeginTransactionAsync(cancellationToken);

        try
        {
            entity.Status = value.Approve ? ChangeRequestStatus.Approved : ChangeRequestStatus.Rejected;
            entity.Notes = value.Notes;
            entity.ReviewedByUserId = RequestUser.Id;
            entity.ReviewedOn = DateTime.UtcNow;

            if (value.Approve)
            {
                var isNewCave = string.IsNullOrWhiteSpace(value.Cave.Id);
                var originalValues = isNewCave ? null : await Repository.GetCave(value.Cave.Id!);
                var caveId = await AddCave(value.Cave, cancellationToken, transaction: transaction);
                value.Cave.Id = caveId;

                var changes = await BuildChangeLog(originalValues, value.Cave, RequestUser.Id, entity.CreatedByUserId!);
                Repository.AddRange(changes);
                await Repository.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            await _caveChangeRequestRepository.SaveChangesAsync(cancellationToken);
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

        var original = !entity.CaveId.IsNullOrWhiteSpace() ? await GetCave(entity.CaveId) : null;
        var changes = await BuildChangeLogVm(original, entity.Json, entity.ReviewedByUserId!, entity.CreatedByUserId!);
        var result = new ProposedChangeRequestVm
        {
            Id = entity.Id,
            Cave = entity.Json,
            Changes = changes,
            OriginalCave = original,
        };

        return result;
    }

    private async Task<IEnumerable<CaveChangeLogVm>> BuildChangeLogVm(
        CaveVm? original,
        AddCave modified,
        string approvedByUserId,
        string changedByUserId)
    {
        var changes = await BuildChangeLog(original, modified, approvedByUserId, changedByUserId);
        var result = changes.Select(e => new CaveChangeLogVm
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
    public async Task<List<CaveChangeLog>> BuildChangeLog(
        CaveVm? original,
        AddCave modified,
        string approvedByUserId,
        string changedByUserId)
    {
        var caveId = modified.Id;
        if (string.IsNullOrWhiteSpace(caveId))
            throw ApiExceptionDictionary.BadRequest("Cave Id is required");

        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw ApiExceptionDictionary.NoAccount;

        var builder = new ChangeLogBuilder(
            accountId: RequestUser.AccountId,
            caveId: caveId,
            changedByUserId: changedByUserId,
            approvedByUserId: approvedByUserId);

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

        // TODO: Files, Entrances, GeoJson

        #region Entrance

        foreach (var modifiedEntrance in modified.Entrances)
        {
            var id = modifiedEntrance.Id;
            var originalEntrance = original?.Entrances.FirstOrDefault(e => e.Id == id);

            // name & description
            builder.AddStringFieldAsync(
                CaveLogPropertyNames.EntranceName,
                originalEntrance?.Name,
                modifiedEntrance.Name,
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
        
        foreach (var removedEntranceId in removedEntrances)
        {
            builder.AddRemoveEntranceLog(removedEntranceId);
        }

        #endregion

        var changes = builder.Build();
        return changes;
    }
}