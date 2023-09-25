using Microsoft.EntityFrameworkCore;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Shared.Base;
using Planarian.Shared.Exceptions;

namespace Planarian.Modules.Caves.Repositories;

public class CaveRepository : RepositoryBase
{
    public CaveRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<PagedResult<CaveVm>> GetCaves(FilterQuery filterQuery)
    {
        var query = DbContext.Caves.Where(e => e.AccountId == RequestUser.AccountId!).AsQueryable();

        if (filterQuery.Conditions.Any())
        {
            foreach (var queryCondition in filterQuery.Conditions)
            {
                switch (queryCondition.Field)
                {
                    case "Name":
                        query = queryCondition.Operator switch
                        {
                            QueryOperator.Contains => query.Where(e => e.Name.Contains(queryCondition.Value)),
                            _ => query
                        };
                        break;
                    case nameof(CaveQuery.StateId):
                        query = queryCondition.Operator switch
                        {
                            QueryOperator.Contains => query.Where(e => e.StateId == queryCondition.Value),
                            _ => query
                        };
                        break;
                    case nameof(CaveQuery.CountyId):
                        query = queryCondition.Operator switch
                        {
                            QueryOperator.Contains => query.Where(e => e.CountyId == queryCondition.Value),
                            _ => query
                        };
                        break;
                    case nameof(CaveQuery.LengthFeet):
                        var lengthHasNumber = double.TryParse(queryCondition.Value, out var lengthFeet);
                        if (!lengthHasNumber)
                            throw ApiExceptionDictionary.QueryInvalidValue(queryCondition.Field, queryCondition.Value);
                        query = queryCondition.Operator switch
                        {
                            QueryOperator.LessThan => query.Where(e => e.LengthFeet < lengthFeet),
                            QueryOperator.LessThanOrEqual => query.Where(e => e.LengthFeet <= lengthFeet),
                            QueryOperator.Equal => query.Where(e => e.LengthFeet == lengthFeet),
                            QueryOperator.GreaterThanOrEqual => query.Where(e => e.LengthFeet >= lengthFeet),
                            QueryOperator.GreaterThan => query.Where(e => e.LengthFeet > lengthFeet),
                            _ => query
                        };

                        break;
                    case nameof(CaveQuery.DepthFeet):
                        var depthHasNumber = double.TryParse(queryCondition.Value, out var depthFeet);
                        if (!depthHasNumber)
                            throw ApiExceptionDictionary.QueryInvalidValue(queryCondition.Field, queryCondition.Value);
                        query = queryCondition.Operator switch
                        {
                            QueryOperator.LessThan => query.Where(e => e.DepthFeet < depthFeet),
                            QueryOperator.LessThanOrEqual => query.Where(e => e.DepthFeet <= depthFeet),
                            QueryOperator.Equal => query.Where(e => e.DepthFeet == depthFeet),
                            QueryOperator.GreaterThanOrEqual => query.Where(e => e.DepthFeet >= depthFeet),
                            QueryOperator.GreaterThan => query.Where(e => e.DepthFeet > depthFeet),
                            _ => query
                        };
                        break;
                    case nameof(CaveQuery.NumberOfPits):
                        var numberOfPitsHasNumber = int.TryParse(queryCondition.Value, out var numberOfPits);
                        if (!numberOfPitsHasNumber)
                            throw ApiExceptionDictionary.QueryInvalidValue(queryCondition.Field, queryCondition.Value);
                        query = queryCondition.Operator switch
                        {
                            QueryOperator.LessThan => query.Where(e => e.NumberOfPits < numberOfPits),
                            QueryOperator.LessThanOrEqual => query.Where(e => e.NumberOfPits <= numberOfPits),
                            QueryOperator.Equal => query.Where(e => e.NumberOfPits == numberOfPits),
                            QueryOperator.GreaterThanOrEqual => query.Where(e => e.NumberOfPits >= numberOfPits),
                            QueryOperator.GreaterThan => query.Where(e => e.NumberOfPits > numberOfPits),
                            _ => query
                        };
                        break;
                    case nameof(CaveQuery.Narrative):
                        query = queryCondition.Operator switch
                        {
                            QueryOperator.FreeText => query.Where(e =>
                                e.Narrative != null && EF.Functions.FreeText(e.Narrative, queryCondition.Value)),
                        };
                        break;
                    case "PrimaryEntrance.entranceStatusTagIds":
                        var entranceStatusTagIds = queryCondition.Value.SplitAndTrim();
                        query = queryCondition.Operator switch
                        {
                            QueryOperator.In => query = query.Where(e =>
                                e.Entrances.SelectMany(e => e.EntranceStatusTags)
                                    .Any(e => entranceStatusTagIds.Contains(e.TagTypeId))),
                            _ => query
                        };
                        break;

                    case "PrimaryEntrance.entranceHydrologyTagIds":
                        var entranceHydrologyTagIds = queryCondition.Value.SplitAndTrim();
                        query = queryCondition.Operator switch
                        {
                            QueryOperator.In => query = query.Where(e =>
                                e.Entrances.SelectMany(e => e.EntranceHydrologyTags)
                                    .Any(e => entranceHydrologyTagIds.Contains(e.TagTypeId))),
                            _ => query
                        };
                        break;
                    case nameof(CaveQuery.GeologyTagIds):
                        var geologyTagIds = queryCondition.Value.SplitAndTrim();
                        query = queryCondition.Operator switch
                        {
                            QueryOperator.In => query = query.Where(e =>
                                e.GeologyTags.Any(e => geologyTagIds.Contains(e.TagTypeId))),
                            _ => query
                        };

                        break;
                    case "PrimaryEntrance.entranceHydrologyFrequencyTagIds":
                        var entranceHydrologyFrequencyTagIds = queryCondition.Value.SplitAndTrim();
                        query = queryCondition.Operator switch
                        {
                            QueryOperator.In => query = query.Where(e =>
                                e.Entrances.SelectMany(e => e.EntranceHydrologyFrequencyTags)
                                    .Any(e => entranceHydrologyFrequencyTagIds.Contains(e.TagTypeId))),
                            _ => query
                        };
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(queryCondition.Field));
                }
            }
        }

        var result = await query.Select(e => new CaveVm
        {
            Id = e.Id,
            CountyId = e.CountyId,
            DisplayId = $"{e.County.DisplayId}{e.CountyNumber}",
            Name = e.Name,
            LengthFeet = e.LengthFeet,
            DepthFeet = e.DepthFeet,
            PrimaryEntrance = new EntranceVm
            {
                ElevationFeet = e.Entrances.Where(e => e.IsPrimary == true).Select(e => e.Location.Z).FirstOrDefault(),
            },
            GeologyTagIds = e.GeologyTags.Select(ee => ee.TagTypeId),
        }).AsSplitQuery().ApplyPagingAsync(filterQuery.PageNumber, filterQuery.PageSize, e => e.LengthFeet);

        return result;
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
                PrimaryEntrance = e.Entrances.Where(ee => ee.IsPrimary).Select(ee =>
                    new EntranceVm
                    {
                        Id = ee.Id,
                        IsPrimary = true,
                        Latitude = ee.Location.Y,
                        Longitude = ee.Location.X,
                        ElevationFeet = ee.Location.Z,
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
                        Latitude = ee.Location.Y,
                        Longitude = ee.Location.X,
                        ElevationFeet = ee.Location.Z,
                        ReportedOn = ee.ReportedOn,
                        ReportedByName = ee.ReportedByName,
                        PitFeet = ee.PitFeet,
                        EntranceStatusTagIds = ee.EntranceStatusTags.Select(e => e.TagTypeId),
                        EntranceHydrologyFrequencyTagIds =
                            ee.EntranceHydrologyFrequencyTags.Select(e => e.TagTypeId),
                        FieldIndicationTagIds = ee.FieldIndicationTags.Select(e => e.TagTypeId),
                        EntranceHydrologyTagIds = ee.EntranceHydrologyTags.Select(e => e.TagTypeId)
                    })
                    .OrderByDescending(ee => ee.IsPrimary)
                    .ThenBy(ee => ee.ReportedOn).ToList(),
                GeologyTagIds = e.GeologyTags.Select(e => e.TagTypeId),
                Files = e.Files.Select(ee => new FileVm
                {
                    Id = ee.Id,
                    DisplayName = ee.DisplayName,
                    FileName = ee.FileName,
                    FileTypeKey = ee.FileTypeTag.Name,
                    FileTypeTagId = ee.FileTypeTagId,
                })
            })
            .AsSplitQuery()
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

    

    public record UsedCountyNumber(string CountyId, int CountyNumber);
    public async Task<HashSet<UsedCountyNumber>> GetUsedCountyNumbers()
    {
        var usedCountyNumbers = await DbContext.Caves
            .Where(e => e.AccountId == RequestUser.AccountId)
            .Select(e => new UsedCountyNumber(e.CountyId, e.CountyNumber))
            .ToListAsync();

        return usedCountyNumbers.ToHashSet();
    }

   
}

public class CaveQuery
{

    public string StateId { get; set; } = null!;
    public string CountyId { get; set; } = null!;
    public string County { get; set; } = null!;
    public double LengthFeet { get; set; }
    public double DepthFeet { get; set; }
    public int NumberOfPits { get; set; }
    public string Narrative { get; set; } = null!;
    public string PrimaryEntranceStatus { get; set; } = null!;
    public string PrimaryEntranceHydrology { get; set; } = null!;
    public string PrimaryEntranceHydrologyFrequency { get; set; } = null!;
    public string Geology { get; set; } = null!;
    public string State { get; set; } = null!;
    
    public IEnumerable<string> PrimaryEntranceEntranceHydrologyTagIds { get; set; } = new List<string>();
    public IEnumerable<string> PrimaryEntranceStatusTagIds { get; set; }= new List<string>();
    public IEnumerable<string> GeologyTagIds { get; set; }= new List<string>();
    public IEnumerable<string> PrimaryEntranceEntranceHydrologyFrequencyTagIds { get; set; }= new List<string>();
    
}