using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.DateTime;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Extensions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Caves.Repositories;

public class CaveRepository<TDbContext> : RepositoryBase<TDbContext> where TDbContext : PlanarianDbContextBase
{
    public CaveRepository(TDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<PagedResult<CaveSearchVm>> GetCaves(FilterQuery filterQuery, string? permissionKey = null)
    {
        var query = GetCavesQuery(filterQuery, permissionKey);
        
        var narrativeCondition = filterQuery.Conditions
            .FirstOrDefault(c => c.Field == nameof(CaveSearchParamsVm.Narrative));

        bool isNarrativeSearch   = narrativeCondition != null;
        string narrativeSearch   = narrativeCondition?.Value ?? string.Empty;


        var caveSearchQuery = query.Select(e => new CaveSearchVm
        {
            Id = e.Id,
            NarrativeSnippet = isNarrativeSearch
                ? FullTextSearchExtensions.TsHeadlineSimple(
                    "english", // config
                    e.Narrative, // document column
                    narrativeSearch, // search term
                    "StartSel=<mark>,StopSel=</mark>,MaxWords=30,MinWords=10,MaxFragments=100,FragmentDelimiter= ...<br><br>"
                ) // options
                : null,
            CountyId = e.County!.Id,
            DisplayId =
                $"{e.County.DisplayId}{e.Account!.CountyIdDelimiter}{e.CountyNumber}",
            PrimaryEntranceLatitude = e.Entrances.Count == 0
                ? null
                : e.Entrances.Where(ee => ee.IsPrimary == true)
                    .Select(ee => ee.Location.Y)
                    .FirstOrDefault(),
            PrimaryEntranceLongitude = e.Entrances.Count == 0
                ? null
                : e.Entrances.Where(ee => ee.IsPrimary == true)
                    .Select(ee => ee.Location.X)
                    .FirstOrDefault(),
            PrimaryEntranceElevationFeet = e.Entrances.Count == 0
                ? null
                : e.Entrances.Where(ee => ee.IsPrimary == true)
                    .Select(ee => ee.Location.Z)
                    .FirstOrDefault(),
            IsFavorite = e.Favorites.Any(favorite => favorite.UserId == RequestUser.Id),
            ReportedByTagIds = e.CaveReportedByNameTags.Select(ee => ee.TagTypeId),
            Name = e.Name,
            LengthFeet = e.LengthFeet,
            DepthFeet = e.DepthFeet,
            MaxPitDepthFeet = e.MaxPitDepthFeet,
            NumberOfPits = e.NumberOfPits,
            ReportedOn = e.ReportedOn,
            IsArchived = e.IsArchived,
            MapStatusTagIds = e.MapStatusTags.Select(ee => ee.TagTypeId),
            GeologyTagIds = e.GeologyTags.Select(ee => ee.TagTypeId),
            BiologyTagIds = e.BiologyTags.Select(ee => ee.TagTypeId),
            ArchaeologyTagIds = e.ArcheologyTags.Select(ee => ee.TagTypeId),
            CartographerNameTagIds = e.CartographerNameTags.Select(ee => ee.TagTypeId),
            GeologicAgeTagIds = e.GeologicAgeTags.Select(ee => ee.TagTypeId),
            PhysiographicProvinceTagIds = e.PhysiographicProvinceTags.Select(ee => ee.TagTypeId),
            OtherTagIds = e.CaveOtherTags.Select(ee => ee.TagTypeId),
        });

        var result = filterQuery.SortBy switch
        {
            nameof(CaveSearchVm.Name) => await caveSearchQuery.ApplyPagingAsync(filterQuery.PageNumber,
                filterQuery.PageSize,
                e => e.Name, filterQuery.SortDescending),
            nameof(CaveSearchVm.ReportedOn) => await caveSearchQuery.ApplyPagingAsync(filterQuery.PageNumber,
                filterQuery.PageSize, e => e.ReportedOn, filterQuery.SortDescending),
            nameof(CaveSearchVm.DepthFeet) => await caveSearchQuery.ApplyPagingAsync(filterQuery.PageNumber,
                filterQuery.PageSize, e => e.DepthFeet, filterQuery.SortDescending),
            nameof(CaveSearchVm.LengthFeet) => await caveSearchQuery.ApplyPagingAsync(filterQuery.PageNumber,
                filterQuery.PageSize, e => e.LengthFeet, filterQuery.SortDescending),
            nameof(CaveSearchVm.MaxPitDepthFeet) => await caveSearchQuery.ApplyPagingAsync(filterQuery.PageNumber,
                filterQuery.PageSize, e => e.MaxPitDepthFeet, filterQuery.SortDescending),
            nameof(CaveSearchVm.NumberOfPits) => await caveSearchQuery.ApplyPagingAsync(filterQuery.PageNumber,
                filterQuery.PageSize, e => e.NumberOfPits, filterQuery.SortDescending),
            _ => await caveSearchQuery.ApplyPagingAsync(filterQuery.PageNumber, filterQuery.PageSize, e => e.LengthFeet,
                filterQuery.SortDescending)
        };

        return result;
    }

    public async Task<List<CaveExportDto>> GetCavesForExport(FilterQuery filterQuery, string? permissionKey)
    {
        var query = GetCavesQuery(filterQuery, permissionKey);

        var exportData = await query.Select(c => new CaveExportDto
        {
            Id = c.Id,
            Name = c.Name,
            AlternateNames = c.AlternateNamesList,
            CountyName = c.County.Name,
            CountyDisplayId = c.County.DisplayId,
            CountyIdDelimiter = c.Account.CountyIdDelimiter,
            StateName = c.State.Name,
            CountyNumber = c.CountyNumber,
            LengthFeet = c.LengthFeet,
            DepthFeet = c.DepthFeet,
            MaxPitDepthFeet = c.MaxPitDepthFeet,
            NumberOfPits = c.NumberOfPits,
            Narrative = c.Narrative,
            ReportedOn = c.ReportedOn,
            IsArchived = c.IsArchived,
            GeologyTags = c.GeologyTags.Select(gt => gt.TagType.Name)
                .ToList(),
            MapStatusTags = c.MapStatusTags.Select(mt => mt.TagType.Name)
                .ToList(),
            GeologicAgeTags = c.GeologicAgeTags.Select(gt => gt.TagType.Name)
                .ToList(),
            PhysiographicProvinceTags = c.PhysiographicProvinceTags.Select(pt => pt.TagType.Name)
                .ToList(),
            BiologyTags = c.BiologyTags.Select(bt => bt.TagType.Name)
                .ToList(),
            ArcheologyTags = c.ArcheologyTags.Select(at => at.TagType.Name)
                .ToList(),
            CartographerNameTags = c.CartographerNameTags.Select(ct => ct.TagType.Name)
                .ToList(),
            CaveReportedByTags = c.CaveReportedByNameTags.Select(ct => ct.TagType.Name)
                .ToList(),
            CaveOtherTags = c.CaveOtherTags.Select(ct => ct.TagType.Name)
                .ToList(),
            Entrances = c.Entrances.Select(e => new EntranceExportDto
                {
                    Name = e.Name,
                    Description = e.Description,
                    IsPrimary = e.IsPrimary,
                    ReportedOn = e.ReportedOn,
                    PitDepthFeet = e.PitDepthFeet,
                    Latitude = e.Location.Y,
                    Longitude = e.Location.X,
                    Elevation = e.Location.Z,
                    LocationQuality = e.LocationQualityTag.Name,
                    EntranceStatusTags = e.EntranceStatusTags.Select(t => t.TagType.Name)
                        .ToList(),
                    FieldIndicationTags = e.FieldIndicationTags.Select(t => t.TagType.Name)
                        .ToList(),
                    EntranceHydrologyTags = e.EntranceHydrologyTags.Select(t => t.TagType.Name)
                        .ToList(),
                    EntranceReportedByTags = e.EntranceReportedByNameTags.Select(t => t.TagType.Name)
                        .ToList(),
                    EntranceOtherTags = e.EntranceOtherTags.Select(t => t.TagType.Name)
                        .ToList()
                })
                .ToList()
        }).ToListAsync();

        return exportData;
    }

    private IQueryable<Cave> GetCavesQuery(FilterQuery filterQuery, string? permissionKey = null)
    {
        var query = DbContext.Caves
            .IgnoreQueryFilters() // ignoring because it makes the query too slow, we are applying the same filter to it here so we can include a permission type
            .Where(cave =>
                DbContext.UserCavePermissionView.Any(ucp =>
                    ucp.AccountId == RequestUser.AccountId
                    && ucp.UserId == RequestUser.Id &&
                    (string.IsNullOrWhiteSpace(permissionKey) ||
                     ucp.PermissionKey ==
                     permissionKey) // gets caves that the user has access at a specific permission level for (i.e. manager)
                    && ucp.CaveId == cave.Id)).AsQueryable();

        if (!filterQuery.Conditions.Any()) return query;

        foreach (var queryCondition in filterQuery.Conditions)
            switch (queryCondition.Field)
            {
                case nameof(CaveSearchParamsVm.IsFavorite):
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.Equal => query.Where(e =>
                            e.Favorites.Any(ee =>
                                ee.UserId == RequestUser.Id && ee.AccountId == RequestUser.AccountId)),
                        QueryOperator.NotEqual => query.Where(e =>
                            !e.Favorites.Any(ee =>
                                ee.UserId == RequestUser.Id && ee.AccountId == RequestUser.AccountId)),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.Name):
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.Contains => query
                            .Where(e =>
                                e.Name.ToLower().Contains(queryCondition.Value.ToLower())
                                // || EF.Functions.ToTsVector(e.Name).Matches(EF.Functions.PlainToTsQuery(queryCondition.Value + ":*"))
                                || EF.Functions.ToTsVector(e.AlternateNames).Matches(queryCondition.Value)
                                || (
                                    queryCondition.Value.Contains(e.County.DisplayId)
                                    && queryCondition.Value.Contains(e.CountyNumber.ToString())
                                )
                            ),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.Narrative):
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.FreeText => query.Where(e =>
                            e.Narrative != null &&
                            e.NarrativeSearchVector.Matches(
                                EF.Functions.WebSearchToTsQuery("english", queryCondition.Value)
                            )
                        ),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.StateId):
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.Contains => query.Where(e => e.StateId == queryCondition.Value),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.CountyId):
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.Contains => query.Where(e => e.CountyId == queryCondition.Value),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.LengthFeet):
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
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };

                    break;
                case nameof(CaveSearchParamsVm.DepthFeet):
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
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.ElevationFeet):
                    var elevationValue = double.Parse(queryCondition.Value);
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.LessThan => query.Where(e =>
                            e.Entrances.Select(ee => ee.Location.Z)
                                .Any(ee => ee < elevationValue)),
                        QueryOperator.LessThanOrEqual => query.Where(e =>
                            e.Entrances.Select(ee => ee.Location.Z)
                                .Any(ee => ee <= elevationValue)),
                        QueryOperator.Equal => query.Where(e =>
                            e.Entrances.Select(ee => ee.Location.Z)
                                .Any(ee => ee == elevationValue)),
                        QueryOperator.GreaterThanOrEqual => query.Where(e =>
                            e.Entrances.Select(ee => ee.Location.Z)
                                .Any(ee => ee >= elevationValue)),
                        QueryOperator.GreaterThan => query.Where(e =>
                            e.Entrances.Select(ee => ee.Location.Z)
                                .Any(ee => ee > elevationValue)),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.NumberOfPits):
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
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.MaxPitDepthFeet):
                    var maxPitDepthFeetHasNumber = double.TryParse(queryCondition.Value, out var maxPitDepthFeet);
                    if (!maxPitDepthFeetHasNumber)
                        throw ApiExceptionDictionary.QueryInvalidValue(queryCondition.Field, queryCondition.Value);
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.LessThan => query.Where(e => e.MaxPitDepthFeet < maxPitDepthFeet),
                        QueryOperator.LessThanOrEqual => query.Where(e => e.MaxPitDepthFeet <= maxPitDepthFeet),
                        QueryOperator.Equal => query.Where(e => e.MaxPitDepthFeet == maxPitDepthFeet),
                        QueryOperator.GreaterThanOrEqual => query.Where(e => e.MaxPitDepthFeet >= maxPitDepthFeet),
                        QueryOperator.GreaterThan => query.Where(e => e.MaxPitDepthFeet > maxPitDepthFeet),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.MapStatusTagIds):
                    var mapStatusTagIds = queryCondition.Value.SplitAndTrim();

                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.MapStatusTags.Any(ee =>
                                mapStatusTagIds.Contains(ee.TagTypeId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.CartographerNamePeopleTagIds):
                    var cartographerNamePeopleTagIds = queryCondition.Value.SplitAndTrim();

                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.CartographerNameTags.Any(ee =>
                                cartographerNamePeopleTagIds.Contains(ee.TagTypeId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.GeologyTagIds):
                    var geologyTagIds = queryCondition.Value.SplitAndTrim();
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.GeologyTags.Any(ee => geologyTagIds.Contains(ee.TagTypeId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };

                    break;
                case nameof(CaveSearchParamsVm.GeologicAgeTagIds):
                    var geologicAgeTagIds = queryCondition.Value.SplitAndTrim();
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.GeologicAgeTags.Any(ee => geologicAgeTagIds.Contains(ee.TagTypeId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.PhysiographicProvinceTagIds):
                    var physiographicProvinceTagIds = queryCondition.Value.SplitAndTrim();
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.PhysiographicProvinceTags.Any(ee =>
                                physiographicProvinceTagIds.Contains(ee.TagTypeId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.BiologyTagIds):
                    var biologyTagIds = queryCondition.Value.SplitAndTrim();
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.BiologyTags.Any(ee =>
                                biologyTagIds.Contains(ee.TagTypeId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.ArchaeologyTagIds):
                    var archeologyTagIds = queryCondition.Value.SplitAndTrim();
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.ArcheologyTags.Any(ee =>
                                archeologyTagIds.Contains(ee.TagTypeId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.CaveReportedByNameTagIds):
                    var reportedByNameTagIds = queryCondition.Value.SplitAndTrim();

                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.CaveReportedByNameTags.Any(ee =>
                                reportedByNameTagIds.Contains(ee.TagTypeId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.CaveReportedOnDate):
                    var hasDate = DateTime.TryParse(queryCondition.Value, out var caveReportedOn);
                    if (!hasDate)
                        throw ApiExceptionDictionary.QueryInvalidValue(queryCondition.Field, queryCondition.Value);
                    caveReportedOn = caveReportedOn.ToUtcKind();
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.LessThan => query.Where(e => e.ReportedOn < caveReportedOn),
                        QueryOperator.LessThanOrEqual => query.Where(e => e.ReportedOn <= caveReportedOn),
                        QueryOperator.Equal => query.Where(e => e.ReportedOn == caveReportedOn),
                        QueryOperator.GreaterThanOrEqual => query.Where(e => e.ReportedOn >= caveReportedOn),
                        QueryOperator.GreaterThan => query.Where(e => e.ReportedOn > caveReportedOn),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.CaveOtherTagIds):
                    var caveOtherTagIds = queryCondition.Value.SplitAndTrim();

                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.CaveOtherTags.Any(ee =>
                                caveOtherTagIds.Contains(ee.TagTypeId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.EntranceStatusTagIds):
                    var entranceStatusTagIds = queryCondition.Value.SplitAndTrim();
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.Entrances.SelectMany(ee => ee.EntranceStatusTags)
                                .Any(ee => entranceStatusTagIds.Contains(ee.TagTypeId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.EntranceDescription):
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.FreeText => query.Where(e =>
                            e.Entrances.Any(ee =>
                                ee.Description != null && EF.Functions.ToTsVector(ee.Description)
                                    .Matches(queryCondition.Value))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.EntranceFieldIndicationTagIds):
                    var entranceFieldIndicationTagIds = queryCondition.Value.SplitAndTrim();
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.Entrances.SelectMany(ee => ee.FieldIndicationTags)
                                .Any(ee => entranceFieldIndicationTagIds.Contains(ee.TagTypeId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.EntrancePitDepthFeet):
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.LessThan => query.Where(e =>
                            e.Entrances.Any(ee => ee.PitDepthFeet < double.Parse(queryCondition.Value))),
                        QueryOperator.LessThanOrEqual => query.Where(e =>
                            e.Entrances.Any(ee => ee.PitDepthFeet <= double.Parse(queryCondition.Value))),
                        QueryOperator.Equal => query.Where(e =>
                            e.Entrances.Any(ee => ee.PitDepthFeet == double.Parse(queryCondition.Value))),
                        QueryOperator.GreaterThanOrEqual => query.Where(e =>
                            e.Entrances.Any(ee => ee.PitDepthFeet >= double.Parse(queryCondition.Value))),
                        QueryOperator.GreaterThan => query.Where(e =>
                            e.Entrances.Any(ee => ee.PitDepthFeet > double.Parse(queryCondition.Value))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.EntranceHydrologyTagIds):
                    var entranceHydrologyTagIds = queryCondition.Value.SplitAndTrim();
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.Entrances.SelectMany(ee => ee.EntranceHydrologyTags)
                                .Any(ee => entranceHydrologyTagIds.Contains(ee.TagTypeId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.LocationQualityTagIds):
                    var entranceLocationQualityTagIds = queryCondition.Value.SplitAndTrim();
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.Entrances.Any(ee => entranceLocationQualityTagIds.Contains(ee.LocationQualityTagId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.EntranceReportedOnDate):
                    var entranceReportedOnHasDate =
                        DateTime.TryParse(queryCondition.Value, out var entranceReportedOn);
                    if (!entranceReportedOnHasDate)
                        throw ApiExceptionDictionary.QueryInvalidValue(queryCondition.Field, queryCondition.Value);
                    entranceReportedOn = entranceReportedOn.ToUtcKind();
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.LessThan => query.Where(e =>
                            e.Entrances.Any(ee => ee.ReportedOn < entranceReportedOn)),
                        QueryOperator.LessThanOrEqual => query.Where(e =>
                            e.Entrances.Any(ee => ee.ReportedOn <= entranceReportedOn)),
                        QueryOperator.Equal => query.Where(e =>
                            e.Entrances.Any(ee => ee.ReportedOn == entranceReportedOn)),
                        QueryOperator.GreaterThanOrEqual => query.Where(e =>
                            e.Entrances.Any(ee => ee.ReportedOn >= entranceReportedOn)),
                        QueryOperator.GreaterThan => query.Where(e =>
                            e.Entrances.Any(ee => ee.ReportedOn > entranceReportedOn)),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.FileTypeTagIds):
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query.Where(e =>
                            e.Files.Any(ee => ee.FileTypeTagId.Contains(queryCondition.Value))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.FileDisplayName):
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.Contains => query.Where(e =>
                            e.Files.Any(ee =>
                                ee.DisplayName != null && ee.DisplayName.Contains(queryCondition.Value))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(queryCondition.Field));
            }

        return query;
    }

    public async Task<int> GetNewDisplayId(string countyId)
    {
        var maxCaveNumber = await DbContext.Caves
            .IgnoreQueryFilters() // need to ignore filter to calculate county number for all caves, not just ones the user has access too
            .Where(e => e.AccountId == RequestUser.AccountId && e.CountyId == countyId)
            .MaxAsync(e => (int?)e.CountyNumber);

        // If maxCaveNumber is null, it means there are no cave numbers assigned yet.
        // In that case, return 1, otherwise increment the maximum cave number by 1.
        return maxCaveNumber.HasValue ? maxCaveNumber.Value + 1 : 1;
    }

    public async Task<CaveVm?> GetCave(string? caveId)
    {
        return await DbContext.Caves.Where(e => e.Id == caveId && e.AccountId == RequestUser.AccountId)
            .Select(e => new CaveVm
            {
                Id = e.Id,
                UpdatedOn = e.CaveChangeLogs.Select(log=>log.CreatedOn).OrderDescending().FirstOrDefault(),
                IsFavorite = e.Favorites.Any(favorite=>favorite.UserId == RequestUser.Id),
                ReportedByUserId = e.ReportedByUserId,
                StateId = e.StateId,
                CountyId = e.CountyId,
                DisplayId = $"{e.County.DisplayId}{e.Account.CountyIdDelimiter}{e.CountyNumber}",
                Name = e.Name,
                AlternateNames = e.AlternateNamesList,
                LengthFeet = e.LengthFeet,
                DepthFeet = e.DepthFeet,
                MaxPitDepthFeet = e.MaxPitDepthFeet,
                NumberOfPits = e.NumberOfPits,
                Narrative = e.Narrative,
                ReportedOn = e.ReportedOn,
                ReportedByNameTagIds = e.CaveReportedByNameTags.Select(e => e.TagTypeId),
                IsArchived = e.IsArchived,
                PrimaryEntrance = e.Entrances.Where(ee => ee.IsPrimary).Select(ee =>
                    new EntranceVm
                    {
                        Id = ee.Id,
                        IsPrimary = true,
                        Latitude = ee.Location.Y,
                        Longitude = ee.Location.X,
                        ElevationFeet = ee.Location.Z,
                        EntranceStatusTagIds = ee.EntranceStatusTags.Select(eee => eee.TagTypeId).ToList(),
                        FieldIndicationTagIds = ee.FieldIndicationTags.Select(ee => ee.TagTypeId).ToList(),
                        EntranceHydrologyTagIds =
                            ee.EntranceHydrologyTags.Select(eee => eee.TagTypeId).ToList()
                    }).FirstOrDefault(),
                MapIds = e.MapStatusTags.Select(ee => ee.TagTypeId),
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
                        PitFeet = ee.PitDepthFeet,
                        EntranceStatusTagIds = ee.EntranceStatusTags.Select(eee => eee.TagTypeId),
                        FieldIndicationTagIds = ee.FieldIndicationTags.Select(eee => eee.TagTypeId),
                        EntranceHydrologyTagIds = ee.EntranceHydrologyTags.Select(eee => eee.TagTypeId),
                        ReportedByNameTagIds = ee.EntranceReportedByNameTags.Select(eee => eee.TagTypeId)
                    })
                    .OrderByDescending(ee => ee.IsPrimary)
                    .ThenBy(ee => ee.ReportedOn).ToList(),
                GeologyTagIds = e.GeologyTags.Select(ee => ee.TagTypeId),
                Files = e.Files.Select(ee => new FileVm
                {
                    Id = ee.Id,
                    DisplayName = ee.DisplayName,
                    FileName = ee.FileName,
                    FileTypeKey = ee.FileTypeTag.Name,
                    FileTypeTagId = ee.FileTypeTagId
                }).ToList(),
                BiologyTagIds = e.BiologyTags.Select(ee => ee.TagTypeId),
                ArcheologyTagIds = e.ArcheologyTags.Select(ee => ee.TagTypeId),
                CartographerNameTagIds = e.CartographerNameTags.Select(ee => ee.TagTypeId),
                GeologicAgeTagIds = e.GeologicAgeTags.Select(ee => ee.TagTypeId),
                PhysiographicProvinceTagIds = e.PhysiographicProvinceTags.Select(ee => ee.TagTypeId),
                OtherTagIds = e.CaveOtherTags.Select(ee => ee.TagTypeId),
                MapStatusTagIds = e.MapStatusTags.Select(ee => ee.TagTypeId),
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync();
    }

    public async Task<Cave?> GetAsync(string? id)
    {
        return await DbContext.Caves.Where(e => e.Id == id && e.AccountId == RequestUser.AccountId)
            .Include(e => e.GeologyTags)
            .Include(e => e.ArcheologyTags)
            .Include(e => e.BiologyTags)
            .Include(e => e.CartographerNameTags)
            .Include(e => e.MapStatusTags)
            .Include(e => e.GeologicAgeTags)
            .Include(e => e.PhysiographicProvinceTags)
            .Include(e => e.CaveOtherTags)
            .Include(e => e.CaveReportedByNameTags)
            .Include(e => e.Files)
            .Include(e => e.CaveReportedByNameTags)
            .Include(e => e.Entrances)
            .ThenInclude(entrance => entrance.EntranceStatusTags)
            .Include(e => e.Entrances)
            .ThenInclude(entrance => entrance.FieldIndicationTags)
            .Include(e => e.Entrances)
            .ThenInclude(entrance => entrance.EntranceOtherTags)
            .Include(e => e.Entrances)
            .ThenInclude(entrance => entrance.EntranceHydrologyTags)
            .Include(e => e.Entrances)
            .ThenInclude(entrance => entrance.EntranceReportedByNameTags)
            .FirstOrDefaultAsync();
    }

    public async Task<Cave?> GetCaveWithLinePlots(string caveId)
    {
        return await DbContext.Caves.Where(e => e.Id == caveId && e.AccountId == RequestUser.AccountId)
            .Include(e => e.GeoJsons)
            .FirstOrDefaultAsync();
    }

    public async Task<HashSet<UsedCountyNumber>> GetUsedCountyNumbers()
    {
        var usedCountyNumbers = await DbContext.Caves
            .Where(e => e.AccountId == RequestUser.AccountId)
            .Select(e => new UsedCountyNumber(e.CountyId, e.CountyNumber))
            .ToListAsync();

        return usedCountyNumbers.ToHashSet();
    }

    public record GetCaveForFileImportByCountyCodeNumberResult(string CaveId, string CaveName);

    public async Task<GetCaveForFileImportByCountyCodeNumberResult?> GetCaveForFileImportByCountyCodeNumber(
        string countyDisplayId, int countyNumber,
        CancellationToken cancellationToken)
    {
        var result = await DbContext.Caves
            .Where(e => e.AccountId == RequestUser.AccountId)
            .Where(e => e.County!.DisplayId == countyDisplayId && e.CountyNumber == countyNumber)
            .Select(e => new GetCaveForFileImportByCountyCodeNumberResult(e.Id,
                $"{e.County!.DisplayId}{e.Account!.CountyIdDelimiter}{e.CountyNumber} {e.Name}"))
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        return result;
    }

    public async Task<PagedResult<CaveSearchVm>> GetCavesSearch(FilterQuery filterQuery, string? permissionKey = null)
    {
        var result = await GetCaves(filterQuery, permissionKey);

        return result;
    }

    #region Favorites

    public async Task<PagedResult<FavoriteVm>> GetFavoriteCaves(FilterQuery query)
    {
        var favoriteCaves = await DbContext.Favorites
            .Where(e => e.AccountId == RequestUser.AccountId && e.UserId == RequestUser.Id)
            .OrderByDescending(e => e.CreatedOn)
            .Select(e => new FavoriteVm
            {
                CaveId = e.CaveId,
            })
            .ApplyPagingAsync(query.PageNumber, query.PageSize);

        return favoriteCaves;
    }

    public async Task<Favorite?> GetFavoriteCave(string caveId)
    {
        var favoriteCave = await GetFavoriteCaveQuery(caveId)
            .FirstOrDefaultAsync();
        
        return favoriteCave;
    }

    public async Task<FavoriteVm?> GetFavoriteCaveVm(string caveId)
    {
        var favoriteCave = await GetFavoriteCaveQuery(caveId)
            .Select(e => new FavoriteVm
            {
                CaveId = e.CaveId,
            })
            .FirstOrDefaultAsync();

        return favoriteCave;
    }

    private IQueryable<Favorite> GetFavoriteCaveQuery(string caveId)
    {
        return DbContext.Favorites
            .Where(e => e.AccountId == RequestUser.AccountId && e.UserId == RequestUser.Id && e.CaveId == caveId);
    }

    #endregion

    #region GeoJson

    public async Task<IEnumerable<CaveGeoJson>> GetCaveGeoJsonsAsync(string caveId)
    {
        return await DbContext.CaveGeoJsons.Where(c => c.CaveId == caveId).ToListAsync();
    }

    public void AddCaveGeoJson(CaveGeoJson geoJson)
    {
        DbContext.CaveGeoJsons.Add(geoJson);
    }

    public void RemoveCaveGeoJson(CaveGeoJson geoJson)
    {
        DbContext.CaveGeoJsons.Remove(geoJson);
    }
    
    #endregion
}

public class CaveRepository : CaveRepository<PlanarianDbContext>
{
    public CaveRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }
}

public record UsedCountyNumber(string CountyId, int CountyNumber);
