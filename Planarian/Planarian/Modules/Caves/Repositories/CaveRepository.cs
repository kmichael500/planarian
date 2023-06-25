using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Caves.Repositories;

public class CaveRepository : RepositoryBase
{
    public CaveRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<PagedResult<CaveVm>> GetCaves(FilterQuery query)
    {
        var caves = await DbContext.Caves.Where(e => e.AccountId == RequestUser.AccountId)
            .Select(e => new CaveVm
            {
                Id = e.Id,
                Narrative = e.Narrative,
                StateId = e.StateId,
                CountyId = e.CountyId,
                DisplayId = $"{e.County.DisplayId}{e.CountyNumber}",
                Name = e.Name,
                LengthFeet = e.LengthFeet,
                DepthFeet = e.DepthFeet,
                NumberOfPits = e.NumberOfPits,
                IsArchived = e.IsArchived,
                PrimaryEntrance = e.Entrances.Where(ee => ee.IsPrimary).Select(ee =>
                    new EntranceVm
                    {
                        Id = ee.Id,
                        IsPrimary = true,
                        Latitude = ee.Latitude,
                        Longitude = ee.Longitude,
                        ElevationFeet = ee.ElevationFeet,
                        EntranceStatusTagIds = ee.EntranceStatusTags.Select(ee => ee.TagTypeId).ToList(),
                        EntranceHydrologyFrequencyTagIds = ee.EntranceHydrologyFrequencyTags
                            .Select(y => y.TagTypeId).ToList(),
                        FieldIndicationTagIds = ee.FieldIndicationTags.Select(ee => ee.TagTypeId).ToList(),
                        EntranceHydrologyTagIds =
                            ee.EntranceHydrologyTags.Select(ee => ee.TagTypeId).ToList(),
                    }).FirstOrDefault(),
                MapIds = e.Files.Select(ee => ee.Id),
                Entrances = e.Entrances.Select(ee => new EntranceVm
                    {
                        Id = ee.Id,
                        IsPrimary = ee.IsPrimary,
                        ReportedByUserId = ee.ReportedByUserId,
                        LocationQualityTagId = ee.LocationQualityTagId,
                        Name = ee.Name,
                        Description = ee.Description,
                        Latitude = ee.Latitude,
                        Longitude = ee.Longitude,
                        ElevationFeet = ee.ElevationFeet,
                        ReportedOn = ee.ReportedOn,
                        ReportedByName = ee.ReportedByName,
                        PitFeet = ee.PitFeet,
                        EntranceStatusTagIds = ee.EntranceStatusTags.Select(ee => ee.TagTypeId),
                        EntranceHydrologyFrequencyTagIds =
                            ee.EntranceHydrologyFrequencyTags.Select(e => e.TagTypeId),
                        FieldIndicationTagIds = ee.FieldIndicationTags.Select(e => e.TagTypeId),
                        EntranceHydrologyTagIds = ee.EntranceHydrologyTags.Select(e => e.TagTypeId)
                    })
                    .OrderByDescending(ee => ee.IsPrimary)
                    .ThenBy(ee => ee.ReportedOn).ToList(),
                GeologyTagIds = e.GeologyTags.Select(ee => ee.TagTypeId),
                Files = e.Files.Select(ee => new FileVm
                {
                    Id = e.Id,
                    DisplayName = ee.DisplayName,
                    FileName = ee.FileName,
                    FileTypeKey = ee.FileTypeTag.Key,
                    FileTypeTagId = ee.FileTypeTagId,
                })
            })
            .QueryFilter(query.Conditions)
            .ApplyPagingAsync(query.PageNumber, query.PageSize, e => e.LengthFeet);

        return caves;
    }

    public async Task<int> GetNewDisplayId(string countyId)
    {
        var maxCaveNumber = await DbContext.Caves
            .Where(e => e.AccountId == RequestUser.AccountId && e.CountyId == countyId)
            .MaxAsync(e => (int?)e.CountyNumber);

        // If maxCaveNumber is null, it means there are no cave numbers assigned yet.
        // In that case, return 1, otherwise increment the maximum cave number by 1.
        return maxCaveNumber.HasValue ? maxCaveNumber.Value + 1 : 1;
    }

    public async Task<CaveVm?> GetCave(string caveId)
    {
        return await DbContext.Caves.Where(e => e.Id == caveId && e.AccountId == RequestUser.AccountId)
            .Select(e => new CaveVm
            {
                Id = e.Id,
                ReportedByUserId = e.ReportedByUserId,
                StateId = e.StateId,
                CountyId = e.CountyId,
                DisplayId = $"{e.County.DisplayId}{e.CountyNumber}",
                Name = e.Name,
                LengthFeet = e.LengthFeet,
                DepthFeet = e.DepthFeet,
                MaxPitDepthFeet = e.MaxPitDepthFeet,
                NumberOfPits = e.NumberOfPits,
                Narrative = e.Narrative,
                ReportedOn = e.ReportedOn,
                ReportedByName = e.ReportedByName,
                IsArchived = e.IsArchived,
                PrimaryEntrance = e.Entrances.Where(ee=>ee.IsPrimary).Select(ee=>
                    new EntranceVm
                    {
                        Id = ee.Id,
                        IsPrimary = true,
                        Latitude = ee.Latitude,
                        Longitude = ee.Longitude,
                        ElevationFeet = ee.ElevationFeet,
                        EntranceStatusTagIds = ee.EntranceStatusTags.Select(ee => ee.TagTypeId).ToList(),
                        EntranceHydrologyFrequencyTagIds = ee.EntranceHydrologyFrequencyTags
                            .Select(y => y.TagTypeId).ToList(),
                        FieldIndicationTagIds = ee.FieldIndicationTags.Select(ee => ee.TagTypeId).ToList(),
                        EntranceHydrologyTagIds =
                            ee.EntranceHydrologyTags.Select(ee => ee.TagTypeId).ToList(),
                    }).FirstOrDefault(),
                MapIds = e.Files.Select(e => e.Id),
                Entrances = e.Entrances.Select(ee => new EntranceVm
                {
                    Id = ee.Id,
                    IsPrimary = ee.IsPrimary,
                    ReportedByUserId = ee.ReportedByUserId,
                    LocationQualityTagId = ee.LocationQualityTagId,
                    Name = ee.Name,
                    Description = ee.Description,
                    Latitude = ee.Latitude,
                    Longitude = ee.Longitude,
                    ElevationFeet = ee.ElevationFeet,
                    ReportedOn = ee.ReportedOn,
                    ReportedByName = ee.ReportedByName,
                    PitFeet = ee.PitFeet,
                    EntranceStatusTagIds = ee.EntranceStatusTags.Select(e => e.TagTypeId),
                    EntranceHydrologyFrequencyTagIds =
                        ee.EntranceHydrologyFrequencyTags.Select(e => e.TagTypeId),
                    FieldIndicationTagIds = ee.FieldIndicationTags.Select(e => e.TagTypeId),
                    EntranceHydrologyTagIds = ee.EntranceHydrologyTags.Select(e => e.TagTypeId)
                })
                    .OrderByDescending(ee=>ee.IsPrimary)
                    .ThenBy(ee=>ee.ReportedOn).ToList(),
                GeologyTagIds = e.GeologyTags.Select(e => e.TagTypeId),
                Files = e.Files.Select(ee=>new FileVm
                {
                    Id = e.Id,
                    DisplayName = ee.DisplayName,
                    FileName = ee.FileName,
                    FileTypeKey = ee.FileTypeTag.Key,
                    FileTypeTagId = ee.FileTypeTagId,
                })
            })
            .FirstOrDefaultAsync();
    }

    public async Task<Entrance?> GetEntrance(string? id)
    {
        return await DbContext.Entrances
            .Include(e => e.EntranceHydrologyTags)
            .Include(e=>e.EntranceStatusTags)
            .Include(e=>e.FieldIndicationTags)
            .Include(e=>e.EntranceHydrologyFrequencyTags)
            .Where(e => e.Id == id && e.Cave.AccountId == RequestUser.AccountId).FirstOrDefaultAsync();
    }

    public async Task<Cave?> GetAsync(string? id)
    {
        return await DbContext.Caves.Where(e => e.Id == id && e.AccountId == RequestUser.AccountId)
            .Include(e=>e.Files)
            .Include(e=>e.GeologyTags)
            .Include(e=>e.Entrances)
            .FirstOrDefaultAsync();
    }
}