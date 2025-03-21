using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.DateTime;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Base;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Modules.Caves.Services;

public class CaveService : ServiceBase<CaveRepository>
{
    private readonly FileService _fileService;
    private readonly FileRepository _fileRepository;
    private readonly TagRepository _tagRepository;

    public CaveService(CaveRepository repository, RequestUser requestUser, FileService fileService,
        FileRepository fileRepository, TagRepository tagRepository) : base(
        repository, requestUser)
    {
        _fileService = fileService;
        _fileRepository = fileRepository;
        _tagRepository = tagRepository;
    }

    #region Caves

    public async Task<PagedResult<CaveSearchVm>> GetCaves(FilterQuery query)
    {
        return await Repository.GetCaves(query);
    }

    public async Task<PagedResult<CaveSearchVm>> GetCavesSearch(FilterQuery query, string? permissionKey = null)
    {
        return await Repository.GetCavesSearch(query, permissionKey);
    }


    public async Task<string> AddCave(AddCaveVm values, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;
        
        await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, values.Id, values.CountyId);
        var isNew = string.IsNullOrWhiteSpace(values.Id);

        await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, values.Id, values.CountyId);

        
        #region Data Validation

        // must be at least one entrance
        if (values.Entrances == null || !values.Entrances.Any())
            throw ApiExceptionDictionary.EntranceRequired("At least 1 entrance is required!");

        if (values.NumberOfPits < 0)
            throw ApiExceptionDictionary.BadRequest("Number of pits must be greater than or equal to 1!");

        if (values.LengthFeet < 0)
            throw ApiExceptionDictionary.BadRequest("Length must be greater than or equal to 0!");

        if (values.DepthFeet < 0)
            throw ApiExceptionDictionary.BadRequest("Depth must be greater than or equal to 0!");

        if (values.MaxPitDepthFeet < 0)
            throw ApiExceptionDictionary.BadRequest("Max pit depth must be greater than or equal to 0!");

        if (values.Entrances.Any(e => e.Latitude > 90 || e.Latitude < -90))
            throw ApiExceptionDictionary.BadRequest("Latitude must be between -90 and 90!");

        if (values.Entrances.Any(e => e.Longitude > 180 || e.Longitude < -180))
            throw ApiExceptionDictionary.BadRequest("Longitude must be between -180 and 180!");

        if (values.Entrances.Any(e => e.ElevationFeet < 0))
            throw ApiExceptionDictionary.BadRequest("Elevation must be greater than or equal to 0!");

        if (values.Entrances.Any(e => e.PitFeet < 0))
            throw ApiExceptionDictionary.BadRequest("Pit depth must be greater than or equal to 0!");

        var numberOfPrimaryEntrances = values.Entrances.Count(e => e.IsPrimary);

        if (numberOfPrimaryEntrances == 0)
            throw ApiExceptionDictionary.BadRequest("One entrance must be marked as primary!");

        if (numberOfPrimaryEntrances != 1)
            throw ApiExceptionDictionary.BadRequest("Only one entrance can be marked as primary!");

        #endregion


        await using var transaction = await Repository.BeginTransactionAsync(cancellationToken);
        try
        {
            var entity = isNew ? new Cave() : await Repository.GetAsync(values.Id);

            if (entity == null) throw ApiExceptionDictionary.NotFound(nameof(entity.Id));

            var isNewCounty = entity.CountyId != values.CountyId;

            entity.Name = values.Name.Trim();
            entity.SetAlternateNamesList(values.AlternateNames.Select(e => e.Trim()));

            entity.CountyId = values.CountyId.Trim();
            entity.StateId = values.StateId.Trim();
            entity.LengthFeet = values.LengthFeet;
            entity.DepthFeet = values.DepthFeet;
            entity.MaxPitDepthFeet = values.MaxPitDepthFeet;
            entity.NumberOfPits = values.NumberOfPits;
            entity.Narrative = values.Narrative?.Trim();
            entity.ReportedOn = values.ReportedOn?.ToUtcKind();
            entity.AccountId = RequestUser.AccountId;

            entity.GeologyTags.Clear();
            foreach (var tagId in values.GeologyTagIds)
            {
                var tag = new GeologyTag()
                {
                    TagTypeId = tagId
                };
                entity.GeologyTags.Add(tag);
            }

            entity.ArcheologyTags.Clear();
            foreach (var tagId in values.ArcheologyTagIds)
            {
                var tag = new ArcheologyTag()
                {
                    TagTypeId = tagId
                };
                entity.ArcheologyTags.Add(tag);
            }

            entity.BiologyTags.Clear();
            foreach (var tagId in values.BiologyTagIds)
            {
                var tag = new BiologyTag()
                {
                    TagTypeId = tagId
                };
                entity.BiologyTags.Add(tag);
            }

            entity.CartographerNameTags.Clear();
            foreach (var personTagTypeId in values.CartographerNameTagIds)
            {

                var tagType = await _tagRepository.GetTag(personTagTypeId);
                tagType ??= new TagType
                {
                    Name = personTagTypeId.Trim(),
                    AccountId = RequestUser.AccountId,
                    Key = TagTypeKeyConstant.People,
                };
                var tag = new CartographerNameTag
                {
                    TagType = tagType
                };

                entity.CartographerNameTags.Add(tag);
            }

            entity.MapStatusTags.Clear();
            foreach (var tagId in values.MapStatusTagIds)
            {
                var tag = new MapStatusTag()
                {
                    TagTypeId = tagId
                };
                entity.MapStatusTags.Add(tag);
            }

            entity.GeologicAgeTags.Clear();
            foreach (var tagId in values.GeologicAgeTagIds)
            {
                var tag = new GeologicAgeTag()
                {
                    TagTypeId = tagId
                };
                entity.GeologicAgeTags.Add(tag);
            }

            entity.PhysiographicProvinceTags.Clear();
            foreach (var tagId in values.PhysiographicProvinceTagIds)
            {
                var tag = new PhysiographicProvinceTag()
                {
                    TagTypeId = tagId
                };
                entity.PhysiographicProvinceTags.Add(tag);
            }

            entity.CaveOtherTags.Clear();
            foreach (var tagId in values.OtherTagIds)
            {
                var tag = new CaveOtherTag()
                {
                    TagTypeId = tagId
                };
                entity.CaveOtherTags.Add(tag);
            }

            entity.CaveReportedByNameTags.Clear();
            foreach (var personTagTypeId in values.ReportedByNameTagIds)
            {
                var tagType = await _tagRepository.GetTag(personTagTypeId);
                tagType ??= new TagType
                {
                    Name = personTagTypeId.Trim(),
                    AccountId = RequestUser.AccountId,
                    Key = TagTypeKeyConstant.People,
                };
                var tag = new CaveReportedByNameTag
                {
                    TagType = tagType
                };

                entity.CaveReportedByNameTags.Add(tag);
            }

            if (isNewCounty) entity.CountyNumber = await Repository.GetNewDisplayId(entity.CountyId);

            if (!isNew)
                // remove entrances
                foreach (var entrance in entity.Entrances.ToList())
                {
                    var entranceValue = values.Entrances.FirstOrDefault(e => e.Id == entrance.Id);

                    if (entranceValue != null) continue;
                    entity.Entrances.Remove(entrance);
                    Repository.Delete(entrance);
                }

            foreach (var entranceValue in values.Entrances)
            {
                var isNewEntrance = string.IsNullOrWhiteSpace(entranceValue.Id);

                var entrance = isNewEntrance
                    ? new Entrance()
                    : entity.Entrances.FirstOrDefault(e => e.Id == entranceValue.Id);

                if (entrance == null) throw ApiExceptionDictionary.NotFound(nameof(entranceValue.Id));

                entrance.Name = entranceValue.Name;
                entrance.LocationQualityTagId = entranceValue.LocationQualityTagId;
                entrance.Description = entranceValue.Description;
                entrance.ReportedOn = entranceValue.ReportedOn?.ToUtcKind();
                entrance.PitDepthFeet = entranceValue.PitFeet;

                entrance.ReportedOn = entrance.ReportedOn?.ToUtcKind();

                entrance.Location =
                    new Point(entranceValue.Longitude, entranceValue.Latitude, entranceValue.ElevationFeet)
                        { SRID = 4326 };

                entrance.EntranceStatusTags.Clear();
                foreach (var tagId in entranceValue.EntranceStatusTagIds)
                {
                    var tag = new EntranceStatusTag()
                    {
                        TagTypeId = tagId
                    };
                    entrance.EntranceStatusTags.Add(tag);
                }

                entrance.FieldIndicationTags.Clear();
                foreach (var tagId in entranceValue.FieldIndicationTagIds)
                {
                    var tag = new FieldIndicationTag()
                    {
                        TagTypeId = tagId
                    };
                    entrance.FieldIndicationTags.Add(tag);
                }

                entrance.EntranceOtherTags.Clear();
                foreach (var tagId in entranceValue.EntranceOtherTagIds)
                {
                    var tag = new EntranceOtherTag
                    {
                        TagTypeId = tagId
                    };
                    entrance.EntranceOtherTags.Add(tag);
                }

                entrance.EntranceHydrologyTags.Clear();
                foreach (var tagId in entranceValue.EntranceHydrologyTagIds)
                {
                    var tag = new EntranceHydrologyTag()
                    {
                        TagTypeId = tagId
                    };
                    entrance.EntranceHydrologyTags.Add(tag);
                }

                entrance.EntranceReportedByNameTags.Clear();
                foreach (var personTagTypeId in entranceValue.ReportedByNameTagIds)
                {
                    var peopleTag = await _tagRepository.GetTag(personTagTypeId);
                    peopleTag ??= new TagType
                    {
                        Name = personTagTypeId.Trim(),
                        AccountId = RequestUser.AccountId,
                        Key = TagTypeKeyConstant.People,
                    };
                    var tag = new EntranceReportedByNameTag()
                    {
                        TagType = peopleTag
                    };
                    entrance.EntranceReportedByNameTags.Add(tag);

                }

                entrance.IsPrimary = entranceValue.IsPrimary;

                if (isNewEntrance) entity.Entrances.Add(entrance);
            }

            var blobsToDelete = new List<File>();
            if (values.Files != null)
            {
                foreach (var file in values.Files)
                {
                    var fileEntity = entity.Files.FirstOrDefault(f => f.Id == file.Id);

                    if (fileEntity == null) throw ApiExceptionDictionary.NotFound("File");

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
                        if (tagType == null) throw ApiExceptionDictionary.NotFound("Tag");

                        fileEntity.FileTypeTagId = tagType.Id;
                    }
                }

                // check if any ids are missing from the request compared to the db and delete the ones that are missing
                var missingIds = entity.Files.Select(e => e.Id).Except(values.Files.Select(f => f.Id)).ToList();
                foreach (var missingId in missingIds)
                {
                    var fileEntity = entity.Files.FirstOrDefault(f => f.Id == missingId);
                    if (fileEntity == null) continue;

                    blobsToDelete.Add(fileEntity);
                    Repository.Delete(fileEntity);
                }
            }

            if (isNew) Repository.Add(entity);

            await Repository.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            foreach (var blobProperties in blobsToDelete)
                await _fileService.DeleteFile(blobProperties.BlobKey, blobProperties.BlobContainer);

            return entity.Id;


        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

    }

    public async Task<CaveVm?> GetCave(string caveId)
    {
        var cave = await Repository.GetCave(caveId);

        if (cave == null) throw ApiExceptionDictionary.NotFound("Cave");

        foreach (var file in cave.Files)
        {
            var fileProperties = await _fileRepository.GetFileBlobProperties(file.Id);
            if (fileProperties == null || string.IsNullOrWhiteSpace(fileProperties.BlobKey) ||
                string.IsNullOrWhiteSpace(fileProperties.ContainerName)) continue;

            file.EmbedUrl =
                await _fileService.GetLink(fileProperties.BlobKey, fileProperties.ContainerName, file.FileName);
            file.DownloadUrl = await _fileService.GetLink(fileProperties.BlobKey, fileProperties.ContainerName,
                file.FileName, true);
        }

        return cave;
    }

    public async Task DeleteCave(string caveId, CancellationToken cancellationToken,
        IDbContextTransaction? transaction = null)
    {
        var outsideTransaction = transaction != null;
        transaction ??= await Repository.BeginTransactionAsync(cancellationToken);

        var files = new List<File>();
        var isSuccessful = false;
        try
        {
            var entity = await Repository.GetAsync(caveId);

            if (entity == null) throw ApiExceptionDictionary.NotFound(nameof(entity.Id));
            await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, caveId, entity.CountyId);

            foreach (var entrance in entity.Entrances)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var tag in entrance.EntranceStatusTags)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Repository.Delete(tag);
                }

                await Repository.SaveChangesAsync(cancellationToken);

                foreach (var tag in entrance.EntranceHydrologyTags)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Repository.Delete(tag);
                }

                await Repository.SaveChangesAsync(cancellationToken);

                foreach (var tag in entrance.FieldIndicationTags)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Repository.Delete(tag);
                }

                await Repository.SaveChangesAsync(cancellationToken);

                foreach (var tag in entrance.EntranceReportedByNameTags)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Repository.Delete(tag);
                }

                await Repository.SaveChangesAsync(cancellationToken);

                foreach (var tag in entrance.EntranceOtherTags)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Repository.Delete(tag);
                }

                await Repository.SaveChangesAsync(cancellationToken);

                Repository.Delete(entrance);
            }

            await Repository.SaveChangesAsync(cancellationToken);


            foreach (var tag in entity.GeologyTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var tag in entity.MapStatusTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var tag in entity.GeologicAgeTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var tag in entity.PhysiographicProvinceTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var tag in entity.BiologyTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var tag in entity.ArcheologyTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var tag in entity.CartographerNameTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var tag in entity.CaveReportedByNameTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var tag in entity.CaveOtherTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            files = entity.Files.ToList();

            Repository.Delete(entity);
            await Repository.SaveChangesAsync(cancellationToken);

            if (!outsideTransaction)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            isSuccessful = true;
        }
        catch (Exception)
        {
            if (!outsideTransaction)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }

        if (isSuccessful)
        {
            foreach (var file in files) await _fileService.DeleteFile(file.BlobKey, file.BlobContainer);
        }
    }

    public async Task ArchiveCave(string caveId)
    {

        var entity = await Repository.GetAsync(caveId);
        if (entity == null) throw ApiExceptionDictionary.NotFound(nameof(entity.Id));

        await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, caveId, entity.CountyId);


        entity.IsArchived = true;
        await Repository.SaveChangesAsync();
    }

    public async Task UnarchiveCave(string caveId)
    {
        var entity = await Repository.GetAsync(caveId);

        if (entity == null) throw ApiExceptionDictionary.NotFound(nameof(entity.Id));
        await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, caveId, entity.CountyId);

        entity.IsArchived = false;
        await Repository.SaveChangesAsync();
    }

    #endregion


}