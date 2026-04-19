using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.TemporaryEntities;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Account.Import.Models;
using Planarian.Modules.Import.Models;

namespace Planarian.Modules.Account.Import.Services;

public partial class ImportService
{
    public async Task<List<EntranceDryRun>> ImportEntrancesFileProcess(string temporaryFileId, bool isDryRun,
        bool syncExisting,
        CancellationToken cancellationToken)
    {
        if (RequestUser.AccountId == null) throw ApiExceptionDictionary.NoAccount;

        if (string.IsNullOrWhiteSpace(temporaryFileId)) throw ApiExceptionDictionary.NullValue(nameof(temporaryFileId));

        await using var stream = await _fileService.GetFileStream(temporaryFileId);


        await using var transaction = await _repository.BeginTransactionAsync(cancellationToken);
        var signalRGroup = temporaryFileId;
        try
        {
            var failedRecords = new List<FailedCaveCsvRecord<EntranceCsvModel>>();

            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Started parsing CSV");
            var entranceRecords = await ParseEntranceCsv(stream, failedRecords, cancellationToken);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished parsing CSV");

            #region Tags

            var allLocationQualityTags = await CreateAndProcessEntranceTags(entranceRecords,
                (await _tagRepository.LocationQualityTags()).ToList(),
                TagTypeKeyConstant.LocationQuality,
                e => new List<string?> { e.LocationQuality }, signalRGroup,
                cancellationToken);

            var allEntranceStatusTags = await CreateAndProcessEntranceTags(entranceRecords,
                (await _tagRepository.GetEntranceStatusTags()).ToList(),
                TagTypeKeyConstant.EntranceStatus, e => e.EntranceStatuses?.SplitAndTrim(), signalRGroup,
                cancellationToken);
            var allEntranceHydrologyTags = await CreateAndProcessEntranceTags(entranceRecords,
                (await _tagRepository.GetEntranceHydrologyTags()).ToList(),
                TagTypeKeyConstant.EntranceHydrology, e => e.EntranceHydrology?.SplitAndTrim(), signalRGroup,
                cancellationToken);
            var allFieldIndicationTags = await CreateAndProcessEntranceTags(entranceRecords,
                (await _tagRepository.GetFieldIndicationTags()).ToList(),
                TagTypeKeyConstant.FieldIndication, e => e.FieldIndication?.SplitAndTrim(), signalRGroup,
                cancellationToken);

            var allPeopleTags = await CreateAndProcessEntranceTags(entranceRecords,
                (await _tagRepository.GetTags(TagTypeKeyConstant.People)).ToList(),
                TagTypeKeyConstant.People, e => e.ReportedByNames?.SplitAndTrim(), signalRGroup,
                cancellationToken);

            var entranceStatusTags = new List<EntranceStatusTag>();
            var entranceHydrologyTags = new List<EntranceHydrologyTag>();
            var entranceFieldIndicationTags = new List<FieldIndicationTag>();

            var entranceReportedByNameTags = new List<EntranceReportedByNameTag>();

            #endregion

            var entrances = new List<TemporaryEntrance>();
            var rowNumber = 0;
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Started processing entrances");
            foreach (var entranceRecord in entranceRecords)
            {
                rowNumber++;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    #region Validation

                    var isValidReportedOn = DateTime.TryParse(entranceRecord.ReportedOnDate, out var reportedOnDate);

                    if (entranceRecord.DecimalLatitude == null)
                    {
                        failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(entranceRecord, rowNumber,
                            $"Missing value for {nameof(entranceRecord.DecimalLatitude)}"));
                        continue;
                    }

                    if (entranceRecord.DecimalLongitude == null)
                    {
                        failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(entranceRecord, rowNumber,
                            $"Missing value for {nameof(entranceRecord.DecimalLongitude)}"));
                        continue;
                    }

                    if (entranceRecord.EntranceElevationFt == null)
                    {
                        failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(entranceRecord, rowNumber,
                            $"Missing value for {nameof(entranceRecord.EntranceElevationFt)}"));
                        continue;
                    }

                    var hasCountyCaveNumber = int.TryParse(entranceRecord.CountyCaveNumber, out var countyCaveNumber);
                    if (!hasCountyCaveNumber)
                    {
                        failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(entranceRecord, rowNumber,
                            $"Missing value for {nameof(entranceRecord.CountyCaveNumber)}"));
                        continue;
                    }

                    if (entranceRecord.DecimalLongitude == null)
                    {
                        failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(entranceRecord, rowNumber,
                            $"Missing value for {nameof(entranceRecord.DecimalLongitude)}"));
                        continue;
                    }

                    if (entranceRecord.DecimalLatitude == null)
                    {
                        failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(entranceRecord, rowNumber,
                            $"Missing value for {nameof(entranceRecord.DecimalLatitude)}"));
                        continue;
                    }

                    if (entranceRecord.EntranceElevationFt == null)
                    {
                        failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(entranceRecord, rowNumber,
                            $"Missing value for {nameof(entranceRecord.EntranceElevationFt)}"));
                        continue;
                    }

                    var locationQualityTag = allLocationQualityTags.FirstOrDefault(e =>
                        e.Name.Equals(entranceRecord.LocationQuality, StringComparison.InvariantCultureIgnoreCase));

                    if (locationQualityTag == null)
                    {
                        failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(entranceRecord, rowNumber,
                            $"Missing value for {nameof(entranceRecord.LocationQuality)}"));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(entranceRecord.CountyCode))
                    {
                        failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(entranceRecord, rowNumber,
                            $"Missing value for {nameof(entranceRecord.CountyCode)}"));
                        continue;
                    }
                    #endregion
                    var entrance = new TemporaryEntrance()
                    {
                        Id = IdGenerator.Generate(),
                        Name = entranceRecord.EntranceName?.Trim(),
                        Description = entranceRecord.EntranceDescription?.Trim(),
                        IsPrimary = entranceRecord.IsPrimaryEntrance ?? false,
                        PitFeet = entranceRecord.EntrancePitDepth,
                        CountyCaveNumber = countyCaveNumber,
                        CountyDisplayId = entranceRecord.CountyCode,
                        Latitude = (double)entranceRecord.DecimalLatitude,
                        Longitude = (double)entranceRecord.DecimalLongitude,
                        Elevation = (double)entranceRecord.EntranceElevationFt,
                        LocationQualityTagId = locationQualityTag.Id,
                        ReportedOn = isValidReportedOn ? reportedOnDate : null,
                        CaveId = null, // intentionally null
                        CreatedOn = DateTime.UtcNow,
                        CreatedByUserId = RequestUser.Id
                    };

                    entranceRecord.EntranceId =
                        entrance.Id; // used to associate with erroneous records after inserting into the db
                    #region Tags
                    CreateEntranceTags(
                        entranceRecord, entrance.Id,
                        nameof(entranceRecord.EntranceStatuses),
                        entranceStatusTags,
                        e => e.EntranceStatuses,
                        allEntranceStatusTags);

                    CreateEntranceTags(
                        entranceRecord, entrance.Id,
                        nameof(entranceRecord.EntranceHydrology),
                        entranceHydrologyTags,
                        e => e.EntranceHydrology,
                        allEntranceHydrologyTags);

                    CreateEntranceTags(
                        entranceRecord, entrance.Id,
                        nameof(entranceRecord.FieldIndication),
                        entranceFieldIndicationTags,
                        e => e.FieldIndication,
                        allFieldIndicationTags);

                    CreateEntranceTags(
                        entranceRecord, entrance.Id,
                        nameof(entranceRecord.ReportedByNames),
                        entranceReportedByNameTags,
                        e => e.ReportedByNames,
                        allPeopleTags);
                    #endregion

                    var isValid = IsValidEntrance(entrance, entranceRecord, rowNumber, failedRecords);

                    if (isValid) entrances.Add(entrance);
                }
                catch (Exception e)
                {
                    failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(entranceRecord, rowNumber, e.Message));
                }
            }

            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished processing entrances");

            if (failedRecords.Any())
                throw ApiExceptionDictionary.InvalidImport(failedRecords, ImportType.Entrance);

            await _notificationService.SendNotificationToGroupAsync(signalRGroup,
                "Inserting entrances. This may take a while...");
            await _temporaryEntranceRepository.CreateTable();

            async void OnBatchProcessed(int currentProcessedCount, int total)
            {
                var message = $"Inserted {currentProcessedCount} out of {total}.";
                await _notificationService.SendNotificationToGroupAsync(signalRGroup, message);
            }

            await _temporaryEntranceRepository.InsertEntrances(entrances, OnBatchProcessed);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished inserting entrances!");

            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Associating entrances with caves");
            var (unassociatedEntranceIds, associatedEntrances) = await _temporaryEntranceRepository.UpdateTemporaryEntranceWithCaveId();
            await _notificationService.SendNotificationToGroupAsync(signalRGroup,
                "Finished associating entrances with caves");
            foreach (var unassociatedEntranceId in unassociatedEntranceIds)
            {
                var unassociatedRecord = entranceRecords.FirstOrDefault(e => e.EntranceId == unassociatedEntranceId);
                if (unassociatedRecord == null) continue;

                var calculatedRowNumber = entranceRecords.IndexOf(unassociatedRecord) + 2;
                failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(unassociatedRecord, calculatedRowNumber,
                    $"Entrance could not be associated with the cave {unassociatedRecord.CountyCode}-{unassociatedRecord.CountyCaveNumber}"));
            }

            if (failedRecords.Any())
            {
                failedRecords = failedRecords.OrderBy(e => e.RowNumber).ToList();
                throw ApiExceptionDictionary.InvalidImport(failedRecords, ImportType.Entrance);
            }

            var importedCaveIds = associatedEntrances
                .Select(e => e.CaveId)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Cast<string>()
                .Distinct()
                .ToList();
            var existingEntranceCounts = await _temporaryEntranceRepository.GetExistingEntranceCounts(importedCaveIds,
                cancellationToken);
            var existingPrimaryEntranceCounts = await _temporaryEntranceRepository.GetExistingPrimaryEntranceCounts(
                importedCaveIds, cancellationToken);
            var importedEntranceCounts = associatedEntrances
                .Where(e => !string.IsNullOrWhiteSpace(e.CaveId))
                .GroupBy(e => e.CaveId!)
                .ToDictionary(g => g.Key, g => g.Count());

            if (syncExisting)
            {
                await _temporaryEntranceRepository.DeleteExistingEntrancesForImportedCaves(cancellationToken);
            }

            await _notificationService.SendNotificationToGroupAsync(signalRGroup,
                "Validating there is only one primary entrance per cave");
            var invalidPrimaryEntrance = await _temporaryEntranceRepository.GetInvalidIsPrimaryRecords();
            if (invalidPrimaryEntrance.Any())
            {
                var associatedEntrancesById = associatedEntrances.ToDictionary(e => e.Id);
                var invalidCaveIds = new HashSet<string>();
                foreach (var tempEntranceId in invalidPrimaryEntrance)
                {
                    if (!associatedEntrancesById.TryGetValue(tempEntranceId, out var associatedEntrance) ||
                        string.IsNullOrWhiteSpace(associatedEntrance.CaveId))
                        throw ApiExceptionDictionary.InternalServerError(
                            "There was an issue validating primary entrances.");

                    if (!invalidCaveIds.Add(associatedEntrance.CaveId))
                    {
                        continue;
                    }

                    var caveEntranceIds = associatedEntrances
                        .Where(e => e.CaveId == associatedEntrance.CaveId)
                        .Select(e => e.Id)
                        .ToHashSet();
                    var record = entranceRecords.FirstOrDefault(e => caveEntranceIds.Contains(e.EntranceId ?? ""));
                    if (record == null)
                        throw ApiExceptionDictionary.InternalServerError(
                            "There was an issue validating primary entrances.");

                    var existingPrimaryCount = syncExisting
                        ? 0
                        : existingPrimaryEntranceCounts.GetValueOrDefault(associatedEntrance.CaveId);
                    var importedPrimaryCount = associatedEntrances
                        .Count(e => e.CaveId == associatedEntrance.CaveId &&
                                    entrances.Any(entrance => entrance.Id == e.Id && entrance.IsPrimary));
                    var finalPrimaryCount = existingPrimaryCount + importedPrimaryCount;
                    if (finalPrimaryCount == 0)
                    {
                        var calculatedRowNumber = entranceRecords.IndexOf(record) + 2;
                        failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(record, calculatedRowNumber,
                            $"No primary entrance found for cave {record.CountyCode}-{record.CountyCaveNumber}"));
                        continue;
                    }

                    var offendingPrimaryRecords = entranceRecords
                        .Where(e => caveEntranceIds.Contains(e.EntranceId ?? "") && e.IsPrimaryEntrance == true)
                        .ToList();

                    foreach (var offendingRecord in offendingPrimaryRecords)
                    {
                        var calculatedRowNumber = entranceRecords.IndexOf(offendingRecord) + 2;
                        failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(offendingRecord, calculatedRowNumber,
                            $"Entrance is marked as primary but there is already a primary entrance for the cave {offendingRecord.CountyCode}-{offendingRecord.CountyCaveNumber}"));
                    }
                }
            }

            if (failedRecords.Any())
            {
                failedRecords = failedRecords.OrderBy(e => e.RowNumber).ToList();
                throw ApiExceptionDictionary.InvalidImport(failedRecords, ImportType.Entrance);
            }

            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Moving entrances to main table");
            await _temporaryEntranceRepository.MigrateTemporaryEntrancesAsync();

            #region Tag Insert

            const int batchSize = 5000;

            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Inserting entrance status tags");
            await _repository.BulkInsertAsync(entranceStatusTags, onBatchProcessed: OnBatchProcessed,
                batchSize: batchSize,
                cancellationToken: cancellationToken);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Inserting entrance hydrology tags");
            await _repository.BulkInsertAsync(entranceHydrologyTags, onBatchProcessed: OnBatchProcessed,
                batchSize: batchSize,
                cancellationToken: cancellationToken);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup,
                "Inserting entrance hydrology tags");
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Inserting field indication tags");
            await _repository.BulkInsertAsync(entranceFieldIndicationTags, onBatchProcessed: OnBatchProcessed,
                batchSize: batchSize,
                cancellationToken: cancellationToken);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Inserting reported by name tags");
            await _repository.BulkInsertAsync(entranceReportedByNameTags, onBatchProcessed: OnBatchProcessed,
                batchSize: batchSize,
                cancellationToken: cancellationToken);

            #endregion

            await _temporaryEntranceRepository.DropTable();

            var records = new List<EntranceDryRun>();

            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Started processing result!");

            var locationQualityDict = allLocationQualityTags.ToDictionary(e => e.Id, e => e.Name);
            var entranceStatusDict = entranceStatusTags
                .GroupBy(e => e.EntranceId)
                .ToDictionary(g => g.Key, g => g.Select(et => allEntranceStatusTags.FirstOrDefault(tag => tag.Id == et.TagTypeId)?.Name)
                    .Where(name => name != null)
                    .Cast<string>()
                    .ToList());
            var fieldIndicationDict = entranceFieldIndicationTags
                .GroupBy(e => e.EntranceId)
                .ToDictionary(g => g.Key, g => g.Select(et => allFieldIndicationTags.FirstOrDefault(tag => tag.Id == et.TagTypeId)?.Name)
                    .Where(name => name != null)
                    .Cast<string>()
                    .ToList());
            var entranceHydrologyDict = entranceHydrologyTags
                .GroupBy(e => e.EntranceId)
                .ToDictionary(g => g.Key, g => g.Select(et => allEntranceHydrologyTags.FirstOrDefault(tag => tag.Id == et.TagTypeId)?.Name)
                    .Where(name => name != null)
                    .Cast<string>()
                    .ToList());
            var reportedByNameDict = entranceReportedByNameTags
                .GroupBy(e => e.EntranceId)
                .ToDictionary(g => g.Key, g => g.Select(et => allPeopleTags.FirstOrDefault(tag => tag.Id == et.TagTypeId)?.Name)
                    .Where(name => name != null)
                    .Cast<string>()
                    .ToList());
            var associatedEntrancesDict = associatedEntrances.ToDictionary(e => e.Id);

            foreach (var entrance in entrances)
            {
                associatedEntrancesDict.TryGetValue(entrance.Id, out var associatedCave);
                var existingEntranceCount = associatedCave?.CaveId != null
                    ? existingEntranceCounts.GetValueOrDefault(associatedCave.CaveId)
                    : 0;
                var importedEntranceCount = associatedCave?.CaveId != null
                    ? importedEntranceCounts.GetValueOrDefault(associatedCave.CaveId)
                    : 0;
                var finalEntranceCount = syncExisting
                    ? importedEntranceCount
                    : existingEntranceCount + importedEntranceCount;

                var record = new EntranceDryRun
                {
                    AssociatedCave = $"{associatedCave?.DisplayId} {associatedCave?.CaveName}",
                    EntranceCountChange = finalEntranceCount - existingEntranceCount,
                    LocationQuality = locationQualityDict[entrance.LocationQualityTagId],
                    IsPrimaryEntrance = entrance.IsPrimary,
                    EntranceName = entrance.Name,
                    EntranceDescription = entrance.Description,
                    DecimalLatitude = entrance.Latitude,
                    DecimalLongitude = entrance.Longitude,
                    EntranceElevationFt = entrance.Elevation,
                    ReportedOnDate = entrance.ReportedOn,
                    EntrancePitDepth = entrance.PitFeet,
                    EntranceStatuses = entranceStatusDict.TryGetValue(entrance.Id, out var entranceStatuses)
                        ? entranceStatuses
                        : new List<string>(),
                    FieldIndication = fieldIndicationDict.TryGetValue(entrance.Id, value: out var fieldIndication)
                        ? fieldIndication
                        : new List<string>(),
                    EntranceHydrology = entranceHydrologyDict.TryGetValue(entrance.Id, out var entranceHydrology
                    )
                        ? entranceHydrology
                        : new List<string>(),
                    ReportedByNames = reportedByNameDict.TryGetValue(entrance.Id, out var reportedByNames)
                        ? reportedByNames
                        : new List<string>(),
                };

                records.Add(record);

                if (records.Count % 100 == 0)
                {
                    await _notificationService.SendNotificationToGroupAsync(signalRGroup, $"Processing preview of {records.Count} entrances out of {entrances.Count}");
                }
            }

            if (!isDryRun)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished importing entrances!");

            return records;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<List<EntranceCsvModel>> ParseEntranceCsv(Stream stream,
        List<FailedCaveCsvRecord<EntranceCsvModel>> failedRecords, CancellationToken cancellationToken)
    {
        var entranceRecords = new List<EntranceCsvModel>();
        using var reader = new StreamReader(stream);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null };
        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<EntranceCsvModelMap>();

        if (await csv.ReadAsync())
        {
            csv.ReadHeader();
            var index = 1;

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                index++;

                var record = new EntranceCsvModel();
                var errors = new List<string>();

                TryGetFieldValue(csv, nameof(record.CountyCode), true, errors, out string? countyCode);
                if (!string.IsNullOrWhiteSpace(countyCode))
                    record.CountyCode = countyCode;

                TryGetFieldValue(csv, nameof(record.CountyCaveNumber), true, errors, out string? countyCaveNumber);
                if (!string.IsNullOrWhiteSpace(countyCaveNumber))
                    record.CountyCaveNumber = countyCaveNumber;

                TryGetFieldValue(csv, nameof(record.EntranceName), false, errors, out string? entranceName);
                record.EntranceName = entranceName;

                TryGetFieldValue(csv, nameof(record.DecimalLatitude), true, errors, out double decimalLatitude);
                record.DecimalLatitude = decimalLatitude;

                TryGetFieldValue(csv, nameof(record.DecimalLongitude), true, errors, out double decimalLongitude);
                record.DecimalLongitude = decimalLongitude;

                TryGetFieldValue(csv, nameof(record.EntranceElevationFt), true, errors,
                    out double entranceElevationFt);
                record.EntranceElevationFt = entranceElevationFt;

                TryGetFieldValue(csv, nameof(record.LocationQuality), true, errors, out string? locationQuality);
                record.LocationQuality = locationQuality ?? string.Empty;

                TryGetFieldValue(csv, nameof(record.EntranceDescription), false, errors,
                    out string? entranceDescription);
                record.EntranceDescription = entranceDescription;

                TryGetFieldValue(csv, nameof(record.EntrancePitDepth), false, errors, out double? entrancePitDepth);
                record.EntrancePitDepth = entrancePitDepth;

                TryGetFieldValue(csv, nameof(record.EntranceStatuses), false, errors, out string? entranceStatus);
                record.EntranceStatuses = entranceStatus;

                TryGetFieldValue(csv, nameof(record.EntranceHydrology), false, errors, out string? entranceHydrology);
                record.EntranceHydrology = entranceHydrology;

                TryGetFieldValue(csv, nameof(record.FieldIndication), false, errors, out string? fieldIndication);
                record.FieldIndication = fieldIndication;

                TryGetFieldValue(csv, nameof(record.ReportedOnDate), false, errors, out string? reportedOnDate);
                record.ReportedOnDate = reportedOnDate;

                TryGetFieldValue(csv, nameof(record.ReportedByNames), false, errors, out string? reportedByName);
                record.ReportedByNames = reportedByName;

                TryGetFieldValue(csv, nameof(record.IsPrimaryEntrance), false, errors, out bool isPrimaryEntrance);
                record.IsPrimaryEntrance = isPrimaryEntrance;

                // Add record to the list if there are no errors, else add errors to the failedRecords list
                if (errors.Any())
                    foreach (var error in errors)
                        failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(record, index, error));
                else
                    entranceRecords.Add(record);
            }
        }

        if (!failedRecords.Any()) return entranceRecords;

        failedRecords = failedRecords.OrderBy(e => e.RowNumber).ToList();
        throw ApiExceptionDictionary.InvalidImport(failedRecords, ImportType.Entrance);
    }

    private async Task<List<TagType>> CreateAndProcessEntranceTags(IEnumerable<EntranceCsvModel> entranceRecords,
        List<TagType> allTags, string key, Func<EntranceCsvModel, IEnumerable<string?>?> selector,
        string signalRGroup,
        CancellationToken cancellationToken)
    {
        await _notificationService.SendNotificationToGroupAsync(signalRGroup, $"Started processing {key} tags");
        allTags = (allTags).ToList();
        var tags = entranceRecords.SelectMany(e => selector(e)?.Select(s => s?.Trim()) ?? Array.Empty<string>())
            .Distinct()
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => new TagType
            {
                AccountId = RequestUser.AccountId,
                CreatedByUserId = RequestUser.Id,
                CreatedOn = DateTime.UtcNow,
                Key = key,
                Id = IdGenerator.Generate(),
                Name = e ?? throw ApiExceptionDictionary.NullValue(nameof(TagType.Name))
            }).ToList();

        var newTags = tags.Where(gt => allTags.All(ag => ag.Name != gt.Name)).ToList();

        await _tagRepository.BulkInsertAsync(newTags, onBatchProcessed: OnBatchProcessed,
            cancellationToken: cancellationToken);

        allTags.AddRange(newTags);
        await _notificationService.SendNotificationToGroupAsync(signalRGroup, $"Finished processing {key} tags");

        return allTags;

        async void OnBatchProcessed(int currentProcessedCount, int total)
        {
            var message = $"Inserted {currentProcessedCount} out of {total} {key} tags.";
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, message);
        }
    }




    private void CreateEntranceTags<TTag>(EntranceCsvModel entranceRecord,
        string entranceId,
        string entranceTagField,
        List<TTag> tagList,
        Func<EntranceCsvModel, string?> entranceTagSelector,
        List<TagType> allTags) where TTag : EntityBase, IEntranceTag, new()
    {
        var entranceTagNames = entranceTagSelector(entranceRecord)?.SplitAndTrim()
            .Where(e => !string.IsNullOrWhiteSpace(e)).ToList() ?? new List<string>();

        foreach (var tagName in entranceTagNames)
        {
            var tag = allTags.FirstOrDefault(e =>
                e.Name.Equals(tagName, StringComparison.InvariantCultureIgnoreCase));
            if (tag == null) throw ApiExceptionDictionary.NotFound(entranceTagField);

            var entranceTag = new TTag()
            {
                Id = IdGenerator.Generate(),
                EntranceId = entranceId,
                TagTypeId = tag.Id,
                CreatedOn = DateTime.UtcNow,
                CreatedByUserId = RequestUser.Id
            };
            tagList.Add(entranceTag);
        }
    }

    private bool IsValidEntrance(TemporaryEntrance entrance,
        EntranceCsvModel currentRecord,
        int currentRowNumber,
        List<FailedCaveCsvRecord<EntranceCsvModel>> failedRecords)
    {
        var isValid = true;
        if (entrance == null) throw ApiExceptionDictionary.NotFound("Entrance");

        // check max length for properties
        foreach (var prop in typeof(TemporaryEntrance).GetProperties())
            if (prop.Name != nameof(entrance.Id))
                if (prop.GetCustomAttributes(typeof(MaxLengthAttribute), false).FirstOrDefault() is MaxLengthAttribute
                    maxLengthAttribute)
                    if (prop.GetValue(entrance) is string stringValue && stringValue.Length > maxLengthAttribute.Length)
                    {
                        failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(currentRecord, currentRowNumber,
                            $"{prop.Name} exceeds the maximum allowed length of {maxLengthAttribute.Length}"));
                        isValid = false;
                    }

        if (entrance.Latitude is > 90 or < -90)
        {
            failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(currentRecord, currentRowNumber,
                $"Latitude must be between -90 and 90!"));
            isValid = false;
        }

        if (entrance.Longitude is > 180 or < -180)
        {
            failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(currentRecord, currentRowNumber,
                $"Longitude must be between -180 and 180!"));
            isValid = false;
        }

        if (entrance.Elevation < 0)
        {
            failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(currentRecord, currentRowNumber,
                $"Elevation must be greater than or equal to 0!"));
            isValid = false;
        }

        if (entrance.PitFeet < 0)
        {
            failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(currentRecord, currentRowNumber,
                $"Pit depth must be greater than or equal to 0!"));
            isValid = false;
        }


        return isValid;
    }
}