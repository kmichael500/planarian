using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.DateTime;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Import.Models;

namespace Planarian.Modules.Import.Planning;

/// <summary>
/// Pure Entrance import planning. Parsing, normalization, account-scoped
/// resolution, intended-state validation and preview inputs happen without a
/// transaction and without tracked/persistent writes.
/// </summary>
public sealed class EntranceImportPlanner
{
    private readonly PlanarianDbContext _db;
    private readonly AccountExecutionScope _scope;

    public EntranceImportPlanner(PlanarianDbContext db, RequestUser requestUser)
    {
        _db = db;
        _scope = AccountExecutionScope.Require(requestUser);
    }

    public async Task<EntranceImportPlan> PlanAsync(Stream stream, bool syncExisting,
        CancellationToken cancellationToken = default)
    {
        var failedRecords = new List<FailedCaveCsvRecord<EntranceCsvModel>>();
        var records = await ParseAsync(stream, failedRecords, cancellationToken);
        var tagSets = await ResolveTagsAsync(records, cancellationToken);

        var parsed = new List<NormalizedEntranceRow>(records.Count);
        for (var index = 0; index < records.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = records[index];
            var validationRowNumber = index + 1;
            var failureCountBefore = failedRecords.Count;
            try
            {
                var validDate = DateTime.TryParse(record.ReportedOnDate, out var reportedOn);
                reportedOn = reportedOn.ToUtcKind();
                if (!record.DecimalLatitude.HasValue)
                {
                    failedRecords.Add(new(record, validationRowNumber,
                        $"Missing value for {nameof(record.DecimalLatitude)}"));
                    continue;
                }
                if (!record.DecimalLongitude.HasValue)
                {
                    failedRecords.Add(new(record, validationRowNumber,
                        $"Missing value for {nameof(record.DecimalLongitude)}"));
                    continue;
                }
                if (!record.EntranceElevationFt.HasValue)
                {
                    failedRecords.Add(new(record, validationRowNumber,
                        $"Missing value for {nameof(record.EntranceElevationFt)}"));
                    continue;
                }
                if (!int.TryParse(record.CountyCaveNumber, out var countyCaveNumber))
                {
                    failedRecords.Add(new(record, validationRowNumber,
                        $"Missing value for {nameof(record.CountyCaveNumber)}"));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(record.CountyCode))
                {
                    failedRecords.Add(new(record, validationRowNumber,
                        $"Missing value for {nameof(record.CountyCode)}"));
                    continue;
                }

                var locationQuality = ResolveTag(tagSets, TagTypeKeyConstant.LocationQuality,
                    record.LocationQuality ?? string.Empty);
                if (locationQuality is null)
                {
                    failedRecords.Add(new(record, validationRowNumber,
                        $"Missing value for {nameof(record.LocationQuality)}"));
                    continue;
                }

                var normalized = new NormalizedEntranceRow(
                    IdGenerator.Generate(), index, record,
                    record.CountyCode, countyCaveNumber,
                    record.EntranceName?.Trim(), record.IsPrimaryEntrance ?? false,
                    record.EntranceDescription?.Trim(), record.DecimalLatitude.Value,
                    record.DecimalLongitude.Value, record.EntranceElevationFt.Value,
                    locationQuality, validDate ? reportedOn : null, record.EntrancePitDepth,
                    ResolveMany(tagSets, TagTypeKeyConstant.EntranceStatus, record.EntranceStatuses),
                    ResolveMany(tagSets, TagTypeKeyConstant.EntranceHydrology, record.EntranceHydrology),
                    ResolveMany(tagSets, TagTypeKeyConstant.FieldIndication, record.FieldIndication),
                    ResolveMany(tagSets, TagTypeKeyConstant.People, record.ReportedByNames));

                ValidateNormalized(normalized, validationRowNumber, failedRecords);
                if (failedRecords.Count == failureCountBefore) parsed.Add(normalized);
            }
            catch (Exception exception)
            {
                failedRecords.Add(new(record, validationRowNumber, exception.Message));
            }
        }

        ThrowIfInvalid(failedRecords);

        var keys = parsed.Select(row => (row.CountyDisplayId, row.CountyCaveNumber)).Distinct().ToList();
        var countyCodes = keys.Select(k => k.CountyDisplayId).Distinct(StringComparer.Ordinal).ToList();
        var caveNumbers = keys.Select(k => k.CountyCaveNumber).Distinct().ToList();

        var candidateCaves = await _db.Caves.IgnoreQueryFilters()
            .Where(c => c.AccountId == _scope.AccountId &&
                        countyCodes.Contains(c.County.DisplayId) && caveNumbers.Contains(c.CountyNumber))
            .AsNoTracking()
            .Select(c => new
            {
                c.Id, c.Name, CountyDisplayId = c.County.DisplayId, c.CountyNumber,
                c.Version, c.CurrentRevisionId
            })
            .ToListAsync(cancellationToken);
        var caveByKey = candidateCaves.ToDictionary(c => (c.CountyDisplayId, c.CountyNumber));

        foreach (var row in parsed)
        {
            if (caveByKey.ContainsKey((row.CountyDisplayId, row.CountyCaveNumber))) continue;
            failedRecords.Add(new(row.Source, row.SourceIndex + 2,
                $"Entrance could not be associated with the cave {row.CountyDisplayId}-{row.CountyCaveNumber}"));
        }
        ThrowIfInvalid(failedRecords);

        var caveIds = candidateCaves.Select(c => c.Id).Distinct().ToList();
        var existingCounts = caveIds.Count == 0
            ? new Dictionary<string, int>()
            : await _db.Entrances.IgnoreQueryFilters()
                .Where(e => caveIds.Contains(e.CaveId) && e.Cave != null && e.Cave.AccountId == _scope.AccountId)
                .AsNoTracking()
                .GroupBy(e => e.CaveId)
                .Select(g => new { CaveId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(e => e.CaveId, e => e.Count, cancellationToken);
        var existingPrimaryCounts = caveIds.Count == 0
            ? new Dictionary<string, int>()
            : await _db.Entrances.IgnoreQueryFilters()
                .Where(e => caveIds.Contains(e.CaveId) && e.IsPrimary &&
                            e.Cave != null && e.Cave.AccountId == _scope.AccountId)
                .AsNoTracking()
                .GroupBy(e => e.CaveId)
                .Select(g => new { CaveId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(e => e.CaveId, e => e.Count, cancellationToken);

        var planned = new List<PlannedEntrance>(parsed.Count);
        foreach (var row in parsed)
        {
            var cave = caveByKey[(row.CountyDisplayId, row.CountyCaveNumber)];
            var tags = new List<PlannedEntranceTag>();
            AddTags(tags, row.Id, row.StatusTags, EntranceImportTagRole.Status);
            AddTags(tags, row.Id, row.HydrologyTags, EntranceImportTagRole.Hydrology);
            AddTags(tags, row.Id, row.FieldIndicationTags, EntranceImportTagRole.FieldIndication);
            AddTags(tags, row.Id, row.ReportedByTags, EntranceImportTagRole.ReportedBy);
            planned.Add(new PlannedEntrance(
                row.Id, row.SourceIndex + 2, cave.Id, row.CountyDisplayId, row.CountyCaveNumber, cave.Name,
                row.Name, row.IsPrimary, row.Description, row.Latitude, row.Longitude, row.Elevation,
                row.LocationQuality.Id, row.LocationQuality.Name, row.ReportedOn, row.PitDepthFeet, tags,
                row.StatusTags.Select(t => t.Name).ToList(),
                row.HydrologyTags.Select(t => t.Name).ToList(),
                row.FieldIndicationTags.Select(t => t.Name).ToList(),
                row.ReportedByTags.Select(t => t.Name).ToList()));
        }

        ValidatePrimaryCounts(planned, existingPrimaryCounts, syncExisting, failedRecords);
        ThrowIfInvalid(failedRecords);

        var targets = candidateCaves
            .Where(c => planned.Any(row => row.CaveId == c.Id))
            .ToDictionary(c => c.Id, c => new EntranceImportCaveTarget(
                c.Id, c.Name, c.CountyDisplayId, c.CountyNumber, c.Version, c.CurrentRevisionId,
                existingCounts.GetValueOrDefault(c.Id), existingPrimaryCounts.GetValueOrDefault(c.Id)),
                StringComparer.Ordinal);
        var tagNamesById = tagSets.All
            .GroupBy(t => t.Id)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.Ordinal);

        return new EntranceImportPlan(_scope.AccountId, syncExisting, planned,
            tagSets.Creations.ToList(), tagNamesById, targets);
    }

    private static void AddTags(ICollection<PlannedEntranceTag> result, string entranceId,
        IEnumerable<ImportTagLookup> tags, EntranceImportTagRole role)
    {
        foreach (var tag in tags)
            result.Add(new PlannedEntranceTag(IdGenerator.Generate(), entranceId, tag.Id, role));
    }

    private static void ValidatePrimaryCounts(IReadOnlyList<PlannedEntrance> planned,
        IReadOnlyDictionary<string, int> existingPrimaryCounts, bool syncExisting,
        List<FailedCaveCsvRecord<EntranceCsvModel>> failedRecords)
    {
        foreach (var group in planned.GroupBy(row => row.CaveId))
        {
            var existingPrimary = syncExisting ? 0 : existingPrimaryCounts.GetValueOrDefault(group.Key);
            var importedPrimary = group.Count(row => row.IsPrimary);
            var finalPrimaryCount = existingPrimary + importedPrimary;
            if (finalPrimaryCount == 1) continue;

            var first = group.First();
            if (finalPrimaryCount == 0)
            {
                failedRecords.Add(new(first.SourceModel(), first.CsvRowNumber,
                    $"No primary entrance found for cave {first.CountyDisplayId}-{first.CountyCaveNumber}"));
                continue;
            }

            foreach (var offending in group.Where(row => row.IsPrimary))
                failedRecords.Add(new(offending.SourceModel(), offending.CsvRowNumber,
                    $"Entrance is marked as primary but there is already a primary entrance for the cave {offending.CountyDisplayId}-{offending.CountyCaveNumber}"));
        }
    }

    private Task<ImportTagResolutionSet> ResolveTagsAsync(IReadOnlyList<EntranceCsvModel> records,
        CancellationToken cancellationToken)
    {
        var requested = new List<(string Key, IEnumerable<string?> Names)>
        {
            (TagTypeKeyConstant.LocationQuality, records.Select(r => r.LocationQuality)),
            (TagTypeKeyConstant.EntranceStatus, records.SelectMany(r => r.EntranceStatuses.SplitAndTrim())),
            (TagTypeKeyConstant.EntranceHydrology, records.SelectMany(r => r.EntranceHydrology.SplitAndTrim())),
            (TagTypeKeyConstant.FieldIndication, records.SelectMany(r => r.FieldIndication.SplitAndTrim())),
            (TagTypeKeyConstant.People, records.SelectMany(r => r.ReportedByNames.SplitAndTrim()))
        };
        return ImportTagResolver.ResolveAsync(_db, _scope.AccountId, requested, cancellationToken);
    }

    private static ImportTagLookup? ResolveTag(ImportTagResolutionSet set, string key, string name) =>
        ImportTagResolver.Resolve(set, key, name);

    private static List<ImportTagLookup> ResolveMany(ImportTagResolutionSet set, string key, string? raw) =>
        ImportTagResolver.ResolveMany(set, key, raw.SplitAndTrim());

    private static void ValidateNormalized(NormalizedEntranceRow row, int rowNumber,
        List<FailedCaveCsvRecord<EntranceCsvModel>> failedRecords)
    {
        if (row.CountyDisplayId.Length > PropertyLength.SmallText)
            failedRecords.Add(new(row.Source, rowNumber,
                $"CountyDisplayId exceeds the maximum allowed length of {PropertyLength.SmallText}"));
        if (row.Name is { Length: > PropertyLength.Name })
            failedRecords.Add(new(row.Source, rowNumber,
                $"Name exceeds the maximum allowed length of {PropertyLength.Name}"));
        if (row.Latitude is > 90 or < -90)
            failedRecords.Add(new(row.Source, rowNumber, "Latitude must be between -90 and 90!"));
        if (row.Longitude is > 180 or < -180)
            failedRecords.Add(new(row.Source, rowNumber, "Longitude must be between -180 and 180!"));
        if (row.Elevation < 0)
            failedRecords.Add(new(row.Source, rowNumber, "Elevation must be greater than or equal to 0!"));
        if (row.PitDepthFeet < 0)
            failedRecords.Add(new(row.Source, rowNumber, "Pit depth must be greater than or equal to 0!"));
    }

    private static void ThrowIfInvalid(List<FailedCaveCsvRecord<EntranceCsvModel>> failedRecords)
    {
        if (failedRecords.Count == 0) return;
        throw ApiExceptionDictionary.InvalidImport(failedRecords.OrderBy(e => e.RowNumber).ToList(), ImportType.Entrance);
    }

    private static async Task<List<EntranceCsvModel>> ParseAsync(Stream stream,
        List<FailedCaveCsvRecord<EntranceCsvModel>> failedRecords, CancellationToken cancellationToken)
    {
        var records = new List<EntranceCsvModel>();
        using var reader = new StreamReader(stream);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null };
        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<EntranceCsvModelMap>();
        if (!await csv.ReadAsync()) return records;
        csv.ReadHeader();
        var rowNumber = 1;
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            var record = new EntranceCsvModel();
            var errors = new List<string>();
            TryGet(csv, nameof(record.CountyCode), true, errors, out string? countyCode); record.CountyCode = countyCode;
            TryGet(csv, nameof(record.CountyCaveNumber), true, errors, out string? countyCaveNumber); record.CountyCaveNumber = countyCaveNumber;
            TryGet(csv, nameof(record.EntranceName), false, errors, out string? name); record.EntranceName = name;
            TryGet(csv, nameof(record.DecimalLatitude), true, errors, out double latitude); record.DecimalLatitude = latitude;
            TryGet(csv, nameof(record.DecimalLongitude), true, errors, out double longitude); record.DecimalLongitude = longitude;
            TryGet(csv, nameof(record.EntranceElevationFt), true, errors, out double elevation); record.EntranceElevationFt = elevation;
            TryGet(csv, nameof(record.LocationQuality), true, errors, out string? locationQuality); record.LocationQuality = locationQuality ?? string.Empty;
            TryGet(csv, nameof(record.EntranceDescription), false, errors, out string? description); record.EntranceDescription = description;
            TryGet(csv, nameof(record.EntrancePitDepth), false, errors, out double? pit); record.EntrancePitDepth = pit;
            TryGet(csv, nameof(record.EntranceStatuses), false, errors, out string? status); record.EntranceStatuses = status;
            TryGet(csv, nameof(record.EntranceHydrology), false, errors, out string? hydrology); record.EntranceHydrology = hydrology;
            TryGet(csv, nameof(record.FieldIndication), false, errors, out string? field); record.FieldIndication = field;
            TryGet(csv, nameof(record.ReportedOnDate), false, errors, out string? reportedOn); record.ReportedOnDate = reportedOn;
            TryGet(csv, nameof(record.ReportedByNames), false, errors, out string? reportedBy); record.ReportedByNames = reportedBy;
            TryGet(csv, nameof(record.IsPrimaryEntrance), false, errors, out bool primary); record.IsPrimaryEntrance = primary;
            if (errors.Count == 0) records.Add(record);
            else foreach (var error in errors) failedRecords.Add(new(record, rowNumber, error));
        }
        ThrowIfInvalid(failedRecords);
        return records;
    }

    private static bool TryGet<T>(IReaderRow csv, string fieldName, bool required, ICollection<string> errors,
        out T? value)
    {
        var hasValue = csv.TryGetField(fieldName, out value);
        if (!hasValue || (typeof(T) == typeof(string) && string.IsNullOrWhiteSpace(value?.ToString())))
        {
            if (required) errors.Add($"{fieldName} is required.");
            return false;
        }
        if (typeof(T) == typeof(string) && value != null) value = (T)(object)value.ToString()!.Trim();
        return true;
    }

    private sealed record NormalizedEntranceRow(
        string Id, int SourceIndex, EntranceCsvModel Source, string CountyDisplayId, int CountyCaveNumber,
        string? Name, bool IsPrimary, string? Description, double Latitude, double Longitude, double Elevation,
        ImportTagLookup LocationQuality, DateTime? ReportedOn, double? PitDepthFeet,
        IReadOnlyList<ImportTagLookup> StatusTags, IReadOnlyList<ImportTagLookup> HydrologyTags,
        IReadOnlyList<ImportTagLookup> FieldIndicationTags, IReadOnlyList<ImportTagLookup> ReportedByTags);
}

internal static class PlannedEntranceSourceExtensions
{
    public static EntranceCsvModel SourceModel(this PlannedEntrance row) => new()
    {
        CountyCode = row.CountyDisplayId,
        CountyCaveNumber = row.CountyCaveNumber.ToString(CultureInfo.InvariantCulture),
        EntranceName = row.Name,
        DecimalLatitude = row.Latitude,
        DecimalLongitude = row.Longitude,
        EntranceElevationFt = row.Elevation,
        LocationQuality = row.LocationQualityName,
        EntranceDescription = row.Description,
        EntrancePitDepth = row.PitDepthFeet,
        EntranceStatuses = string.Join(",", row.EntranceStatuses),
        EntranceHydrology = string.Join(",", row.EntranceHydrology),
        FieldIndication = string.Join(",", row.FieldIndication),
        ReportedByNames = string.Join(",", row.ReportedByNames),
        IsPrimaryEntrance = row.IsPrimary,
        ReportedOnDate = row.ReportedOn?.ToString(CultureInfo.InvariantCulture)
    };
}
