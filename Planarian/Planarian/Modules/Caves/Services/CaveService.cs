using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Shared.Base;
using Planarian.Shared.Exceptions;

namespace Planarian.Modules.Caves.Services;

public class CaveService : ServiceBase<CaveRepository>
{
    public CaveService(CaveRepository repository, RequestUser requestUser) : base(repository, requestUser)
    {
    }

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

            // must be at least one entrance
            if (values.Entrances == null || !values.Entrances.Any())
            {
                throw ApiExceptionDictionary.EntranceRequired("At least 1 entrance is required!");
            }

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

            var numberOfPrimaryEntrances = values.Entrances.Count(e => e.IsPrimary);
            if (numberOfPrimaryEntrances != 1)
            {
                throw ApiExceptionDictionary.BadRequest("Only one entrance can be primary!");
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
                entrance.Latitude = entranceValue.Latitude;
                entrance.Longitude = entranceValue.Longitude;
                entrance.ElevationFeet = entranceValue.ElevationFeet;
                entrance.ReportedOn = entranceValue.ReportedOn ?? DateTime.UtcNow;
                entrance.PitFeet = entranceValue.PitFeet;

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

            if (isNew)
            {
                Repository.Add(entity);
            }

            await Repository.SaveChangesAsync();

            await transaction.CommitAsync();

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
        return cave;
    }

    public async Task DeleteCave(string caveId)
    {
        var entity = await Repository.GetAsync(caveId);

        if (entity == null)
        {
            throw ApiExceptionDictionary.NotFound(nameof(entity.Id));
        }

        Repository.Delete(entity);
        await Repository.SaveChangesAsync();
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
}