using System.Globalization;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
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
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Modules.Caves.Services;

public class CaveService : ServiceBase<CaveRepository>
{
    private readonly FileService _fileService;
    private readonly FileRepository _fileRepository;
    private readonly TagRepository _tagRepository;
    private readonly SettingsRepository _settingsRepository;

    public CaveService(CaveRepository repository, RequestUser requestUser, FileService fileService,
        FileRepository fileRepository, TagRepository tagRepository, SettingsRepository settingsRepository) : base(repository, requestUser)
    {
        _fileService = fileService;
        _fileRepository = fileRepository;
        _tagRepository = tagRepository;
        _settingsRepository = settingsRepository;
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

            entity.Name = values.Name;
            entity.CountyId = values.CountyId;
            entity.StateId = values.StateId;
            entity.LengthFeet = values.LengthFeet;
            entity.DepthFeet = values.DepthFeet;
            entity.MaxPitDepthFeet = values.MaxPitDepthFeet;
            entity.NumberOfPits = values.NumberOfPits;
            entity.Narrative = values.Narrative;
            entity.ReportedOn = values.ReportedOn;
            entity.ReportedByName = values.ReportedByName;
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

    public async Task<FileVm> ImportCaves(Stream stream, string fileName, string? uuid)
    {
        if (RequestUser.AccountId == null)
        {
            throw ApiExceptionDictionary.NoAccount;
        }
        
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var caveRecords = csv.GetRecords<CaveCsvModel>().ToList();

        foreach (var caveRecord in caveRecords)
        {
            await using var transaction = await Repository.BeginTransactionAsync();

            try
            {
                var state = await _settingsRepository.GetStateByNameOrAbbreviation(caveRecord.State);
                if (state == null)
                {
                    throw ApiExceptionDictionary.NotFound("State");
                }

                var county = await _settingsRepository.GetCountyByDisplayId(caveRecord.CountyCode, state.Id);
                if (county == null)
                {
                    county = new County
                    {
                        DisplayId = caveRecord.CountyCode,
                        Name = caveRecord.CountyName,
                        StateId = state.Id,
                        AccountId = RequestUser.AccountId
                    };
                    Repository.Add(county);
                }

                await Repository.SaveChangesAsync();

                var geologyTypes = caveRecord.Geology.Split(',').Select(g => g.Trim()).ToList();
                var geologyTags = new List<TagType>();
                foreach (var geologyType in geologyTypes)
                {
                    if (geologyTags.Select(e => e.Name)
                        .Contains(geologyType, StringComparer.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    var tagType = await _tagRepository.GetGeologyTagByName(geologyType);
                    if (tagType == null)
                    {
                        tagType = new TagType
                        {
                            Key = TagTypeKeyConstant.Geology,
                            Name = geologyType,
                            AccountId = RequestUser.AccountId
                        };
                        _tagRepository.Add(tagType);
                    }

                    geologyTags.Add(tagType);
                }

                await Repository.SaveChangesAsync();

                var cave = new Cave
                {
                    Name = caveRecord.CaveName,
                    AccountId = RequestUser.AccountId,
                    LengthFeet = caveRecord.CaveLengthFt,
                    DepthFeet = caveRecord.CaveDepthFt,
                    MaxPitDepthFeet = caveRecord.MaxPitDepthFt,
                    NumberOfPits = caveRecord.NumberOfPits,
                    Narrative = caveRecord.Narrative,
                    CountyId = county.Id,
                    CountyNumber = caveRecord.CountyCaveNumber,
                    StateId = state.Id,
                    ReportedOn = DateTime.Parse(caveRecord.ReportedOnDate),
                    ReportedByName = caveRecord.ReportedByName,
                    IsArchived = caveRecord.IsArchived
                };

                foreach (var tagType in geologyTags)
                {
                    var geologyTag = new GeologyTag
                    {
                        TagTypeId = tagType.Id,
                    };
                    cave.GeologyTags.Add(geologyTag);
                }

                Repository.Add(cave);

                await Repository.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
            }
        }

        return new FileVm();
    }

    #endregion
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
