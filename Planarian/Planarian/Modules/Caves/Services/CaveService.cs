using System.ComponentModel.DataAnnotations;
using System.Data.SqlTypes;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.SqlServer.Types;
using NetTopologySuite.Geometries;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.TemporaryEntities;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Modules.Settings.Repositories;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Exceptions;
using Planarian.Shared.Extensions.Type;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Modules.Caves.Services;

public class CaveService : ServiceBase<CaveRepository>
{
    private readonly FileService _fileService;
    private readonly FileRepository _fileRepository;
    private readonly TagRepository _tagRepository;
    private readonly SettingsRepository _settingsRepository;
    private readonly TemporaryEntranceRepository _temporaryEntranceRepository;

    public CaveService(CaveRepository repository, RequestUser requestUser, FileService fileService,
        FileRepository fileRepository, TagRepository tagRepository, SettingsRepository settingsRepository,
        TemporaryEntranceRepository temporaryEntranceRepository) : base(
        repository, requestUser)
    {
        _fileService = fileService;
        _fileRepository = fileRepository;
        _tagRepository = tagRepository;
        _settingsRepository = settingsRepository;
        _temporaryEntranceRepository = temporaryEntranceRepository;
    }

    #region Caves

    public async Task<PagedResult<CaveVm>> GetCaves(FilterQuery query)
    {
        return await Repository.GetCaves(query);
    }

    public async Task<string> AddCave(AddCaveVm values)
    {
        await using var transaction = await Repository.BeginTransactionAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            {
                throw ApiExceptionDictionary.NoAccount;
            }

            #region Data Validation

            // must be at least one entrance
            if (values.Entrances == null || !values.Entrances.Any())
            {
                throw ApiExceptionDictionary.EntranceRequired("At least 1 entrance is required!");
            }

            if (values.NumberOfPits < 0)
            {
                throw ApiExceptionDictionary.BadRequest("Number of pits must be greater than or equal to 1!");
            }

            if (values.LengthFeet < 0)
            {
                throw ApiExceptionDictionary.BadRequest("Length must be greater than or equal to 0!");
            }

            if (values.DepthFeet < 0)
            {
                throw ApiExceptionDictionary.BadRequest("Depth must be greater than or equal to 0!");
            }

            if (values.MaxPitDepthFeet < 0)
            {
                throw ApiExceptionDictionary.BadRequest("Max pit depth must be greater than or equal to 0!");
            }

            if (values.Entrances.Any(e => e.Latitude > 90 || e.Latitude < -90))
            {
                throw ApiExceptionDictionary.BadRequest("Latitude must be between -90 and 90!");
            }

            if (values.Entrances.Any(e => e.Longitude > 180 || e.Longitude < -180))
            {
                throw ApiExceptionDictionary.BadRequest("Longitude must be between -180 and 180!");
            }

            if (values.Entrances.Any(e => e.ElevationFeet < 0))
            {
                throw ApiExceptionDictionary.BadRequest("Elevation must be greater than or equal to 0!");
            }

            if (values.Entrances.Any(e => e.PitFeet < 0))
            {
                throw ApiExceptionDictionary.BadRequest("Pit depth must be greater than or equal to 0!");
            }

            var numberOfPrimaryEntrances = values.Entrances.Count(e => e.IsPrimary);

            if (numberOfPrimaryEntrances == 0)
            {
                throw ApiExceptionDictionary.BadRequest("One entrance must be marked as primary!");
            }

            if (numberOfPrimaryEntrances != 1)
            {
                throw ApiExceptionDictionary.BadRequest("Only one entrance can be marked as primary!");
            }

            #endregion


            var isNew = string.IsNullOrWhiteSpace(values.Id);


            var entity = isNew ? new Cave() : await Repository.GetAsync(values.Id);

            if (entity == null)
            {
                throw ApiExceptionDictionary.NotFound(nameof(entity.Id));
            }

            var isNewCounty = entity.CountyId != values.CountyId;

            entity.Name = values.Name.Trim();
            entity.CountyId = values.CountyId.Trim();
            entity.StateId = values.StateId.Trim();
            entity.LengthFeet = values.LengthFeet;
            entity.DepthFeet = values.DepthFeet;
            entity.MaxPitDepthFeet = values.MaxPitDepthFeet;
            entity.NumberOfPits = values.NumberOfPits;
            entity.Narrative = values.Narrative?.Trim();
            entity.ReportedOn = values.ReportedOn;
            entity.ReportedByName = values.ReportedByName?.Trim();
            entity.AccountId = RequestUser.AccountId;

            if (string.IsNullOrWhiteSpace(entity.ReportedByName)) entity.ReportedByName = RequestUser.FullName;

            entity.GeologyTags.Clear();
            foreach (var tagId in values.GeologyTagIds)
            {
                var tag = new GeologyTag()
                {
                    TagTypeId = tagId
                };
                entity.GeologyTags.Add(tag);
            }

            if (isNewCounty)
            {
                entity.CountyNumber = await Repository.GetNewDisplayId(entity.CountyId);
            }

            if (!isNew)
            {
                // remove entrances
                foreach (var entrance in entity.Entrances.ToList())
                {
                    var entranceValue = values.Entrances.FirstOrDefault(e => e.Id == entrance.Id);

                    if (entranceValue != null) continue;
                    entity.Entrances.Remove(entrance);
                    Repository.Delete(entrance);
                }
            }

            foreach (var entranceValue in values.Entrances)
            {
                var isNewEntrance = string.IsNullOrWhiteSpace(entranceValue.Id);

                var entrance = isNewEntrance ? new Entrance() : await Repository.GetEntrance(entranceValue.Id);

                if (entrance == null)
                {
                    throw ApiExceptionDictionary.NotFound(nameof(entranceValue.Id));
                }

                entrance.Name = entranceValue.Name;
                entrance.LocationQualityTagId = entranceValue.LocationQualityTagId;
                entrance.Description = entranceValue.Description;
                entrance.ReportedOn = entranceValue.ReportedOn ?? DateTime.UtcNow;
                entrance.PitFeet = entranceValue.PitFeet;

                entrance.Location =
                    new Point(entranceValue.Longitude, entranceValue.Latitude, entranceValue.ElevationFeet)
                        { SRID = 4326 };

                if (string.IsNullOrWhiteSpace(entrance.ReportedByName)) entrance.ReportedByName = RequestUser.FullName;

                entrance.EntranceStatusTags.Clear();
                foreach (var tagId in entranceValue.EntranceStatusTagIds)
                {
                    var tag = new EntranceStatusTag()
                    {
                        TagTypeId = tagId,
                    };
                    entrance.EntranceStatusTags.Add(tag);
                }

                entrance.EntranceHydrologyFrequencyTags.Clear();
                foreach (var tagId in entranceValue.EntranceHydrologyFrequencyTagIds)
                {
                    var tag = new EntranceHydrologyFrequencyTag()
                    {
                        TagTypeId = tagId,
                    };
                    entrance.EntranceHydrologyFrequencyTags.Add(tag);
                }

                entrance.FieldIndicationTags.Clear();
                foreach (var tagId in entranceValue.FieldIndicationTagIds)
                {
                    var tag = new FieldIndicationTag()
                    {
                        TagTypeId = tagId,
                    };
                    entrance.FieldIndicationTags.Add(tag);
                }

                entrance.EntranceHydrologyTags.Clear();
                foreach (var tagId in entranceValue.EntranceHydrologyTagIds)
                {
                    var tag = new EntranceHydrologyTag()
                    {
                        TagTypeId = tagId,
                    };
                    entrance.EntranceHydrologyTags.Add(tag);
                }


                entrance.IsPrimary = entranceValue.IsPrimary;

                if (isNewEntrance)
                {
                    entity.Entrances.Add(entrance);
                }
            }

            var blobsToDelete = new List<File>();
            if (values.Files != null)
            {
                foreach (var file in values.Files)
                {
                    var fileEntity = entity.Files.FirstOrDefault(f => f.Id == file.Id);

                    if (fileEntity == null)
                    {
                        throw ApiExceptionDictionary.NotFound("File");
                    }

                    if (!string.IsNullOrWhiteSpace(file.DisplayName) &&
                        !string.Equals(file.DisplayName, fileEntity.DisplayName))
                    {
                        fileEntity.DisplayName = file.DisplayName;
                        fileEntity.FileName = $"{file.DisplayName}{Path.GetExtension(fileEntity.FileName)}";
                    }

                    if (!string.IsNullOrWhiteSpace(file.FileTypeTagId) &&
                        !string.Equals(file.FileTypeTagId, fileEntity.FileTypeTagId))
                    {
                        var tagType = await _tagRepository.GetTag(file.FileTypeTagId);
                        if (tagType == null)
                        {
                            throw ApiExceptionDictionary.NotFound("Tag");
                        }

                        fileEntity.FileTypeTagId = tagType.Id;
                    }

                }

                // check if any ids are missing from the request compared to the db and delete the ones that are missing
                var missingIds = entity.Files.Select(e => e.Id).Except(values.Files.Select(f => f.Id)).ToList();
                foreach (var missingId in missingIds)
                {
                    var fileEntity = entity.Files.FirstOrDefault(f => f.Id == missingId);
                    if (fileEntity != null)
                    {
                        blobsToDelete.Add(fileEntity);
                    }

                    Repository.Delete(fileEntity);
                }
            }

            if (isNew)
            {
                Repository.Add(entity);
            }

            await Repository.SaveChangesAsync();

            await transaction.CommitAsync();

            foreach (var blobProperties in blobsToDelete)
            {
                await _fileService.DeleteFile(blobProperties.BlobKey, blobProperties.BlobContainer);
            }

            return entity.Id;
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<CaveVm?> GetCave(string caveId)
    {
        var cave = await Repository.GetCave(caveId);

        if (cave == null)
        {
            throw ApiExceptionDictionary.NotFound("Cave");
        }

        foreach (var file in cave.Files)
        {
            var fileProperties = await _fileRepository.GetFileBlobProperties(file.Id);
            if (fileProperties == null || string.IsNullOrWhiteSpace((fileProperties.BlobKey)) ||
                string.IsNullOrWhiteSpace(fileProperties.ContainerName)) continue;

            file.EmbedUrl =
                await _fileService.GetLink(fileProperties.BlobKey, fileProperties.ContainerName, file.FileName);
            file.DownloadUrl = await _fileService.GetLink(fileProperties.BlobKey, fileProperties.ContainerName,
                file.FileName, true);
        }

        return cave;
    }

    public async Task DeleteCave(string caveId)
    {
        var entity = await Repository.GetAsync(caveId);

        if (entity == null)
        {
            throw ApiExceptionDictionary.NotFound(nameof(entity.Id));
        }

        var files = entity.Files.ToList();

        Repository.Delete(entity);
        await Repository.SaveChangesAsync();

        foreach (var file in files)
        {
            await _fileService.DeleteFile(file.BlobKey, file.BlobContainer);
        }
    }

    public async Task DeleteAllCaves()
    {
        var blobProperties = await _fileRepository.GetAllCavesBlobProperties();
        await Repository.DeleteAlLCaves();
        foreach (var blobProperty in blobProperties)
        {
            await _fileService.DeleteFile(blobProperty.BlobKey, blobProperty.ContainerName);
        }


    }

    public async Task ArchiveCave(string caveId)
    {
        var entity = await Repository.GetAsync(caveId);

        if (entity == null)
        {
            throw ApiExceptionDictionary.NotFound(nameof(entity.Id));
        }

        entity.IsArchived = true;
        await Repository.SaveChangesAsync();
    }

    public async Task UnarchiveCave(string caveId)
    {
        var entity = await Repository.GetAsync(caveId);

        if (entity == null)
        {
            throw ApiExceptionDictionary.NotFound(nameof(entity.Id));
        }

        entity.IsArchived = false;
        await Repository.SaveChangesAsync();
    }

    #endregion

    #region Import

    public async Task<FileVm> ImportCaves(Stream stream, string fileName, string? uuid,
        CancellationToken cancellationToken)
    {
        if (RequestUser.AccountId == null)
        {
            throw ApiExceptionDictionary.NoAccount;
        }

        await using var transaction = await Repository.BeginTransactionAsync();
        try
        {
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var caveRecords = csv.GetRecords<CaveCsvModel>().ToList();
            var caves = new List<Cave>();

            var states = caveRecords.Select(e => e.State.Trim()).Distinct().ToList();

            var stateEntities = new List<State>();

            foreach (var state in states)
            {
                var stateEntity = await _settingsRepository.GetStateByNameOrAbbreviation(state) ??
                                  throw ApiExceptionDictionary.NotFound("State");
                stateEntities.Add(stateEntity);
            }

            #region Geology

            var allGeologyTags = (await _tagRepository.GetGeologyTags()).ToList();
            var geologyTags = caveRecords.SelectMany(e => e.Geology.Split(',').Select(g => g.Trim())).Distinct()
                .Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => new TagType
                {
                    AccountId = RequestUser.AccountId, CreatedByUserId = RequestUser.Id, CreatedOn = DateTime.UtcNow,
                    Key = TagTypeKeyConstant.Geology, Id = IdGenerator.Generate(), Name = e
                }).ToList();

            var newGeologyTags = geologyTags.Where(gt => allGeologyTags.All(ag => ag.Name != gt.Name)).ToList();

            await _tagRepository.BulkInsertAsync(newGeologyTags, cancellationToken: cancellationToken);

            allGeologyTags.AddRange(newGeologyTags);

            #endregion

            #region Counties

            var allCounties = (await _tagRepository.GetCounties()).ToList();
            var counties = caveRecords.Select(e =>
                    new County
                    {
                        Id = IdGenerator.Generate(),
                        Name = e.CountyName.Trim(),
                        DisplayId = e.CountyCode.Trim(),
                        AccountId = RequestUser.AccountId,
                        CreatedByUserId = RequestUser.Id,
                        CreatedOn = DateTime.UtcNow,
                        StateId = stateEntities
                            .Where(ee => ee.Name.Contains(e.State) || ee.Abbreviation.Contains(e.State))
                            .Select(ee => ee.Id).First()
                    })
                .GroupBy(e => new { e.DisplayId, e.AccountId })
                .Select(e => e.First())
                .ToList();

            var newCounties = counties.Where(gt => allCounties.All(ag => ag.DisplayId != gt.DisplayId)).ToList();
            await _tagRepository.BulkInsertAsync(newCounties, cancellationToken: cancellationToken);

            allCounties.AddRange(newCounties);

            #endregion


            var usedCountyNumbers = await Repository.GetUsedCountyNumbers();
            var failedRecords = new List<FailedCsvRecord<CaveCsvModel>>();

            var rowNumber = 0;
            foreach (var caveRecord in caveRecords)
            {
                rowNumber++;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var state = stateEntities.FirstOrDefault(e =>
                        e.Name.Contains(caveRecord.State) || e.Abbreviation.Contains(caveRecord.State));
                    if (state == null)
                    {
                        throw ApiExceptionDictionary.NotFound("State");
                    }

                    var county = allCounties.FirstOrDefault(c =>
                        c.DisplayId.Equals(caveRecord.CountyCode, StringComparison.InvariantCultureIgnoreCase) &&
                        c.StateId == state.Id);
                    if (county == null)
                    {
                        throw ApiExceptionDictionary.NotFound("County");
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
                        Narrative = caveRecord.Narrative.Trim(),
                        CountyId = county.Id,
                        CountyNumber = caveRecord.CountyCaveNumber,
                        StateId = state.Id,
                        ReportedOn = isValidReportedOn ? reportedOnDate : null,
                        ReportedByName = caveRecord.ReportedByName.Trim(),
                        IsArchived = caveRecord.IsArchived,
                    };
                    var geologyNames = caveRecord.Geology.Split(',').Select(g => g.Trim())
                        .Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                    foreach (var geologyName in geologyNames)
                    {
                        var tag = allGeologyTags.FirstOrDefault(e =>
                            e.Name.Equals(geologyName, StringComparison.InvariantCultureIgnoreCase));
                        if (tag == null)
                        {
                            throw ApiExceptionDictionary.NotFound(nameof(caveRecord.Geology));
                        }

                        var geologyTag = new GeologyTag
                        {
                            Id = IdGenerator.Generate(),
                            CaveId = cave.Id,
                            TagTypeId = tag.Id,
                            CreatedOn = DateTime.UtcNow,
                            CreatedByUserId = RequestUser.Id,
                        };
                        cave.GeologyTags.Add(geologyTag);
                    }

                    await IsValidCave(cave, usedCountyNumbers);
                    caves.Add(cave);
                }
                catch (Exception e)
                {
                    failedRecords.Add(new FailedCsvRecord<CaveCsvModel>(caveRecord, rowNumber, e.Message));
                }
            }
            
            if (failedRecords.Any())
            {
                throw ApiExceptionDictionary.InvalidCaveImport(failedRecords);
            }

            var batchSize = 500;
            var proccessed = 0;
            foreach (var batch in caves.Chunk(batchSize))
            {
                await Repository.BulkInsertAsync(batch, cancellationToken: cancellationToken);
                proccessed += batch.Count;
            }
            
            var geologyTagsForInsert = caves.SelectMany(e => e.GeologyTags).ToList();
            var proccessedGeologyTags = 0;
            foreach (var batch in geologyTagsForInsert.Chunk(batchSize).ToList())
            {
                await Repository.BulkInsertAsync(batch, cancellationToken: cancellationToken);
                proccessedGeologyTags += batch.Count;
            }
            
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return new FileVm();
    }
    
    public async Task<FileVm> ImportCaveEntrances(Stream stream, string fileName, string? uuid,
        CancellationToken cancellationToken)
    {
        if (RequestUser.AccountId == null)
        {
            throw ApiExceptionDictionary.NoAccount;
        }

        await using var transaction = await Repository.BeginTransactionAsync();
        try
        {
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var entranceRecords = csv.GetRecords<EntranceCsvModel>().ToList();
            var entrances = new List<TemporaryEntrance>();

            #region Tags

            var allLocationQualityTags = await ProcessTags(entranceRecords, 
                _tagRepository.LocationQualityTags, 
                TagTypeKeyConstant.LocationQuality, 
                e => new List<string?> { e.LocationQuality }, 
                cancellationToken);
            
            var allEntranceStatusTags = await ProcessTags(entranceRecords, _tagRepository.GetEntranceStatusTags, TagTypeKeyConstant.EntranceStatus, e => e.EntranceStatus?.Split(','), cancellationToken);
            var allEntranceHydrologyTags = await ProcessTags(entranceRecords, _tagRepository.GetEntranceHydrologyTags, TagTypeKeyConstant.EntranceHydrology, e => e.EntranceHydrology?.Split(','), cancellationToken);
            var allEntranceHydrologyFrequencyTags = await ProcessTags(entranceRecords, _tagRepository.GetEntranceHydrologyFrequencyTags, TagTypeKeyConstant.EntranceHydrologyFrequency, e => e.EntranceHydrologyFrequency?.Split(','), cancellationToken);
            var allFieldIndicationTags = await ProcessTags(entranceRecords, _tagRepository.GetFieldIndicationTags, TagTypeKeyConstant.FieldIndication, e => e.FieldIndication?.Split(','), cancellationToken);

            var entranceStatusTags = new List<EntranceStatusTag>();
            var entranceHydrologyTags = new List<EntranceHydrologyTag>();
            var entranceHydrologyFrequencyTags = new List<EntranceHydrologyFrequencyTag>();
            var entranceFieldIndicationTags = new List<FieldIndicationTag>();
            #endregion
            

            var failedRecords = new List<FailedCsvRecord<EntranceCsvModel>>();

            var rowNumber = 0;
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
                        throw ApiExceptionDictionary.NotFound(nameof(entranceRecord.DecimalLatitude));
                    }

                    if (entranceRecord.DecimalLongitude == null)
                    {
                        throw ApiExceptionDictionary.NotFound(nameof(entranceRecord.DecimalLongitude));
                    }

                    if (entranceRecord.EntranceElevationFt == null)
                    {
                        throw ApiExceptionDictionary.NotFound(nameof(entranceRecord.EntranceElevationFt));
                    }

                    var hasCountyCaveNumber = int.TryParse(entranceRecord.CountyCaveNumber, out var countyCaveNumber);

                    if (entranceRecord.DecimalLongitude == null)
                    {
                        throw ApiExceptionDictionary.NotFound(nameof(entranceRecord.DecimalLongitude));
                    }
                    if(entranceRecord.DecimalLatitude == null)
                    {
                        throw ApiExceptionDictionary.NotFound(nameof(entranceRecord.DecimalLatitude));
                    }
                    if(entranceRecord.EntranceElevationFt == null)
                    {
                        throw ApiExceptionDictionary.NotFound(nameof(entranceRecord.DecimalLatitude));
                    }

                    var locationQualityTag = allLocationQualityTags.FirstOrDefault(e =>
                        e.Name.Equals(entranceRecord.LocationQuality, StringComparison.InvariantCultureIgnoreCase));

                    if(locationQualityTag == null)
                    {
                        throw ApiExceptionDictionary.NotFound(nameof(entranceRecord.LocationQuality));
                    }
                    
                    #endregion
                    
                    

                    var entrance = new TemporaryEntrance()
                    {
                        Id = IdGenerator.Generate(),
                        Name = entranceRecord.EntranceName,
                        Description = entranceRecord.EntranceDescription,
                        IsPrimary = entranceRecord.IsPrimaryEntrance ?? false,
                        PitFeet = entranceRecord.EntrancePitDepth,
                        CountyCaveNumber = hasCountyCaveNumber
                            ? countyCaveNumber
                            : throw ApiExceptionDictionary.NotFound(nameof(entranceRecord.CountyCaveNumber)),
                        CountyDisplayId = string.IsNullOrWhiteSpace(entranceRecord.CountyCode) ?
                                          throw ApiExceptionDictionary.NotFound(nameof(entranceRecord.CountyCode)) : entranceRecord.CountyCode,
                        Latitude = (double)entranceRecord.DecimalLatitude,
                        Longitude = (double)entranceRecord.DecimalLongitude,
                        Elevation = (double)entranceRecord.EntranceElevationFt,
                        LocationQualityTagId = locationQualityTag.Id,
                        ReportedOn = isValidReportedOn ? reportedOnDate : null,
                        ReportedByName = entranceRecord.ReportedByName,
                        CaveId = null, // intentionally null
                    };
                    entranceRecord.CaveId = entrance.Id; // used to associate with erroneous records after inserting into the db

                    #region Tags

                    ProcessEntranceTags(
                        entranceRecord, entrance.Id,
                        nameof(entranceRecord.EntranceStatus), 
                        entranceStatusTags, 
                        e => e.EntranceStatus, 
                        allEntranceStatusTags);

                    ProcessEntranceTags(
                        entranceRecord, entrance.Id,
                        nameof(entranceRecord.EntranceHydrology), 
                        entranceHydrologyTags, 
                        e => e.EntranceHydrology, 
                        allEntranceHydrologyTags);

                    ProcessEntranceTags(
                        entranceRecord, entrance.Id,
                        nameof(entranceRecord.EntranceHydrologyFrequency),
                        entranceHydrologyFrequencyTags,
                        e => e.EntranceHydrologyFrequency,
                        allEntranceHydrologyFrequencyTags);
                    
                    ProcessEntranceTags(
                        entranceRecord, entrance.Id,
                        nameof(entranceRecord.FieldIndication), 
                        entranceFieldIndicationTags, 
                        e => e.FieldIndication, 
                        allFieldIndicationTags);
                    
                    #endregion
                    
                    // await IsValidCave(cave, usedCountyNumbers);
                    entrances.Add(entrance);
                }
                catch (Exception e)
                {
                    failedRecords.Add(new FailedCsvRecord<EntranceCsvModel>(entranceRecord, rowNumber, e.Message));
                }
            }

            if (failedRecords.Any())
            {
                throw ApiExceptionDictionary.InvalidCaveImport(failedRecords);
            }
            
            var result = await _temporaryEntranceRepository.CreateTable();
            var result1 = await _temporaryEntranceRepository.TaskInsert(entrances);
            var unassociatedEntranceIds = await _temporaryEntranceRepository.UpdateTemporaryEntranceWithCaveId();

            foreach (var unassociatedEntranceId in unassociatedEntranceIds)
            {
                var unassociatedRecord = entranceRecords.FirstOrDefault(e => e.CaveId == unassociatedEntranceId);
                if (unassociatedRecord == null) continue;

                // calculate row number from the index of the record in the list, +2 because the first row is the header and the index is 0 based
                var calculatedRowNumber = entranceRecords.IndexOf(unassociatedRecord) + 2;
                failedRecords.Add(new FailedCsvRecord<EntranceCsvModel>(unassociatedRecord, calculatedRowNumber,
                    $"Entrance could not be associated with the cave {unassociatedRecord.CountyCode}-{unassociatedRecord.CountyCaveNumber}"));
            }

            if (failedRecords.Any())
            {
                throw ApiExceptionDictionary.InvalidCaveImport(failedRecords);
            }
            
            await _temporaryEntranceRepository.MigrateTemporaryEntrancesAsync();

            #region Tag Insert
            
            const int batchSize = 500;
            
            var proccessedEntranceStatusTags = 0;
            foreach (var batch in entranceStatusTags.Chunk(batchSize).ToList())
            {
                await Repository.BulkInsertAsync(batch, cancellationToken: cancellationToken);
                proccessedEntranceStatusTags += batch.Count;
            }
            
            var proccessedEntranceHydrologyTags = 0;
            foreach (var batch in entranceHydrologyTags.Chunk(batchSize).ToList())
            {
                await Repository.BulkInsertAsync(batch, cancellationToken: cancellationToken);
                proccessedEntranceHydrologyTags += batch.Count;
            }
            
            var proccessedEntranceHydrologyFrequencyTags = 0;
            foreach (var batch in entranceHydrologyFrequencyTags.Chunk(batchSize).ToList())
            {
                await Repository.BulkInsertAsync(batch, cancellationToken: cancellationToken);
                proccessedEntranceHydrologyFrequencyTags += batch.Count;
            }
            
            var proccessedFieldIndicationTags = 0;
            foreach (var batch in entranceFieldIndicationTags.Chunk(batchSize).ToList())
            {
                await Repository.BulkInsertAsync(batch, cancellationToken: cancellationToken);
                proccessedFieldIndicationTags += batch.Count;
            }
            
            

            #endregion
            
            
            await transaction.CommitAsync(cancellationToken);
            
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return new FileVm();
    }

    private async Task<List<TagType>> ProcessTags(IEnumerable<EntranceCsvModel> entranceRecords,
        Func<Task<IEnumerable<TagType>>> getTags, string key, Func<EntranceCsvModel, IEnumerable<string?>?> selector,
        CancellationToken cancellationToken)
    {
        var allTags = (await getTags()).ToList();
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

        await _tagRepository.BulkInsertAsync(newTags, cancellationToken: cancellationToken);

        allTags.AddRange(newTags);

        return allTags;
    }

    private void ProcessEntranceTags<TTag>(EntranceCsvModel entranceRecord,
        string entranceId,
        string entranceTagField,
        List<TTag> tagList,
        Func<EntranceCsvModel, string?> entranceTagSelector,
        List<TagType> allTags) where TTag : EntityBase, IEntranceTag, new()
    {
        var entranceTagNames = entranceTagSelector(entranceRecord)?.Split(',').Select(g => g.Trim())
            .Where(e => !string.IsNullOrWhiteSpace(e)).ToList() ?? new List<string>();

        foreach (var tagName in entranceTagNames)
        {
            var tag = allTags.FirstOrDefault(e =>
                e.Name.Equals(tagName, StringComparison.InvariantCultureIgnoreCase));
            if (tag == null)
            {
                throw ApiExceptionDictionary.NotFound(entranceTagField);
            }

            var entranceTag = new TTag()
            {
                Id = IdGenerator.Generate(),
                EntranceId = entranceId,
                TagTypeId = tag.Id,
                CreatedOn = DateTime.UtcNow,
                CreatedByUserId = RequestUser.Id,
            };
            tagList.Add(entranceTag);
        }
    }




    #endregion

    #region Helper

  
    private async Task IsValidCave(Cave cave, HashSet<CaveRepository.UsedCountyNumber> usedCountyNumbers)
    {
        if (cave == null)
        {
            throw ApiExceptionDictionary.BadRequest("Cave is null");
        }
        
        // check max length for properties
        foreach (var prop in typeof(Cave).GetProperties())
        {
            if (prop.Name != nameof(cave.Id))
            {
                var maxLengthAttribute = prop.GetCustomAttributes(typeof(MaxLengthAttribute), false).FirstOrDefault() as MaxLengthAttribute;
                if (maxLengthAttribute != null)
                {
                    var stringValue = prop.GetValue(cave) as string;
                    if (stringValue != null && stringValue.Length > maxLengthAttribute.Length)
                    {
                        throw ApiExceptionDictionary.BadRequest($"{prop.Name} exceeds the maximum allowed length of {maxLengthAttribute.Length}");
                    }
                }
            }
        }
        
        if(usedCountyNumbers.Any(e => e.CountyId == cave.CountyId && e.CountyNumber == cave.CountyNumber))
        {
            throw ApiExceptionDictionary.BadRequest("County number is already used");
        }
        usedCountyNumbers.Add(new CaveRepository.UsedCountyNumber(cave.CountyId, cave.CountyNumber));
        
        if (cave.CountyId == null)
        {
            throw ApiExceptionDictionary.BadRequest("County is null");
        }

        if (cave.NumberOfPits < 0)
        {
            throw ApiExceptionDictionary.BadRequest("Number of pits must be greater than or equal to 1!");
        }

        if (cave.LengthFeet < 0)
        {
            throw ApiExceptionDictionary.BadRequest("Length must be greater than or equal to 0!");
        }

        if (cave.DepthFeet < 0)
        {
            throw ApiExceptionDictionary.BadRequest("Depth must be greater than or equal to 0!");
        }

        if (cave.MaxPitDepthFeet < 0)
        {
            throw ApiExceptionDictionary.BadRequest("Max pit depth must be greater than or equal to 0!");
        }

        if (cave.Entrances.Any(e => e.PitFeet < 0))
        {
            throw ApiExceptionDictionary.BadRequest("Pit depth must be greater than or equal to 0!");
        }
    }

    #endregion
}

public class FailedCsvRecord<T>
{
    public FailedCsvRecord(T rowData, int rowNumber, string reason)
    {
        CaveCsvModel = rowData;
        RowNumber = rowNumber;
        Reason = reason;
    }

    public T CaveCsvModel { get; set; }
    public int RowNumber { get; set; }
    public string Reason { get; set; }
}



public class CaveCsvModel   
{
    public string CaveName { get; set; }
    public double CaveLengthFt { get; set; }
    public double CaveDepthFt { get; set; }
    public double MaxPitDepthFt { get; set; }
    public int NumberOfPits { get; set; }
    public string Narrative { get; set; }
    public string CountyCode { get; set; }
    public string CountyName { get; set; }
    public int CountyCaveNumber { get; set; }
    public string State { get; set; }
    public string Geology { get; set; }
    public string ReportedOnDate { get; set; }
    public string ReportedByName { get; set; }
    public bool IsArchived { get; set; }
}

public class EntranceCsvModel
{
    public string CountyCaveNumber { get; set; }
    public string? EntranceName { get; set; }
    public string? EntranceDescription { get; set; }
    public bool? IsPrimaryEntrance { get; set; }
    public double? EntrancePitDepth { get; set; }
    public string? EntranceStatus { get; set; }
    public string? EntranceHydrology { get; set; }
    public string? EntranceHydrologyFrequency { get; set; }
    public string? FieldIndication { get; set; }
    public string? CountyCode { get; set; }
    public double? DecimalLatitude { get; set; }
    public double? DecimalLongitude { get; set; }
    public double? EntranceElevationFt { get; set; }
    public string? GeologyFormation { get; set; }
    public string? ReportedOnDate { get; set; }
    public string? ReportedByName { get; set; }
    public string? LocationQuality { get; set; }
    [Ignore]
    public string? CaveId { get; set; }
}