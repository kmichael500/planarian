using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
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
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Tags;
using Planarian.Modules.Tags.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Shared.Base;
using Planarian.Shared.Services;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Planarian.Modules.Caves.Repositories;

public class CaveRepository<TDbContext> : RepositoryBase<TDbContext> where TDbContext : PlanarianDbContextBase
{
    public CaveRepository(TDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public IQueryable<string> GetCaveIds(FilterQuery filterQuery, string? permissionKey = null)
    {
        return GetCavesQuery(filterQuery, permissionKey).Select(c => c.Id);
    }

    private const double EarthRadiusMiles = 3958.756;


    public async Task<PagedResult<CaveSearchVm>> GetCaves(FilterQuery filterQuery, string? permissionKey = null)
    {
        var query = GetCavesQuery(filterQuery, permissionKey);

        var userLatitude = filterQuery.Ulat;
        var userLongitude = filterQuery.Ulon;
        var hasLocationForDistance = userLatitude.HasValue && userLongitude.HasValue;

        double cosUserLatitude = 0;

        if (hasLocationForDistance)
        {
            var userLatitudeRadians = userLatitude!.Value * Math.PI / 180d;
            cosUserLatitude = Math.Cos(userLatitudeRadians);
        }

        var narrativeCondition = filterQuery.Conditions
            .FirstOrDefault(c => c.Field == nameof(CaveSearchParamsVm.Narrative));

        var isNarrativeSearch = narrativeCondition != null;
        var narrativeSearch = narrativeCondition?.Value ?? string.Empty;

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
            County = new SelectListItem<string>(e.County!.Name, e.County.Id),
            CountyDisplayId = e.County!.DisplayId,
            CountyNumber = e.CountyNumber,
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
            DistanceMiles = hasLocationForDistance
                ? e.Entrances
                    .Where(ee => ee.Location != null && !ee.Location.IsEmpty)
                    .Select(ee => (double?)(
                        2 * EarthRadiusMiles *
                        Math.Asin(
                            Math.Sqrt(
                                Math.Pow(
                                    Math.Sin(((ee.Location.Y - userLatitude!.Value) * Math.PI / 180d) / 2),
                                    2
                                ) +
                                cosUserLatitude *
                                Math.Cos(ee.Location.Y * Math.PI / 180d) *
                                Math.Pow(
                                    Math.Sin(((ee.Location.X - userLongitude!.Value) * Math.PI / 180d) / 2),
                                    2
                                )
                            )
                        )
                    ))
                    .Min()
                : null,
            IsFavorite = e.Favorites.Any(favorite => favorite.UserId == RequestUser.Id),
            ReportedByTags = e.CaveReportedByNameTags.Select(ee =>
                new SelectListItem<string>(ee.TagType!.Name, ee.TagTypeId)),
            Name = e.Name,
            LengthFeet = e.LengthFeet,
            DepthFeet = e.DepthFeet,
            MaxPitDepthFeet = e.MaxPitDepthFeet,
            NumberOfPits = e.NumberOfPits,
            ReportedOn = e.ReportedOn,
            IsArchived = e.IsArchived,
            MapStatusTags = e.MapStatusTags.Select(ee => new SelectListItem<string>(ee.TagType!.Name, ee.TagTypeId)),
            GeologyTags = e.GeologyTags.Select(ee => new SelectListItem<string>(ee.TagType!.Name, ee.TagTypeId)),
            BiologyTags = e.BiologyTags.Select(ee => new SelectListItem<string>(ee.TagType!.Name, ee.TagTypeId)),
            ArchaeologyTags = e.ArcheologyTags.Select(ee => new SelectListItem<string>(ee.TagType!.Name, ee.TagTypeId)),
            CartographerNameTags = e.CartographerNameTags.Select(ee =>
                new SelectListItem<string>(ee.TagType!.Name, ee.TagTypeId)),
            GeologicAgeTags = e.GeologicAgeTags.Select(ee =>
                new SelectListItem<string>(ee.TagType!.Name, ee.TagTypeId)),
            PhysiographicProvinceTags = e.PhysiographicProvinceTags.Select(ee =>
                new SelectListItem<string>(ee.TagType!.Name, ee.TagTypeId)),
            OtherTags = e.CaveOtherTags.Select(ee => new SelectListItem<string>(ee.TagType!.Name, ee.TagTypeId)),
        });

        var orderedQuery = filterQuery.SortBy switch
        {
            nameof(CaveSearchVm.Name) => ApplyStableOrdering(caveSearchQuery, e => e.Name, filterQuery.SortDescending),
            nameof(CaveSearchVm.ReportedOn) => ApplyStableOrdering(caveSearchQuery, e => e.ReportedOn,
                filterQuery.SortDescending),
            nameof(CaveSearchVm.DepthFeet) => ApplyStableOrdering(caveSearchQuery, e => e.DepthFeet,
                filterQuery.SortDescending),
            nameof(CaveSearchVm.LengthFeet) => ApplyStableOrdering(caveSearchQuery, e => e.LengthFeet,
                filterQuery.SortDescending),
            nameof(CaveSearchVm.MaxPitDepthFeet) => ApplyStableOrdering(caveSearchQuery, e => e.MaxPitDepthFeet,
                filterQuery.SortDescending),
            nameof(CaveSearchVm.NumberOfPits) => ApplyStableOrdering(caveSearchQuery, e => e.NumberOfPits,
                filterQuery.SortDescending),
            nameof(CaveSearchVm.DisplayId) => filterQuery.SortDescending
                ? caveSearchQuery
                    .OrderByDescending(e => e.CountyDisplayId)
                    .ThenByDescending(e => e.CountyNumber)
                : caveSearchQuery
                    .OrderBy(e => e.CountyDisplayId)
                    .ThenBy(e => e.CountyNumber),
            nameof(CaveSearchVm.DistanceMiles) => hasLocationForDistance
                ? ApplyStableOrdering(caveSearchQuery, e => e.DistanceMiles, !filterQuery.SortDescending)
                : ApplyStableOrdering(caveSearchQuery, e => e.LengthFeet, filterQuery.SortDescending),
            _ => ApplyStableOrdering(caveSearchQuery, e => e.LengthFeet, filterQuery.SortDescending)
        };

        var result = await orderedQuery.ApplyPagingAsync(filterQuery.PageNumber, filterQuery.PageSize);

        return result;
    }

    private static IOrderedQueryable<CaveSearchVm> ApplyStableOrdering<TKey>(
        IQueryable<CaveSearchVm> query,
        Expression<Func<CaveSearchVm, TKey>> orderingExpression,
        bool sortDescending)
    {
        return sortDescending
            ? query.OrderByDescendingNullLast(orderingExpression).ThenBy(e => e.Id)
            : query.OrderBy(orderingExpression).ThenBy(e => e.Id);
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

        double? entranceLocationLatitude = null;
        double? entranceLocationLongitude = null;
        double? entranceLocationRadiusMiles = null;
        Geometry? entranceSearchPolygon = null;

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
                case nameof(CaveSearchParamsVm.CountyDisplayId):
                    var countyDisplayId = queryCondition.Value.ToLower();
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.Contains => query.Where(e =>
                            e.County.DisplayId.ToLower() == countyDisplayId
                        ),
                        QueryOperator.Equal => query.Where(e =>
                            e.County.DisplayId.ToLower() == countyDisplayId
                        ),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.CountyNumber):
                    var hasCountyNumber = int.TryParse(queryCondition.Value, out var countyNumber);
                    if (!hasCountyNumber)
                        throw ApiExceptionDictionary.QueryInvalidValue(queryCondition.Field, queryCondition.Value);
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.Equal => query.Where(e => e.CountyNumber == countyNumber),
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

                    if (mapStatusTagIds.Length == 0)
                    {
                        break;
                    }

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

                    if (cartographerNamePeopleTagIds.Length == 0)
                    {
                        break;
                    }

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

                    if (geologyTagIds.Length == 0)
                    {
                        break;
                    }
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.GeologyTags.Any(ee => geologyTagIds.Contains(ee.TagTypeId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };

                    break;
                case nameof(CaveSearchParamsVm.GeologicAgeTagIds):
                    var geologicAgeTagIds = queryCondition.Value.SplitAndTrim();

                    if (geologicAgeTagIds.Length == 0)
                    {
                        break;
                    }
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.GeologicAgeTags.Any(ee => geologicAgeTagIds.Contains(ee.TagTypeId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.PhysiographicProvinceTagIds):
                    var physiographicProvinceTagIds = queryCondition.Value.SplitAndTrim();

                    if (physiographicProvinceTagIds.Length == 0)
                    {
                        break;
                    }
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

                    if (biologyTagIds.Length == 0)
                    {
                        break;
                    }
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

                    if (archeologyTagIds.Length == 0)
                    {
                        break;
                    }
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

                    if (reportedByNameTagIds.Length == 0)
                    {
                        break;
                    }

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

                    if (caveOtherTagIds.Length == 0)
                    {
                        break;
                    }

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

                    if (entranceStatusTagIds.Length == 0)
                    {
                        break;
                    }
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

                    if (entranceFieldIndicationTagIds.Length == 0)
                    {
                        break;
                    }
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

                    if (entranceHydrologyTagIds.Length == 0)
                    {
                        break;
                    }
                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.Entrances.SelectMany(ee => ee.EntranceHydrologyTags)
                                .Any(ee => entranceHydrologyTagIds.Contains(ee.TagTypeId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.EntranceLocation):
                    var rawLocationValue = queryCondition.Value;

                    if (string.IsNullOrWhiteSpace(rawLocationValue))
                    {
                        break;
                    }

                    var locationParts = rawLocationValue.Split(",", StringSplitOptions.RemoveEmptyEntries)
                        .Select(part => part.Trim())
                        .ToArray();

                    if (locationParts.Length != 3 ||
                        !double.TryParse(locationParts[0], NumberStyles.Float, CultureInfo.InvariantCulture,
                            out var parsedLatitude) ||
                        !double.TryParse(locationParts[1], NumberStyles.Float, CultureInfo.InvariantCulture,
                            out var parsedLongitude) ||
                        !double.TryParse(locationParts[2], NumberStyles.Float, CultureInfo.InvariantCulture,
                            out var parsedRadiusMiles))
                    {
                        throw ApiExceptionDictionary.QueryInvalidValue(queryCondition.Field, queryCondition.Value);
                    }

                    entranceLocationLatitude = parsedLatitude;
                    entranceLocationLongitude = parsedLongitude;
                    entranceLocationRadiusMiles = parsedRadiusMiles;
                    break;
                case nameof(CaveSearchParamsVm.EntrancePolygon):
                    var polygonRawValue = queryCondition.Value;

                    if (string.IsNullOrWhiteSpace(polygonRawValue))
                    {
                        entranceSearchPolygon = null;
                        break;
                    }

                    try
                    {
                        var geoJsonReader = new GeoJsonReader();
                        var parsedGeometry = geoJsonReader.Read<Geometry>(polygonRawValue);

                        if (parsedGeometry == null || parsedGeometry.IsEmpty)
                        {
                            throw ApiExceptionDictionary.QueryInvalidValue(
                                nameof(CaveSearchParamsVm.EntrancePolygon),
                                polygonRawValue);
                        }

                        parsedGeometry.SRID = 4326;

                        if (parsedGeometry is not Polygon && parsedGeometry is not MultiPolygon)
                        {
                            throw ApiExceptionDictionary.QueryInvalidValue(
                                nameof(CaveSearchParamsVm.EntrancePolygon),
                                polygonRawValue);
                        }

                        if (!parsedGeometry.IsValid)
                        {
                            parsedGeometry = parsedGeometry.Buffer(0);
                        }

                        if (!parsedGeometry.IsValid)
                        {
                            throw ApiExceptionDictionary.QueryInvalidValue(
                                nameof(CaveSearchParamsVm.EntrancePolygon),
                                polygonRawValue);
                        }

                        entranceSearchPolygon = parsedGeometry;
                    }
                    catch (JsonException)
                    {
                        throw ApiExceptionDictionary.QueryInvalidValue(
                            nameof(CaveSearchParamsVm.EntrancePolygon),
                            polygonRawValue);
                    }
                    catch (ParseException)
                    {
                        throw ApiExceptionDictionary.QueryInvalidValue(
                            nameof(CaveSearchParamsVm.EntrancePolygon),
                            polygonRawValue);
                    }
                    catch (Exception)
                    {
                        throw ApiExceptionDictionary.QueryInvalidValue(
                            nameof(CaveSearchParamsVm.EntrancePolygon),
                            polygonRawValue);
                    }
                    break;
                case nameof(CaveSearchParamsVm.EntranceReportedByPeopleTagIds):
                    var entranceReportedByPeopleTagIds = queryCondition.Value.SplitAndTrim();

                    if (entranceReportedByPeopleTagIds.Length == 0)
                    {
                        break;
                    }

                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query = query.Where(e =>
                            e.Entrances.SelectMany(ee => ee.EntranceReportedByNameTags)
                                .Any(ee => entranceReportedByPeopleTagIds.Contains(ee.TagTypeId))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;
                case nameof(CaveSearchParamsVm.LocationQualityTagIds):
                    var entranceLocationQualityTagIds = queryCondition.Value.SplitAndTrim();

                    if (entranceLocationQualityTagIds.Length == 0)
                    {
                        break;
                    }
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
                    var fileTypeTagIds = queryCondition.Value.SplitAndTrim();

                    if (fileTypeTagIds.Length == 0)
                    {
                        break;
                    }

                    query = queryCondition.Operator switch
                    {
                        QueryOperator.In => query.Where(e =>
                            e.Files.Any(ee => fileTypeTagIds.Contains(ee.FileTypeTagId))),
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
                case nameof(CaveSearchParamsVm.FileExtension):
                    var fileExtension = queryCondition.Value?.Trim();

                    if (fileExtension.IsNullOrWhiteSpace())
                    {
                        break;
                    }

                    var normalizedExtension = fileExtension!
                        .TrimStart('.')
                        .ToLowerInvariant();

                    if (normalizedExtension.IsNullOrWhiteSpace())
                    {
                        break;
                    }

                    var extensionPattern = $"%.{normalizedExtension}";

                    query = queryCondition.Operator switch
                    {
                        QueryOperator.EndsWith => query.Where(e =>
                            e.Files.Any(ee =>
                                ee.FileName != null &&
                                EF.Functions.ILike(ee.FileName, extensionPattern))),
                        QueryOperator.Equal => query.Where(e =>
                            e.Files.Any(ee =>
                                ee.FileName != null &&
                                EF.Functions.ILike(ee.FileName, extensionPattern))),
                        QueryOperator.Contains => query.Where(e =>
                            e.Files.Any(ee =>
                                ee.FileName != null &&
                                EF.Functions.ILike(ee.FileName, extensionPattern))),
                        _ => throw new ArgumentOutOfRangeException(nameof(queryCondition.Operator))
                    };
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(queryCondition.Field));
            }

        if (entranceLocationLatitude.HasValue &&
            entranceLocationLongitude.HasValue &&
            entranceLocationRadiusMiles.HasValue &&
            entranceLocationRadiusMiles.Value > 0)
        {
            var latitude = entranceLocationLatitude.Value;
            var longitude = entranceLocationLongitude.Value;
            var radiusMiles = entranceLocationRadiusMiles.Value;

            if (latitude is < -90 or > 90)
            {
                throw ApiExceptionDictionary.QueryInvalidValue(
                    nameof(CaveSearchParamsVm.EntranceLocation),
                    latitude.ToString(CultureInfo.InvariantCulture));
            }

            if (longitude is < -180 or > 180)
            {
                throw ApiExceptionDictionary.QueryInvalidValue(
                    nameof(CaveSearchParamsVm.EntranceLocation),
                    longitude.ToString(CultureInfo.InvariantCulture));
            }

            var latitudeRadians = latitude * Math.PI / 180d;
            var longitudeRadians = longitude * Math.PI / 180d;

            query = query.Where(e =>
                e.Entrances.Any(ee =>
                    ee.Location != null &&
                    !ee.Location.IsEmpty &&
                    2 * EarthRadiusMiles *
                    Math.Asin(
                        Math.Sqrt(
                            Math.Pow(
                                Math.Sin(((ee.Location.Y - latitude) * Math.PI / 180d) / 2),
                                2
                            ) +
                            Math.Cos(latitudeRadians) *
                            Math.Cos(ee.Location.Y * Math.PI / 180d) *
                            Math.Pow(
                                Math.Sin(((ee.Location.X - longitude) * Math.PI / 180d) / 2),
                                2
                            )
                        )
                    ) <= radiusMiles));
        }

        if (entranceSearchPolygon != null)
        {
            var polygon = entranceSearchPolygon;
            query = query.Where(e =>
                e.Entrances.Any(ee =>
                    ee.Location != null &&
                    !ee.Location.IsEmpty &&
                    polygon.Intersects(ee.Location)));
        }

        return query;
    }

    private IQueryable<Cave> GetCountyNumberQuery(string countyId)
    {
        return DbContext.Caves
            .IgnoreQueryFilters() // need to ignore filter to calculate county number for all caves, not just ones the user has access too
            .Where(e => e.AccountId == RequestUser.AccountId && e.CountyId == countyId);
    }

    public async Task<int> GetNewDisplayId(string countyId, bool useFirstAvailableCountyNumber = false)
    {
        if (!useFirstAvailableCountyNumber)
        {
            var maxCaveNumber = await GetCountyNumberQuery(countyId)
                .MaxAsync(e => (int?)e.CountyNumber);

            return maxCaveNumber.HasValue ? maxCaveNumber.Value + 1 : 1;
        }

        var usedCountyNumbers = await GetCountyNumberQuery(countyId)
            .Select(e => e.CountyNumber)
            .OrderBy(e => e)
            .ToListAsync();

        var nextCountyNumber = 1;

        foreach (var usedCountyNumber in usedCountyNumbers)
        {
            if (usedCountyNumber < nextCountyNumber)
                continue;

            if (usedCountyNumber == nextCountyNumber)
            {
                nextCountyNumber++;
                continue;
            }

            break;
        }

        return nextCountyNumber;
    }

    public async Task<bool> IsCountyNumberInUse(string countyId, int countyNumber, string? excludedCaveId = null)
    {
        return await GetCountyNumberQuery(countyId)
            .Where(e => e.CountyNumber == countyNumber)
            .Where(e => string.IsNullOrWhiteSpace(excludedCaveId) || e.Id != excludedCaveId)
            .AnyAsync();
    }

    public async Task<IReadOnlyList<TagNameCandidate>> GetTagCandidatesByIdsAsync(IEnumerable<string> ids,
        CancellationToken cancellationToken)
    {
        var distinct = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        return await DbContext.TagTypes.AsNoTracking().Where(tag => distinct.Contains(tag.Id))
            .Select(tag => new TagNameCandidate(tag.Id, tag.Name, tag.Key, tag.AccountId, tag.IsDefault))
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<TagNameCandidate>> GetEligiblePeopleCandidatesAsync(
        CancellationToken cancellationToken) =>
        EligiblePeopleTagLookup.GetEligibleAsync(DbContext, RequestUser.AccountId!, cancellationToken);

    public async Task<IReadOnlyList<Planarian.Model.Database.Entities.TagType>> GetTrackedTagTypesAsync(
        IEnumerable<string> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct(StringComparer.Ordinal).ToList();
        return await DbContext.TagTypes.Where(tag => distinct.Contains(tag.Id) &&
                (tag.AccountId == RequestUser.AccountId || tag.IsDefault))
            .ToListAsync(cancellationToken);
    }

    public async Task ValidateStateCountyPairAsync(string stateId, string countyId,
        CancellationToken cancellationToken)
    {
        var valid = await DbContext.Counties.AsNoTracking().AnyAsync(county =>
            county.AccountId == RequestUser.AccountId && county.Id == countyId && county.StateId == stateId &&
            DbContext.States.Any(state => state.Id == stateId), cancellationToken);
        if (!valid) throw ApiExceptionDictionary.BadRequest("The selected County does not belong to the selected State.");
    }

    public async Task<CaveVm?> GetCave(string caveId)
    {
        return await DbContext.Caves.Where(e => e.Id == caveId && e.AccountId == RequestUser.AccountId)
            .Select(e => new CaveVm
            {
                Id = e.Id,
                CurrentRevisionId = e.CurrentRevisionId,
                IsFavorite = e.Favorites.Any(favorite => favorite.UserId == RequestUser.Id),
                StateId = e.StateId,
                CountyId = e.CountyId,
                CountyDisplayId = e.County.DisplayId,
                CountyIdDelimiter = e.Account.CountyIdDelimiter,
                CountyNumber = e.CountyNumber,
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
                        LocationQualityTagId = ee.LocationQualityTagId,
                        Name = ee.Name,
                        Description = ee.Description,
                        Latitude = ee.Location.Y,
                        Longitude = ee.Location.X,
                        ElevationFeet = ee.Location.Z,
                        ReportedOn = ee.ReportedOn,
                        PitFeet = ee.PitDepthFeet,
                        EntranceStatusTagIds = ee.EntranceStatusTags.Select(eee => eee.TagTypeId).ToList(),
                        FieldIndicationTagIds = ee.FieldIndicationTags.Select(eee => eee.TagTypeId).ToList(),
                        EntranceHydrologyTagIds =
                            ee.EntranceHydrologyTags.Select(eee => eee.TagTypeId).ToList(),
                        EntranceOtherTagIds = ee.EntranceOtherTags.Select(eee => eee.TagTypeId).ToList(),
                        ReportedByNameTagIds = ee.EntranceReportedByNameTags.Select(eee => eee.TagTypeId).ToList()
                    }).FirstOrDefault(),
                MapIds = e.MapStatusTags.Select(ee => ee.TagTypeId),
                Entrances = e.Entrances.Select(ee => new EntranceVm
                {
                    Id = ee.Id,
                    IsPrimary = ee.IsPrimary,
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
                    EntranceOtherTagIds = ee.EntranceOtherTags.Select(eee => eee.TagTypeId),
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
                }),
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

    public Task<bool> HasPendingChangeRequestsAsync(string caveId, CancellationToken cancellationToken = default) =>
        DbContext.CaveChangeRequests.AsNoTracking().AnyAsync(request =>
            request.AccountId == RequestUser.AccountId && request.CaveId == caveId &&
            request.Status == CaveChangeRequestStatus.Pending, cancellationToken);

    public async Task LockForHardDeleteAsync(string caveId, CancellationToken cancellationToken = default)
    {
        var cave = await DbContext.Caves.FromSqlInterpolated(
                $"SELECT *, xmin FROM \"Caves\" WHERE \"AccountId\" = {RequestUser.AccountId} AND \"Id\" = {caveId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (cave is null) throw ApiExceptionDictionary.NotFound("Cave");
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
            .Include(e => e.Favorites)
            .Include(e => e.CavePermissions)
            .Include(e => e.Files)
            .Include(e => e.GeoJsons)
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
            .AsSplitQuery()
            .FirstOrDefaultAsync();
    }

    public async Task DeleteTagAssociationsAsync(string caveId, IEnumerable<EntityBase> associations,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw new InvalidOperationException("An active account is required to modify Cave tags.");

        var removed = associations.ToList();
        if (removed.Count == 0) return;

        async Task DeleteCaveAsync<T>(DbSet<T> set, IEnumerable<T> typed, Func<T, string> tagTypeId)
            where T : EntityBase
        {
            var ids = typed.Select(tagTypeId).Distinct(StringComparer.Ordinal).ToList();
            if (ids.Count == 0) return;
            await set.Where(tag => EF.Property<string>(tag, "CaveId") == caveId &&
                                   EF.Property<Cave>(tag, "Cave").AccountId == RequestUser.AccountId &&
                                   ids.Contains(EF.Property<string>(tag, "TagTypeId")))
                .ExecuteDeleteAsync(cancellationToken);
        }

        async Task DeleteEntranceAsync<T>(DbSet<T> set, IEnumerable<T> typed, Func<T, string> tagTypeId,
            Func<T, string> entranceId) where T : EntityBase
        {
            var rows = typed.ToList();
            var ids = rows.Select(tagTypeId).Distinct(StringComparer.Ordinal).ToList();
            if (ids.Count == 0) return;
            var entranceIds = rows.Select(entranceId).Distinct(StringComparer.Ordinal).ToList();
            await set.Where(tag => entranceIds.Contains(EF.Property<string>(tag, "EntranceId")) &&
                                   EF.Property<Entrance>(tag, "Entrance").CaveId == caveId &&
                                   EF.Property<Entrance>(tag, "Entrance").Cave.AccountId == RequestUser.AccountId &&
                                   ids.Contains(EF.Property<string>(tag, "TagTypeId")))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await DeleteCaveAsync(DbContext.ArcheologyTags, removed.OfType<ArcheologyTag>(), tag => tag.TagTypeId);
        await DeleteCaveAsync(DbContext.BiologyTags, removed.OfType<BiologyTag>(), tag => tag.TagTypeId);
        await DeleteCaveAsync(DbContext.CartographerNameTags, removed.OfType<CartographerNameTag>(), tag => tag.TagTypeId);
        await DeleteCaveAsync(DbContext.CaveOtherTags, removed.OfType<CaveOtherTag>(), tag => tag.TagTypeId);
        await DeleteCaveAsync(DbContext.CaveReportedByNameTags, removed.OfType<CaveReportedByNameTag>(), tag => tag.TagTypeId);
        await DeleteCaveAsync(DbContext.GeologicAgeTags, removed.OfType<GeologicAgeTag>(), tag => tag.TagTypeId);
        await DeleteCaveAsync(DbContext.GeologyTags, removed.OfType<GeologyTag>(), tag => tag.TagTypeId);
        await DeleteCaveAsync(DbContext.MapStatusTags, removed.OfType<MapStatusTag>(), tag => tag.TagTypeId);
        await DeleteCaveAsync(DbContext.PhysiographicProvinceTags, removed.OfType<PhysiographicProvinceTag>(), tag => tag.TagTypeId);
        await DeleteEntranceAsync(DbContext.EntranceStatusTags, removed.OfType<EntranceStatusTag>(),
            tag => tag.TagTypeId, tag => tag.EntranceId);
        await DeleteEntranceAsync(DbContext.EntranceHydrologyTags, removed.OfType<EntranceHydrologyTag>(),
            tag => tag.TagTypeId, tag => tag.EntranceId);
        await DeleteEntranceAsync(DbContext.FieldIndicationTags, removed.OfType<FieldIndicationTag>(),
            tag => tag.TagTypeId, tag => tag.EntranceId);
        await DeleteEntranceAsync(DbContext.EntranceOtherTag, removed.OfType<EntranceOtherTag>(),
            tag => tag.TagTypeId, tag => tag.EntranceId);
        await DeleteEntranceAsync(DbContext.EntranceReportedByNameTags, removed.OfType<EntranceReportedByNameTag>(),
            tag => tag.TagTypeId, tag => tag.EntranceId);

        foreach (var association in removed)
            DbContext.Entry(association).State = EntityState.Detached;
    }

    public async Task<IReadOnlySet<string>> GetPublishedFileIdsAsync(string caveId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw new InvalidOperationException("An active account is required to read Cave files.");

        var ids = await DbContext.Files.AsNoTracking()
            .Where(file => file.AccountId == RequestUser.AccountId && file.CaveId == caveId)
            .Select(file => file.Id)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<Planarian.Model.Database.Entities.RidgeWalker.File>>
        AttachAuthoringStagedFilesAsync(string caveId, IEnumerable<string> fileIds,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId) || string.IsNullOrWhiteSpace(RequestUser.Id))
            throw new InvalidOperationException("An active account user is required to publish staged files.");
        var ids = fileIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return [];

        var files = new List<Planarian.Model.Database.Entities.RidgeWalker.File>(ids.Count);
        foreach (var id in ids)
        {
            var file = await DbContext.Files.FromSqlInterpolated(
                    $"SELECT * FROM \"Files\" WHERE \"AccountId\" = {RequestUser.AccountId} AND \"Id\" = {id} FOR UPDATE")
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(cancellationToken);
            if (file is null || file.CreatedByUserId != RequestUser.Id || file.CaveId is not null ||
                file.ExpiresOn is null || file.ExpiresOn <= DateTime.UtcNow ||
                await DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters().AnyAsync(staged =>
                    staged.AccountId == RequestUser.AccountId && staged.FileId == file.Id, cancellationToken) ||
                string.IsNullOrWhiteSpace(file.BlobKey) || string.IsNullOrWhiteSpace(file.BlobContainer))
                throw ApiExceptionDictionary.NotFound("Staged file");
            files.Add(file);
        }

        foreach (var file in files)
        {
            file.CaveId = caveId;
            file.ExpiresOn = null;
        }
        return files;
    }

    public async Task RetainPublishedFileObjectAsync(
        Planarian.Model.Database.Entities.RidgeWalker.File file, string caveId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId) || file.AccountId != RequestUser.AccountId ||
            file.CaveId != caveId)
            throw new InvalidOperationException("The File is not a current File for this account/Cave.");
        if (string.IsNullOrWhiteSpace(file.BlobKey) && string.IsNullOrWhiteSpace(file.BlobContainer)) return;
        if (string.IsNullOrWhiteSpace(file.BlobKey) || string.IsNullOrWhiteSpace(file.BlobContainer))
            throw new InvalidOperationException("The File has an incomplete object-storage address.");

        var existing = await DbContext.RetainedCaveFileObjects.IgnoreQueryFilters()
            .SingleOrDefaultAsync(row => row.AccountId == RequestUser.AccountId && row.FileId == file.Id,
                cancellationToken);
        if (existing is null)
        {
            DbContext.RetainedCaveFileObjects.Add(new RetainedCaveFileObject
            {
                AccountId = RequestUser.AccountId,
                CaveId = caveId,
                FileId = file.Id,
                StoragePartition = file.BlobContainer,
                StorageKey = file.BlobKey
            });
            return;
        }

        if (existing.CaveId != caveId ||
            !string.Equals(existing.StoragePartition, file.BlobContainer, StringComparison.Ordinal) ||
            !string.Equals(existing.StorageKey, file.BlobKey, StringComparison.Ordinal))
            throw new InvalidOperationException("Historical File identity cannot be repointed to different content.");
    }

    public async Task<IReadOnlyList<StorageObjectAddress>> RemoveRetainedFileObjectsForHardDeleteAsync(
        string caveId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw new InvalidOperationException("An active account is required to delete retained File objects.");
        var rows = await DbContext.RetainedCaveFileObjects.IgnoreQueryFilters()
            .Where(row => row.AccountId == RequestUser.AccountId && row.CaveId == caveId)
            .ToListAsync(cancellationToken);
        DbContext.RetainedCaveFileObjects.RemoveRange(rows);
        return rows.Select(row => new StorageObjectAddress(row.StoragePartition, row.StorageKey))
            .Distinct().ToList();
    }

    public async Task DeleteStagedFileReferencesAsync(IEnumerable<string> fileIds,
        CancellationToken cancellationToken = default)
    {
        var ids = fileIds.Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return;
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw new InvalidOperationException("An active account is required to delete staged file references.");

        await DbContext.Set<CaveChangeRequestStagedFile>()
            .IgnoreQueryFilters()
            .Where(staged => staged.AccountId == RequestUser.AccountId && ids.Contains(staged.FileId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<List<Planarian.Model.Database.Entities.RidgeWalker.File>> AttachStagedFilesAsync(
        string changeRequestId, string caveId, IReadOnlyList<StagedCaveFilePublication> publications,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw new InvalidOperationException("An active account is required to publish staged files.");
        var byId = publications.ToDictionary(publication => publication.FileId, StringComparer.Ordinal);
        var ids = byId.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return [];

        var stagedIds = await DbContext.Set<CaveChangeRequestStagedFile>().IgnoreQueryFilters()
            .Where(staged => staged.AccountId == RequestUser.AccountId &&
                             staged.ChangeRequestId == changeRequestId && ids.Contains(staged.FileId))
            .Select(staged => staged.FileId).ToListAsync(cancellationToken);
        if (stagedIds.Count != ids.Count) throw ApiExceptionDictionary.NotFound("Staged file");

        var files = new List<Planarian.Model.Database.Entities.RidgeWalker.File>(ids.Count);
        foreach (var id in ids)
        {
            var file = await DbContext.Files.FromSqlInterpolated(
                    $"SELECT * FROM \"Files\" WHERE \"AccountId\" = {RequestUser.AccountId} AND \"Id\" = {id} FOR UPDATE")
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(cancellationToken);
            if (file is not null && file.CaveId is null) files.Add(file);
        }
        if (files.Count != ids.Count) throw ApiExceptionDictionary.NotFound("Staged file");

        var sharedPendingFileId = await (from staged in DbContext.Set<CaveChangeRequestStagedFile>().IgnoreQueryFilters()
                join request in DbContext.CaveChangeRequests.IgnoreQueryFilters()
                    on new { staged.AccountId, Id = staged.ChangeRequestId }
                    equals new { request.AccountId, request.Id }
                where staged.AccountId == RequestUser.AccountId && ids.Contains(staged.FileId) &&
                      staged.ChangeRequestId != changeRequestId &&
                      request.Status == CaveChangeRequestStatus.Pending
                select staged.FileId)
            .FirstOrDefaultAsync(cancellationToken);
        if (sharedPendingFileId is not null)
            throw ApiExceptionDictionary.BadRequest(
                "A staged file shared with another pending request cannot be published.");

        foreach (var file in files)
        {
            var publication = byId[file.Id];
            if (!string.Equals(file.BlobKey, publication.StorageKey, StringComparison.Ordinal) ||
                !string.Equals(file.BlobContainer, publication.StoragePartition, StringComparison.Ordinal))
                throw ApiExceptionDictionary.NotFound("Staged file");
            file.CaveId = caveId;
            file.ExpiresOn = null;
        }

        await DbContext.Set<CaveChangeRequestStagedFile>().IgnoreQueryFilters()
            .Where(staged => staged.AccountId == RequestUser.AccountId &&
                             staged.ChangeRequestId == changeRequestId && stagedIds.Contains(staged.FileId))
            .ExecuteDeleteAsync(cancellationToken);
        return files;
    }

    public async Task<IReadOnlyList<GeoJsonUploadVm>> GetCaveLinePlotsAsync(string caveId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.CaveGeoJsons.AsNoTracking()
            .Where(linePlot => linePlot.CaveId == caveId &&
                               linePlot.Cave.AccountId == RequestUser.AccountId &&
                               DbContext.UserCavePermissionView.Any(permission =>
                                   permission.AccountId == RequestUser.AccountId &&
                                   permission.UserId == RequestUser.Id && permission.CaveId == caveId))
            .OrderBy(linePlot => linePlot.Id)
            .Select(linePlot => new GeoJsonUploadVm
            {
                Id = linePlot.Id,
                Name = linePlot.Name,
                GeoJson = linePlot.GeoJson
            }).ToListAsync(cancellationToken);
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

    public async Task<List<ImportSyncCaveLookup>> GetImportSyncCaves()
    {
        return await DbContext.Caves
            .IgnoreQueryFilters()
            .Where(e => e.AccountId == RequestUser.AccountId)
            .Select(e => new ImportSyncCaveLookup(
                e.Id,
                e.StateId,
                e.State.Abbreviation,
                e.CountyId,
                e.County.Name,
                e.County.DisplayId,
                e.CountyNumber,
                e.Name,
                e.AlternateNames,
                e.LengthFeet,
                e.DepthFeet,
                e.MaxPitDepthFeet,
                e.NumberOfPits,
                e.Narrative,
                e.ReportedOn,
                e.IsArchived,
                e.GeologyTags.Select(tag => tag.TagTypeId),
                e.GeologicAgeTags.Select(tag => tag.TagTypeId),
                e.MapStatusTags.Select(tag => tag.TagTypeId),
                e.PhysiographicProvinceTags.Select(tag => tag.TagTypeId),
                e.ArcheologyTags.Select(tag => tag.TagTypeId),
                e.BiologyTags.Select(tag => tag.TagTypeId),
                e.CaveOtherTags.Select(tag => tag.TagTypeId),
                e.CartographerNameTags.Select(tag => tag.TagTypeId),
                e.CaveReportedByNameTags.Select(tag => tag.TagTypeId)))
            .ToListAsync();
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
public record ImportSyncCaveLookup(
    string Id,
    string StateId,
    string StateAbbreviation,
    string CountyId,
    string CountyName,
    string CountyDisplayId,
    int CountyNumber,
    string Name,
    string? AlternateNames,
    double? LengthFeet,
    double? DepthFeet,
    double? MaxPitDepthFeet,
    int? NumberOfPits,
    string? Narrative,
    DateTime? ReportedOn,
    bool IsArchived,
    IEnumerable<string> GeologyTagIds,
    IEnumerable<string> GeologicAgeTagIds,
    IEnumerable<string> MapStatusTagIds,
    IEnumerable<string> PhysiographicProvinceTagIds,
    IEnumerable<string> ArcheologyTagIds,
    IEnumerable<string> BiologyTagIds,
    IEnumerable<string> OtherTagIds,
    IEnumerable<string> CartographerNameTagIds,
    IEnumerable<string> ReportedByNameTagIds);
