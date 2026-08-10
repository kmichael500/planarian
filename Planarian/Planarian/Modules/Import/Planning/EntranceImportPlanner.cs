using System.Globalization;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.DateTime;
using Planarian.Library.Extensions.String;
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
    public EntranceImportPlan Plan(IReadOnlyList<EntranceCsvModel> records, EntranceImportPlanningState state,
        bool syncExisting,
        CancellationToken cancellationToken = default)
    {
        var failedRecords = new List<FailedCaveCsvRecord<EntranceCsvModel>>();
        var tagSets = ResolveTags(records, state, cancellationToken);

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

        var candidateCaves = state.Caves;
        var caveByKey = candidateCaves.ToDictionary(c => (c.CountyDisplayId, c.CountyNumber));

        foreach (var row in parsed)
        {
            if (caveByKey.ContainsKey((row.CountyDisplayId, row.CountyCaveNumber))) continue;
            failedRecords.Add(new(row.Source, row.SourceIndex + 2,
                $"Entrance could not be associated with the cave {row.CountyDisplayId}-{row.CountyCaveNumber}"));
        }
        ThrowIfInvalid(failedRecords);

        var existingCounts = state.ExistingEntranceCounts;
        var existingPrimaryCounts = state.ExistingPrimaryCounts;

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

        var plannedCaveIds = planned.Select(row => row.CaveId).ToHashSet(StringComparer.Ordinal);
        var targets = candidateCaves
            .Where(c => plannedCaveIds.Contains(c.Id))
            .ToDictionary(c => c.Id, c => new EntranceImportCaveTarget(
                c.Id, c.Name, c.CountyDisplayId, c.CountyNumber, c.Version, c.CurrentRevisionId,
                existingCounts.GetValueOrDefault(c.Id), existingPrimaryCounts.GetValueOrDefault(c.Id)),
                StringComparer.Ordinal);
        var tagNamesById = tagSets.All
            .GroupBy(t => t.Id)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.Ordinal);

        return new EntranceImportPlan(state.AccountId, syncExisting, planned,
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

    private static ImportTagResolutionSet ResolveTags(IReadOnlyList<EntranceCsvModel> records,
        EntranceImportPlanningState state, CancellationToken cancellationToken)
    {
        var requested = new List<(string Key, IEnumerable<string?> Names)>
        {
            (TagTypeKeyConstant.LocationQuality, records.Select(r => r.LocationQuality)),
            (TagTypeKeyConstant.EntranceStatus, records.SelectMany(r => r.EntranceStatuses.SplitAndTrim())),
            (TagTypeKeyConstant.EntranceHydrology, records.SelectMany(r => r.EntranceHydrology.SplitAndTrim())),
            (TagTypeKeyConstant.FieldIndication, records.SelectMany(r => r.FieldIndication.SplitAndTrim())),
            (TagTypeKeyConstant.People, records.SelectMany(r => r.ReportedByNames.SplitAndTrim()))
        };
        return ImportTagResolver.Resolve(state.AccountId, state.EligibleTags, requested, cancellationToken);
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
