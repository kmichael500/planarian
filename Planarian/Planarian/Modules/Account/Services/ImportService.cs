using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore.Storage;
using Planarian.Library.Constants;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.TemporaryEntities;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Import.Models;
using Planarian.Modules.Import.Repositories;
using Planarian.Modules.Notifications.Services;
using Planarian.Modules.Settings.Repositories;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Base;

namespace Planarian.Modules.Account.Services;

public class ImportService : ServiceBase
{
    private readonly FileService _fileService;
    private readonly TagRepository _tagRepository;
    private readonly SettingsRepository _settingsRepository;
    private readonly TemporaryEntranceRepository _temporaryEntranceRepository;
    private readonly NotificationService _notificationService;
    private readonly CaveRepository _repository;
    private readonly AccountRepository _accountRepository;
    private readonly CaveRepository _caveRepository;

    public ImportService(RequestUser requestUser, FileService fileService,
        TagRepository tagRepository, SettingsRepository settingsRepository,
        TemporaryEntranceRepository temporaryEntranceRepository,
        NotificationService notificationService, CaveRepository repository,
        AccountRepository accountRepository, CaveRepository caveRepository) : base(requestUser)
    {
        _fileService = fileService;
        _tagRepository = tagRepository;
        _settingsRepository = settingsRepository;
        _temporaryEntranceRepository = temporaryEntranceRepository;
        _notificationService = notificationService;
        _repository = repository;
        _accountRepository = accountRepository;
        _caveRepository = caveRepository;
    }

    public async Task<FileVm> AddTemporaryFileForImport(Stream stream, string fileName, string? uuid,
        CancellationToken cancellationToken)
    {
        var result = await _fileService.AddTemporaryAccountFile(stream, fileName,
            FileTypeTagName.Other, cancellationToken, uuid);

        return result;
    }

    private bool TryGetFieldValue<T>(IReaderRow csv, string fieldName, bool isRequired, List<string> errors,
        out T? fieldValue)
    {
        var hasValue = csv.TryGetField(fieldName, out fieldValue);

        if (!hasValue || (typeof(T) == typeof(string) && string.IsNullOrWhiteSpace(fieldValue?.ToString())))
        {
            if (isRequired) errors.Add($"{fieldName} is required.");

            return false;
        }

        // trim value of string
        if (typeof(T) == typeof(string) && fieldValue != null) fieldValue = (T)(object)fieldValue.ToString()?.Trim()!;

        return true;
    }

    #region Cave Import

    public async Task<FileVm> ImportCavesFileProcess(string temporaryFileId, CancellationToken cancellationToken)
    {
        if (RequestUser.AccountId == null) throw ApiExceptionDictionary.NoAccount;

        var signalRGroup = temporaryFileId;

        if (string.IsNullOrWhiteSpace(temporaryFileId)) throw ApiExceptionDictionary.NullValue(nameof(temporaryFileId));

        await using var stream = await _fileService.GetFileStream(temporaryFileId);

        await using var transaction = await _repository.BeginTransactionAsync(cancellationToken);
        try
        {
            var failedRecords = new List<FailedCaveCsvRecord<CaveCsvModel>>();

            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Started parsing CSV");
            var caveRecords = await ParseCaveCsv(stream, failedRecords, cancellationToken);

            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished parsing CSV");

            #region States

            var caves = new List<Cave>();

            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Started processing states");
            var states = caveRecords.Select(e => e.State.Trim()).Distinct().ToList();

            var stateEntities = new List<State>();
            var allAccountStates = (await _accountRepository.GetAllAccountStates()).ToList();
            var newAccountStates = new List<AccountState>();
            foreach (var state in states)
            {
                var stateEntity = await _settingsRepository.GetStateByNameOrAbbreviation(state) ??
                                  throw ApiExceptionDictionary.NotFound("State");
                stateEntities.Add(stateEntity);

                var existingAccountState = allAccountStates.Any(e => e.StateId == stateEntity.Id);
                if (!existingAccountState)
                {
                    var accountState = new AccountState
                    {
                        Id = IdGenerator.Generate(),
                        AccountId = RequestUser.AccountId,
                        StateId = stateEntity.Id,
                        CreatedByUserId = RequestUser.Id,
                        CreatedOn = DateTime.UtcNow
                    };
                    newAccountStates.Add(accountState);
                }
            }

            await _accountRepository.BulkInsertAsync(newAccountStates, cancellationToken: cancellationToken);

            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished processing states");

            #endregion

            #region Tags

            var allGeologyTags = await CreateAndProcessCaveTags(caveRecords, await _tagRepository.GetGeologyTags(),
                TagTypeKeyConstant.Geology, e => e.Geology?.SplitAndTrim(), signalRGroup, cancellationToken);

            var allGeologicAgeTags = await CreateAndProcessCaveTags(caveRecords, await _tagRepository.GetTags (TagTypeKeyConstant.GeologicAge),
                TagTypeKeyConstant.GeologicAge, e => e.GeologicAges?.SplitAndTrim(), signalRGroup, cancellationToken);

            var allMapStatusTags = await CreateAndProcessCaveTags(caveRecords, await _tagRepository.GetTags (TagTypeKeyConstant.MapStatus),
                TagTypeKeyConstant.Geology, e => e.Geology?.SplitAndTrim(), signalRGroup, cancellationToken);
            
            var allPhysiographicProvincesTags = await CreateAndProcessCaveTags(caveRecords,
                await _tagRepository.GetTags(TagTypeKeyConstant.PhysiographicProvince),
                TagTypeKeyConstant.PhysiographicProvince, e => e.PhysiographicProvinces?.SplitAndTrim(), signalRGroup,
                cancellationToken);
            
            var allArcheologyTags = await CreateAndProcessCaveTags(caveRecords, await _tagRepository.GetTags (TagTypeKeyConstant.Archeology),
                TagTypeKeyConstant.Archeology, e => e.Archeology?.SplitAndTrim(), signalRGroup, cancellationToken);

            var allBiologyTags = await CreateAndProcessCaveTags(caveRecords, await _tagRepository.GetTags (TagTypeKeyConstant.Biology),
                TagTypeKeyConstant.Biology, e => e.Biology?.SplitAndTrim(), signalRGroup, cancellationToken);

            var allOtherTags = await CreateAndProcessCaveTags(caveRecords,
                await _tagRepository.GetTags(TagTypeKeyConstant.CaveOther),
                TagTypeKeyConstant.CaveOther, e => e.OtherTags?.SplitAndTrim(), signalRGroup, cancellationToken);

            var allCartographerNames = await CreateAndProcessCaveTags(caveRecords,
                await _tagRepository.GetTags(TagTypeKeyConstant.People),
                TagTypeKeyConstant.People, e => e.CartographerNames?.SplitAndTrim(), signalRGroup,
                cancellationToken);
            
            var allCaveReportedByName = await CreateAndProcessCaveTags(caveRecords,
                await _tagRepository.GetTags(TagTypeKeyConstant.People),
                TagTypeKeyConstant.People, e => e.ReportedByNames?.SplitAndTrim(), signalRGroup,
                cancellationToken);

            var allPeopleTags = allCartographerNames.Concat(allCaveReportedByName).DistinctBy(e => e.Id).ToList();
            #endregion

            #region Counties

            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Started processing counties");
            var allCounties = (await _tagRepository.GetCounties()).ToList();
            var caveRecordsToRemove = new List<CaveCsvModel>();
            var counties = caveRecords.Select(csvRecord =>
                    (new County
                    {
                        Id = IdGenerator.Generate(),
                        Name = csvRecord.CountyName.Trim(),
                        DisplayId = csvRecord.CountyCode.Trim(),
                        AccountId = RequestUser.AccountId,
                        CreatedByUserId = RequestUser.Id,
                        CreatedOn = DateTime.UtcNow,
                        StateId = stateEntities
                            .Where(ee => ee.Name.Contains(csvRecord.State) || ee.Abbreviation.Contains(csvRecord.State))
                            .Select(ee => ee.Id).FirstOrDefault()
                    }, csvRecord))
                .Where(e =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Item1.StateId)) return true;

                    var index = caveRecords.IndexOf(e.csvRecord) + 2;
                    failedRecords.Add(
                        new FailedCaveCsvRecord<CaveCsvModel>(e.csvRecord, index,
                            $"State not found: '{e.csvRecord.State}'"));
                    caveRecordsToRemove.Add(e.csvRecord);
                    return false;
                })
                .Select(e => e.Item1)
                .GroupBy(e => new { e.DisplayId, e.AccountId })
                .Select(e => e.First())
                .ToList();

            foreach (var record in caveRecordsToRemove) caveRecords.Remove(record);
            
            async void OnBatchProcessed(int processed, int total)
            {
                var message = $"Inserted {processed} out of {total} records.";
                await _notificationService.SendNotificationToGroupAsync(signalRGroup, message);
            }
            
            var newCounties = counties.Where(gt => allCounties.All(ag => ag.DisplayId != gt.DisplayId)).ToList();
            await _tagRepository.BulkInsertAsync(newCounties, onBatchProcessed: OnBatchProcessed,
                cancellationToken: cancellationToken);

            allCounties.AddRange(newCounties);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished processing counties");

            #endregion


            var usedCountyNumbers = await _repository.GetUsedCountyNumbers();
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Started processing caves");
            var rowNumber = 1; // start at 1 to account for header row

            #region notifications

            var totalRecords = caveRecords.Count();
            var notifyInterval =
                totalRecords > 0 ? (int)(totalRecords * 0.1) : 0; // every 10% if totalRecords is greater than 0
            notifyInterval = Math.Max(notifyInterval, 1);
            var processedRecords = 0;

            #endregion

            foreach (var caveRecord in caveRecords)
            {
                processedRecords++;
                rowNumber++;
                cancellationToken.ThrowIfCancellationRequested();

                if (processedRecords % notifyInterval == 0 || processedRecords == 1 || processedRecords == totalRecords)
                {
                    var message =
                        $"Processed {processedRecords} out of {totalRecords} records.";
                    await _notificationService.SendNotificationToGroupAsync(signalRGroup, message);
                }

                try
                {
                    var state = stateEntities.FirstOrDefault(e =>
                        e.Name.Contains(caveRecord.State) || e.Abbreviation.Contains(caveRecord.State));
                    if (state == null)
                    {
                        failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(caveRecord, rowNumber,
                            $"State not found: '{caveRecord.State}'"));
                        continue;
                    }

                    var county = allCounties.FirstOrDefault(c =>
                        c.DisplayId.Equals(caveRecord.CountyCode.Trim(), StringComparison.InvariantCultureIgnoreCase) &&
                        c.StateId == state.Id);

                    if (county == null)
                    {
                        var existingCountyRecord =
                            allCounties.FirstOrDefault(e => e.DisplayId == caveRecord.CountyCode.Trim());

                        if (existingCountyRecord == null)
                        {
                            failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(caveRecord, rowNumber,
                                $"{nameof(caveRecord.CountyCode)} not found: '{caveRecord.CountyCode}'"));
                            continue;
                        }

                        failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(caveRecord, rowNumber,
                            $"{nameof(caveRecord.CountyCode)} value '{caveRecord.CountyCode}' is already being used for county '{existingCountyRecord.Name}'."));
                        continue;
                    }

                    if (caveRecord.IsArchived == null)
                    {
                        failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(caveRecord, rowNumber,
                            $"{nameof(caveRecord.IsArchived)} is missing."));
                        continue;
                    }

                    var isValidReportedOn = DateTime.TryParse(caveRecord.ReportedOnDate, out var reportedOnDate);
                    var cave = new Cave
                    {
                        Id = IdGenerator.Generate(),
                        Name = caveRecord.CaveName.Trim(),
                        AccountId = RequestUser.AccountId,
                        LengthFeet = caveRecord.CaveLengthFt,
                        DepthFeet = caveRecord.CaveDepthFt,
                        MaxPitDepthFeet = caveRecord.MaxPitDepthFt,
                        NumberOfPits = caveRecord.NumberOfPits,
                        Narrative = caveRecord.Narrative?.Trim(),
                        CountyId = county.Id,
                        CountyNumber = caveRecord.CountyCaveNumber,
                        StateId = state.Id,
                        ReportedOn = isValidReportedOn ? reportedOnDate : null,
                        IsArchived = (bool)caveRecord.IsArchived,
                        CreatedOn = DateTime.UtcNow,
                        CreatedByUserId = RequestUser.Id
                    };
                    var alternateNames = caveRecord.AlternateNames.SplitAndTrim();
                    cave.SetAlternateNamesList(alternateNames);
                    
                    if (caveRecord.Geology != null)
                    {
                        var geologyNames = caveRecord.Geology.SplitAndTrim()
                            .Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                        foreach (var geologyName in geologyNames)
                        {
                            var tag = allGeologyTags.FirstOrDefault(e =>
                                e.Name.Equals(geologyName, StringComparison.InvariantCultureIgnoreCase));
                            if (tag == null)
                            {
                                failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(caveRecord, rowNumber,
                                    $"{nameof(caveRecord.Geology)} not found: '{geologyName}'"));
                                continue;
                            }

                            var geologyTag = new GeologyTag
                            {
                                Id = IdGenerator.Generate(),
                                CaveId = cave.Id,
                                TagTypeId = tag.Id,
                                CreatedOn = DateTime.UtcNow,
                                CreatedByUserId = RequestUser.Id
                            };
                            cave.GeologyTags.Add(geologyTag);
                        }
                    }

                    if (caveRecord.GeologicAges != null)
                    {
                        var geologicAgesNames = caveRecord.GeologicAges.SplitAndTrim()
                            .Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                        foreach (var geologicAgeName in geologicAgesNames)
                        {
                            var tag = allGeologicAgeTags.FirstOrDefault(e =>
                                e.Name.Equals(geologicAgeName, StringComparison.InvariantCultureIgnoreCase));
                            if (tag == null)
                            {
                                failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(caveRecord, rowNumber,
                                    $"{nameof(caveRecord.GeologicAges)} not found: '{geologicAgeName}'"));
                                continue;
                            }

                            var geologicAgeTag = new GeologicAgeTag
                            {
                                Id = IdGenerator.Generate(),
                                CaveId = cave.Id,
                                TagTypeId = tag.Id,
                                CreatedOn = DateTime.UtcNow,
                                CreatedByUserId = RequestUser.Id
                            };
                            cave.GeologicAgeTags.Add(geologicAgeTag);
                        }
                    }

                    if (caveRecord.MapStatuses != null)
                    {
                        var mapStatusNames = caveRecord.MapStatuses.SplitAndTrim()
                            .Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                        foreach (var mapStatusName in mapStatusNames)
                        {
                            var tag = allMapStatusTags.FirstOrDefault(e =>
                                e.Name.Equals(mapStatusName, StringComparison.InvariantCultureIgnoreCase));
                            if (tag == null)
                            {
                                failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(caveRecord, rowNumber,
                                    $"{nameof(caveRecord.MapStatuses)} not found: '{mapStatusName}'"));
                                continue;
                            }

                            var mapStatusTag = new MapStatusTag
                            {
                                Id = IdGenerator.Generate(),
                                CaveId = cave.Id,
                                TagTypeId = tag.Id,
                                CreatedOn = DateTime.UtcNow,
                                CreatedByUserId = RequestUser.Id
                            };
                            cave.MapStatusTags.Add(mapStatusTag);
                        }
                    }
                    
                    if (caveRecord.PhysiographicProvinces != null)
                    {
                        var physiographicProvinceNames = caveRecord.PhysiographicProvinces.SplitAndTrim()
                            .Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                        foreach (var physiographicProvinceName in physiographicProvinceNames)
                        {
                            var tag = allPhysiographicProvincesTags.FirstOrDefault(e =>
                                e.Name.Equals(physiographicProvinceName, StringComparison.InvariantCultureIgnoreCase));
                            if (tag == null)
                            {
                                failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(caveRecord, rowNumber,
                                    $"{nameof(caveRecord.PhysiographicProvinces)} not found: '{physiographicProvinceName}'"));
                                continue;
                            }

                            var physiographicProvinceTag = new PhysiographicProvinceTag
                            {
                                Id = IdGenerator.Generate(),
                                CaveId = cave.Id,
                                TagTypeId = tag.Id,
                                CreatedOn = DateTime.UtcNow,
                                CreatedByUserId = RequestUser.Id
                            };
                            cave.PhysiographicProvinceTags.Add(physiographicProvinceTag);
                        }
                    }
                    
                    if (caveRecord.Archeology != null)
                    {
                        var archeologyNames = caveRecord.Archeology.SplitAndTrim()
                            .Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                        foreach (var archeologyName in archeologyNames)
                        {
                            var tag = allArcheologyTags.FirstOrDefault(e =>
                                e.Name.Equals(archeologyName, StringComparison.InvariantCultureIgnoreCase));
                            if (tag == null)
                            {
                                failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(caveRecord, rowNumber,
                                    $"{nameof(caveRecord.Archeology)} not found: '{archeologyName}'"));
                                continue;
                            }

                            var archeologyTag = new ArcheologyTag
                            {
                                Id = IdGenerator.Generate(),
                                CaveId = cave.Id,
                                TagTypeId = tag.Id,
                                CreatedOn = DateTime.UtcNow,
                                CreatedByUserId = RequestUser.Id
                            };
                            cave.ArcheologyTags.Add(archeologyTag);
                        }
                    }
                    
                    if (caveRecord.Biology != null)
                    {
                        var biologyNames = caveRecord.Biology.SplitAndTrim()
                            .Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                        foreach (var biologyName in biologyNames)
                        {
                            var tag = allBiologyTags.FirstOrDefault(e =>
                                e.Name.Equals(biologyName, StringComparison.InvariantCultureIgnoreCase));
                            if (tag == null)
                            {
                                failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(caveRecord, rowNumber,
                                    $"{nameof(caveRecord.Biology)} not found: '{biologyName}'"));
                                continue;
                            }

                            var biologyTag = new BiologyTag
                            {
                                Id = IdGenerator.Generate(),
                                CaveId = cave.Id,
                                TagTypeId = tag.Id,
                                CreatedOn = DateTime.UtcNow,
                                CreatedByUserId = RequestUser.Id
                            };
                            cave.BiologyTags.Add(biologyTag);
                        }
                    }
                    
                    if (caveRecord.OtherTags != null)
                    {
                        var otherTagNames = caveRecord.OtherTags.SplitAndTrim()
                            .Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                        foreach (var otherTagName in otherTagNames)
                        {
                            var tag = allOtherTags.FirstOrDefault(e =>
                                e.Name.Equals(otherTagName, StringComparison.InvariantCultureIgnoreCase));
                            if (tag == null)
                            {
                                failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(caveRecord, rowNumber,
                                    $"{nameof(caveRecord.OtherTags)} not found: '{otherTagName}'"));
                                continue;
                            }

                            var otherTag = new CaveOtherTag
                            {
                                Id = IdGenerator.Generate(),
                                CaveId = cave.Id,
                                TagTypeId = tag.Id,
                                CreatedOn = DateTime.UtcNow,
                                CreatedByUserId = RequestUser.Id
                            };
                            cave.CaveOtherTags.Add(otherTag);
                        }
                    }

                    if (caveRecord.CartographerNames != null)
                    {
                        var cartographerNames = caveRecord.CartographerNames.SplitAndTrim()
                            .Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                        foreach (var cartographerName in cartographerNames)
                        {
                            var tag = allPeopleTags.FirstOrDefault(e =>
                                e.Name.Equals(cartographerName, StringComparison.InvariantCultureIgnoreCase));
                            if (tag == null)
                            {
                                failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(caveRecord, rowNumber,
                                    $"{nameof(caveRecord.CartographerNames)} not found: '{cartographerName}'"));
                                continue;
                            }

                            var cartographerTag = new CartographerNameTag()
                            {
                                Id = IdGenerator.Generate(),
                                CaveId = cave.Id,
                                TagTypeId = tag.Id,
                                CreatedOn = DateTime.UtcNow,
                                CreatedByUserId = RequestUser.Id
                            };
                            cave.CartographerNameTags.Add(cartographerTag);
                        }

                        if (caveRecord.ReportedByNames != null)
                        {
                            var reportedByNames = caveRecord.ReportedByNames.SplitAndTrim()
                                .Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                            foreach (var reportedByName in reportedByNames)
                            {
                                var tag = allPeopleTags.FirstOrDefault(e =>
                                    e.Name.Equals(reportedByName, StringComparison.InvariantCultureIgnoreCase));
                                if (tag == null)
                                {
                                    failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(caveRecord, rowNumber,
                                        $"{nameof(caveRecord.ReportedByNames)} not found: '{reportedByName}'"));
                                    continue;
                                }

                                var reportedByTag = new CaveReportedByNameTag()
                                {
                                    Id = IdGenerator.Generate(),
                                    CaveId = cave.Id,
                                    TagTypeId = tag.Id,
                                    CreatedOn = DateTime.UtcNow,
                                    CreatedByUserId = RequestUser.Id
                                };
                                cave.CaveReportedByNameTags.Add(reportedByTag);
                            }
                        }
                    }

                    var isValidCave = IsValidCave(cave, usedCountyNumbers, caveRecord, rowNumber, failedRecords);
                    if (!isValidCave) continue;

                    caves.Add(cave);
                }

                catch (Exception e)
                {
                    failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(caveRecord, rowNumber, e.Message));
                }
            }

            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished processing caves");

            if (failedRecords.Any())
            {
                await transaction.RollbackAsync(cancellationToken);
                failedRecords = failedRecords.OrderBy(e => e.RowNumber).ToList();
                throw ApiExceptionDictionary.InvalidImport(failedRecords, ApiExceptionDictionary.ImportType.Cave);
            }

            await _notificationService.SendNotificationToGroupAsync(signalRGroup,
                "Inserting caves. This may take a while...");
            await _repository.BulkInsertAsync(caves, onBatchProcessed: OnBatchProcessed,
                cancellationToken: cancellationToken);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished inserting caves!");

            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Inserting geology tags.");
            var geologyTagsForInsert = caves.SelectMany(e => e.GeologyTags).ToList();
            await _repository.BulkInsertAsync(geologyTagsForInsert, onBatchProcessed: OnBatchProcessed,
                cancellationToken: cancellationToken);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished inserting geology tags.");
            
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Inserting geologic age tags.");
            var geologicAgeTagsForInsert = caves.SelectMany(e => e.GeologicAgeTags).ToList();
            await _repository.BulkInsertAsync(geologicAgeTagsForInsert, onBatchProcessed: OnBatchProcessed,
                cancellationToken: cancellationToken);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished inserting geologic age tags.");
            
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Inserting map status tags.");
            var mapStatusTagsForInsert = caves.SelectMany(e => e.MapStatusTags).ToList();
            await _repository.BulkInsertAsync(mapStatusTagsForInsert, onBatchProcessed: OnBatchProcessed,
                cancellationToken: cancellationToken);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished inserting map status tags.");
            
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Inserting physiographic province tags.");
            var physiographicProvinceTagsForInsert = caves.SelectMany(e => e.PhysiographicProvinceTags).ToList();
            await _repository.BulkInsertAsync(physiographicProvinceTagsForInsert, onBatchProcessed: OnBatchProcessed,
                cancellationToken: cancellationToken);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished inserting physiographic province tags.");
            
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Inserting archeology tags.");
            var archeologyTagsForInsert = caves.SelectMany(e => e.ArcheologyTags).ToList();
            await _repository.BulkInsertAsync(archeologyTagsForInsert, onBatchProcessed: OnBatchProcessed,
                cancellationToken: cancellationToken);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished inserting archeology tags.");
            
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Inserting biology tags.");
            var biologyTagsForInsert = caves.SelectMany(e => e.BiologyTags).ToList();
            await _repository.BulkInsertAsync(biologyTagsForInsert, onBatchProcessed: OnBatchProcessed,
                cancellationToken: cancellationToken);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished inserting biology tags.");
            
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Inserting other tags.");
            var otherTagsForInsert = caves.SelectMany(e => e.CaveOtherTags).ToList();
            await _repository.BulkInsertAsync(otherTagsForInsert, onBatchProcessed: OnBatchProcessed,
                cancellationToken: cancellationToken);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished inserting other tags.");
            
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Inserting cartographer tags.");
            var cartographerTagsForInsert = caves.SelectMany(e => e.CartographerNameTags).ToList();
            await _repository.BulkInsertAsync(cartographerTagsForInsert, onBatchProcessed: OnBatchProcessed,
                cancellationToken: cancellationToken);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished inserting cartographer tags.");
            
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Inserting reported by tags.");
            var reportedByTagsForInsert = caves.SelectMany(e => e.CaveReportedByNameTags).ToList();
            await _repository.BulkInsertAsync(reportedByTagsForInsert, onBatchProcessed: OnBatchProcessed,
                cancellationToken: cancellationToken);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished inserting reported by tags.");

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception e)
        {
            if (transaction.GetDbTransaction().Connection != null) await transaction.RollbackAsync(cancellationToken);

            throw;
        }

        return new FileVm();
    }

    private async Task<List<CaveCsvModel>> ParseCaveCsv(Stream stream,
        List<FailedCaveCsvRecord<CaveCsvModel>> failedRecords, CancellationToken cancellationToken)
    {
        var caveRecords = new List<CaveCsvModel>();
        using var reader = new StreamReader(stream);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null };
        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<CaveCsvModelMap>();

        if (await csv.ReadAsync())
        {
            csv.ReadHeader();
            var index = 1;

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                index++;

                var record = new CaveCsvModel();
                var errors = new List<string>();

                TryGetFieldValue(csv, nameof(record.CaveName), true, errors, out string? caveName);
                if (!string.IsNullOrWhiteSpace(caveName)) record.CaveName = caveName;
                
                TryGetFieldValue(csv, nameof(record.AlternateNames), false, errors, out string? alternateName);
                record.AlternateNames = alternateName;
                
                TryGetFieldValue(csv, nameof(record.State), true, errors, out string? state);
                if (!string.IsNullOrWhiteSpace(state)) record.State = state;

                TryGetFieldValue(csv, nameof(record.CountyCode), true, errors, out string? countyCode);
                if (!string.IsNullOrWhiteSpace(countyCode)) record.CountyCode = countyCode;

                TryGetFieldValue(csv, nameof(record.CountyName), true, errors, out string? countyName);
                if (!string.IsNullOrWhiteSpace(countyName)) record.CountyName = countyName;

                TryGetFieldValue(csv, nameof(record.CountyCaveNumber), true, errors, out int countyCaveNumber);
                record.CountyCaveNumber = countyCaveNumber;
                
                TryGetFieldValue(csv, nameof(record.MapStatuses), false, errors, out string? mapStatuses);
                record.MapStatuses = mapStatuses;
                
                TryGetFieldValue(csv, nameof(record.CartographerNames), false, errors, out string? cartographerNames);
                record.CartographerNames = cartographerNames;

                TryGetFieldValue(csv, nameof(record.CaveLengthFt), false, errors, out double? caveLengthFt);
                record.CaveLengthFt = caveLengthFt;

                TryGetFieldValue(csv, nameof(record.CaveDepthFt), false, errors, out double? caveDepthFt);
                record.CaveDepthFt = caveDepthFt;

                TryGetFieldValue(csv, nameof(record.MaxPitDepthFt), false, errors, out double? maxPitDepthFt);
                record.MaxPitDepthFt = maxPitDepthFt;

                TryGetFieldValue(csv, nameof(record.NumberOfPits), false, errors, out int? numberOfPits);
                record.NumberOfPits = numberOfPits;                
                
                TryGetFieldValue(csv, nameof(record.Narrative), false, errors, out string? narrative);
                record.Narrative = narrative;
                
                TryGetFieldValue(csv, nameof(record.Geology), false, errors, out string? geology);
                record.Geology = geology;
                
                TryGetFieldValue(csv, nameof(record.GeologicAges), false, errors, out string? geologicAges);
                record.GeologicAges = geologicAges;
                
                TryGetFieldValue(csv, nameof(record.PhysiographicProvinces), false, errors, out string? physiographicProvinces);
                record.PhysiographicProvinces = physiographicProvinces;
                
                TryGetFieldValue(csv, nameof(record.Archeology), false, errors, out string? archeology);
                record.Archeology = archeology;
                
                TryGetFieldValue(csv, nameof(record.Biology), false, errors, out string? biology);
                record.Biology = biology;
                
                TryGetFieldValue(csv, nameof(record.IsArchived), false, errors, out bool isArchived);
                record.IsArchived = isArchived;
                
                TryGetFieldValue(csv, nameof(record.ReportedOnDate), false, errors, out string? reportedOnDate);
                record.ReportedOnDate = reportedOnDate;

                TryGetFieldValue(csv, nameof(record.ReportedByNames), false, errors, out string? reportedByName);
                record.ReportedByNames = reportedByName;

                TryGetFieldValue(csv, nameof(record.OtherTags), false, errors, out string? otherTags);
                record.OtherTags = otherTags;
                
                if (errors.Any())
                    foreach (var error in errors)
                        failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(record, index, error));
                else
                    caveRecords.Add(record);
            }
        }

        if (!failedRecords.Any()) return caveRecords;

        failedRecords = failedRecords.OrderBy(e => e.RowNumber).ToList();
        throw ApiExceptionDictionary.InvalidImport(failedRecords, ApiExceptionDictionary.ImportType.Cave);
    }

    private bool IsValidCave(Cave cave, HashSet<CaveRepository.UsedCountyNumber> usedCountyNumbers,
        CaveCsvModel currentRecord,
        int currentRowNumber,
        List<FailedCaveCsvRecord<CaveCsvModel>> failedRecords)
    {
        var isValid = true;
        if (cave == null) throw ApiExceptionDictionary.BadRequest("Cave is null");

        // check max length for properties
        foreach (var prop in typeof(Cave).GetProperties())
            if (prop.Name != nameof(cave.Id))
            {
                var maxLengthAttribute =
                    prop.GetCustomAttributes(typeof(MaxLengthAttribute), false).FirstOrDefault() as MaxLengthAttribute;
                if (maxLengthAttribute != null)
                {
                    var stringValue = prop.GetValue(cave) as string;
                    if (stringValue != null && stringValue.Length > maxLengthAttribute.Length)
                    {
                        failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(currentRecord, currentRowNumber,
                            $"{prop.Name} exceeds the maximum allowed length of {maxLengthAttribute.Length}"));
                        isValid = false;
                    }
                }
            }

        if (usedCountyNumbers.Any(e => e.CountyId == cave.CountyId && e.CountyNumber == cave.CountyNumber))
        {
            failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(currentRecord, currentRowNumber,
                $"County number is already used"));
            isValid = false;
        }
        else
        {
            usedCountyNumbers.Add(new CaveRepository.UsedCountyNumber(cave.CountyId, cave.CountyNumber));
        }

        if (cave.CountyId == null)
        {
            failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(currentRecord, currentRowNumber,
                $"County is required"));
            isValid = false;
        }

        if (cave.NumberOfPits is < 0)
        {
            failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(currentRecord, currentRowNumber,
                $"Number of pits must be greater than or equal to 1!"));
            isValid = false;
        }
        
        if (cave.LengthFeet is < 0)
        {
            failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(currentRecord, currentRowNumber,
                $"Length must be greater than or equal to 0!"));
            isValid = false;
        }
        
        if (cave.DepthFeet is < 0)
        {
            failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(currentRecord, currentRowNumber,
                $"Depth must be greater than or equal to 0!"));
            isValid = false;
        }
        
        if (cave.MaxPitDepthFeet is < 0)
        {
            failedRecords.Add(new FailedCaveCsvRecord<CaveCsvModel>(currentRecord, currentRowNumber,
                $"Pit depth must be greater than or equal to 0!"));
            isValid = false;
        }

        return isValid;
    }

    private async Task<List<TagType>> CreateAndProcessCaveTags(
        IEnumerable<CaveCsvModel> caveRecords,
        IEnumerable<TagType> existingTags,
        string key,
        Func<CaveCsvModel, IEnumerable<string?>?> selector,
        string signalRGroup,
        CancellationToken cancellationToken)
    {
        await _notificationService.SendNotificationToGroupAsync(signalRGroup, $"Started processing {key} tags");
        var allTags = existingTags.ToList();

        // Extract tags from cave records and create unique tag list
        var tags = caveRecords.SelectMany(e => selector(e)?.Select(s => s?.Trim()) ?? Array.Empty<string>())
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
            })
            .ToList();

        // Determine new tags that don't already exist in the system
        var newTags = tags.Where(gt => allTags.All(ag => ag.Name != gt.Name)).ToList();

        async void OnBatchProcessed(int currentProcessedCount, int total)
        {
            var message = $"Inserted {currentProcessedCount} out of {total} {key} tags.";
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, message);
        }

        // Insert new tags into the repository
        await _tagRepository.BulkInsertAsync(newTags, onBatchProcessed: OnBatchProcessed,
            cancellationToken: cancellationToken);

        allTags.AddRange(newTags); // Combine new tags with existing ones
        await _notificationService.SendNotificationToGroupAsync(signalRGroup, $"Finished processing {key} tags");

        return allTags; // Return the complete list of tags
    }

    #endregion


    #region Entrance Import

    public async Task<FileVm> ImportEntrancesFileProcess(string temporaryFileId, CancellationToken cancellationToken)
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

            var entrances = new List<TemporaryEntrance>();

            #region Tags

            var allLocationQualityTags = await CreateAndProcessEntranceTags(entranceRecords,
                (await _tagRepository.LocationQualityTags()).ToList(),
                TagTypeKeyConstant.LocationQuality,
                e => new List<string?> { e.LocationQuality }, signalRGroup,
                cancellationToken);

            var allEntranceStatusTags = await CreateAndProcessEntranceTags(entranceRecords,
                (await _tagRepository.GetEntranceStatusTags()).ToList(),
                TagTypeKeyConstant.EntranceStatus, e => e.EntranceStatuses?.SplitAndTrim(), signalRGroup, cancellationToken);
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
                    
                    //TODO: How does this work??? Untested
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
                throw ApiExceptionDictionary.InvalidImport(failedRecords, ApiExceptionDictionary.ImportType.Entrance);

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
            var unassociatedEntranceIds = await _temporaryEntranceRepository.UpdateTemporaryEntranceWithCaveId();
            await _notificationService.SendNotificationToGroupAsync(signalRGroup,
                "Finished associating entrances with caves");
            foreach (var unassociatedEntranceId in unassociatedEntranceIds)
            {
                var unassociatedRecord = entranceRecords.FirstOrDefault(e => e.EntranceId == unassociatedEntranceId);
                if (unassociatedRecord == null) continue;

                // calculate row number from the index of the record in the list, +2 because the first row is the header and the index is 0 based
                var calculatedRowNumber = entranceRecords.IndexOf(unassociatedRecord) + 2;
                failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(unassociatedRecord, calculatedRowNumber,
                    $"Entrance could not be associated with the cave {unassociatedRecord.CountyCode}-{unassociatedRecord.CountyCaveNumber}"));
            }

            if (failedRecords.Any())
            {
                failedRecords = failedRecords.OrderBy(e => e.RowNumber).ToList();
                throw ApiExceptionDictionary.InvalidImport(failedRecords, ApiExceptionDictionary.ImportType.Entrance);
            }

            await _notificationService.SendNotificationToGroupAsync(signalRGroup,
                "Validating there is only one primary entrance per cave");
            var invalidPrimaryEntrance = await _temporaryEntranceRepository.GetInvalidIsPrimaryRecords();
            if (invalidPrimaryEntrance.Any())
                foreach (var tempEntranceId in invalidPrimaryEntrance)
                {
                    var tempEntrance = entrances.FirstOrDefault(e => e.Id == tempEntranceId);
                    if (tempEntrance == null)
                        throw ApiExceptionDictionary.InternalServerError(
                            "There was an issue validating primary entrances.");

                    var record = entranceRecords.FirstOrDefault(e => e.EntranceId == tempEntranceId);
                    if (record == null)
                        throw ApiExceptionDictionary.InternalServerError(
                            "There was an issue validating primary entrances.");

                    // calculate row number from the index of the record in the list, +2 because the first row is the header and the index is 0 based
                    var calculatedRowNumber = entranceRecords.IndexOf(record) + 2;
                    failedRecords.Add(new FailedCaveCsvRecord<EntranceCsvModel>(record, calculatedRowNumber,
                        $"Entrance is marked as primary but there is already a primary entrance for the cave {record.CountyCode}-{record.CountyCaveNumber}"));
                }

            if (failedRecords.Any())
            {
                failedRecords = failedRecords.OrderBy(e => e.RowNumber).ToList();
                throw ApiExceptionDictionary.InvalidImport(failedRecords, ApiExceptionDictionary.ImportType.Entrance);
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

            await transaction.CommitAsync(cancellationToken);
            await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished importing entrances!");
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return new FileVm();
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

                TryGetFieldValue(csv, nameof(record.EntrancePitDepth), false, errors, out double entrancePitDepth);
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
        throw ApiExceptionDictionary.InvalidImport(failedRecords, ApiExceptionDictionary.ImportType.Entrance);
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

    #endregion

    public async Task<object?> AddFileForImport(Stream stream, string fileName, string countyCodeRegex,
        string delimiterRegex, string? uuid, CancellationToken cancellationToken)
    {
        // var delimiterRegex = @"";
        // const string countyCodeRegex = @"[A-Z]{2}";
    
        var caveInfos = ExtractCountyInformation(fileName, delimiterRegex, countyCodeRegex, cancellationToken);
        var validCaveIds = new Dictionary<string, string>();

        foreach (var cave in caveInfos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var caveId =
                await _caveRepository.GetCaveIdByCountyCodeNumber(cave.CountyCode, cave.CountyCaveNumber,
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(caveId))
            {
                throw ApiExceptionDictionary.BadRequest(
                    $"Cave with county code {cave.CountyCode} and number {cave.CountyCaveNumber} does not exist for file '{fileName}'.");
            }

            validCaveIds.Add(fileName, caveId);
        }

        foreach (var (key, value) in validCaveIds)
        {
            await _fileService.UploadCaveFile(stream, value, key, cancellationToken, uuid);
        }
    
        return caveInfos;
    }

    // public static List<CountyCaveInfo> ExtractCountyInformation(string fileName,
    //     string delimiterRegex, string countyCodeRegex, CancellationToken cancellationToken)
    // {
    //     // Combining the county code, delimiter, and number patterns.
    //     var combinedPattern = countyCodeRegex + delimiterRegex + "(\\d+)";
    //
    //     var matches = Regex.Matches(fileName, combinedPattern);
    //     var resultList = new List<CountyCaveInfo>();
    //
    //     foreach (Match match in matches)
    //     {
    //         cancellationToken.ThrowIfCancellationRequested();
    //         var countyCode = match.Groups[1].Value;
    //         var countyNumber = int.Parse(match.Groups[2].Value); // Convert string to integer
    //
    //         resultList.Add(new CountyCaveInfo
    //         {
    //             CountyCode = countyCode,
    //             CountyCaveNumber = countyNumber
    //         });
    //     }
    //
    //     return resultList;
    // }

    public static List<CountyCaveInfo> ExtractCountyInformation(string fileName,
        string delimiterRegex, string countyCodeRegex, CancellationToken cancellationToken)
    {
        var countyCodeMatches = Regex.Matches(fileName, countyCodeRegex);
        
        var resultList = new List<CountyCaveInfo>();
        foreach (Match countyCodeMatch in countyCodeMatches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var countyCodeValue = countyCodeMatch.Groups[0].Value;
            var combinedPattern = $"({countyCodeValue})" + delimiterRegex + @"(\d+)";

            var matches = Regex.Matches(fileName, combinedPattern);
            foreach (Match match in matches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var countyCode = match.Groups[1].Value;
                var countyNumber = int.Parse(match.Groups[2].Value);

                resultList.Add(new CountyCaveInfo
                {
                    CountyCode = countyCode,
                    CountyCaveNumber = countyNumber
                });
            }
            
        }
        
        
        return resultList;
    }
    public class CountyCaveInfo
    {
        public string CountyCode { get; set; }
        public int CountyCaveNumber { get; set; }
    }
    

}

