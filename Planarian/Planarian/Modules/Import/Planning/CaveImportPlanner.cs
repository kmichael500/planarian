using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.DateTime;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Import.Models;

namespace Planarian.Modules.Import.Planning;

/// <summary>
/// Pure Cave import planner. It characterizes main's parsing/tag/sync behavior
/// while replacing write-and-rollback preview with read-only resolution and an
/// immutable plan.
/// </summary>
public sealed class CaveImportPlanner
{
    public CaveImportPlan Plan(IReadOnlyList<CaveCsvModel> records, CaveImportPlanningState planningState, bool syncExisting,
        CancellationToken cancellationToken = default)
    {
        var failedRecords = new List<FailedCaveCsvRecord<CaveCsvModel>>();
        for (var index = 0; index < records.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(records[index].CaveName))
                failedRecords.Add(new(records[index], index + 2, $"{nameof(CaveCsvModel.CaveName)} is required."));
        }
        ThrowIfInvalid(failedRecords);

        var stateInputs = records.Select(r => r.State.Trim()).Distinct().ToList();
        var statesByInput = new Dictionary<string, CaveImportStateLookup>(StringComparer.Ordinal);
        foreach (var input in stateInputs)
        {
            var resolvedState = planningState.States.FirstOrDefault(s => s.Name == input || s.Abbreviation == input)
                        ?? throw ApiExceptionDictionary.NotFound("State");
            statesByInput[input] = resolvedState;
        }
        var accountStateCreations = statesByInput.Values.DistinctBy(s => s.Id)
            .Where(s => !planningState.AccountStateIds.Contains(s.Id))
            .Select(s => new AccountStateCreationIntent(IdGenerator.Generate(), planningState.AccountId, s.Id))
            .ToList();

        var tagSet = ResolveTags(records, planningState, cancellationToken);
        var tagNamesById = tagSet.All
            .GroupBy(tag => tag.Id)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.Ordinal);

        var existingCounties = planningState.Counties.ToList();
        var countyCandidates = new List<CaveImportCountyLookup>();
        var seenCountyDisplayIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            var displayId = record.CountyCode.Trim();
            if (!seenCountyDisplayIds.Add(displayId)) continue;
            var state = statesByInput[record.State.Trim()];
            countyCandidates.Add(new CaveImportCountyLookup(IdGenerator.Generate(), state.Id, displayId,
                record.CountyName.Trim()));
        }
        var countyCreations = countyCandidates
            .Where(candidate => existingCounties.All(existing => existing.DisplayId != candidate.DisplayId))
            .Select(c => new CountyCreationIntent(c.Id, planningState.AccountId, c.StateId, c.DisplayId, c.Name))
            .ToList();
        var allCounties = existingCounties.Concat(countyCreations.Select(c =>
                new CaveImportCountyLookup(c.Id, c.StateId, c.DisplayId, c.Name)))
            .ToList();

        var existingCaves = syncExisting
            ? planningState.ExistingCaves
            : [];
        var existingByKey = existingCaves.ToDictionary(
            c => $"{c.StateId}:{c.CountyId}:{c.CountyNumber}", StringComparer.Ordinal);
        var deleteIds = existingCaves.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var seenCsvKeys = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        var usedCountyNumbers = planningState.UsedCountyNumbers.ToHashSet();

        var planned = new List<PlannedCave>(records.Count);
        for (var index = 0; index < records.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = records[index];
            var rowNumber = index + 2;
            try
            {
                var state = statesByInput[record.State.Trim()];
                var county = allCounties.FirstOrDefault(c =>
                    c.DisplayId.Equals(record.CountyCode.Trim(), StringComparison.InvariantCultureIgnoreCase) &&
                    c.StateId == state.Id);
                if (county is null)
                {
                    var conflicting = allCounties.FirstOrDefault(c => c.DisplayId == record.CountyCode.Trim());
                    failedRecords.Add(new(record, rowNumber, conflicting is null
                        ? $"{nameof(record.CountyCode)} not found: '{record.CountyCode}'"
                        : $"{nameof(record.CountyCode)} value '{record.CountyCode}' is already being used for county '{conflicting.Name}'."));
                    continue;
                }
                if (record.IsArchived is null)
                {
                    failedRecords.Add(new(record, rowNumber, $"{nameof(record.IsArchived)} is missing."));
                    continue;
                }

                CaveImportExistingCave? existing = null;
                if (syncExisting)
                {
                    var key = $"{state.Id}:{county.Id}:{record.CountyCaveNumber}";
                    if (!seenCsvKeys.Add(key))
                    {
                        failedRecords.Add(new(record, rowNumber,
                            $"Duplicate cave key found for {record.CountyCode}-{record.CountyCaveNumber}."));
                        continue;
                    }
                    existingByKey.TryGetValue(key, out existing);
                }

                var isValidReportedOn = DateTime.TryParse(record.ReportedOnDate, out var reportedOnDate);
                reportedOnDate = reportedOnDate.ToUtcKind();
                var caveId = existing?.Id ?? IdGenerator.Generate();
                var alternateNames = record.AlternateNames.SplitAndTrim().ToList();
                var transient = new Cave
                {
                    Id = caveId,
                    Name = record.CaveName.Trim(),
                    AccountId = planningState.AccountId,
                    LengthFeet = record.CaveLengthFt,
                    DepthFeet = record.CaveDepthFt,
                    MaxPitDepthFeet = record.MaxPitDepthFt,
                    NumberOfPits = record.NumberOfPits,
                    Narrative = record.Narrative?.Trim(),
                    CountyId = county.Id,
                    CountyNumber = record.CountyCaveNumber,
                    StateId = state.Id,
                    ReportedOn = isValidReportedOn ? reportedOnDate : null,
                    IsArchived = record.IsArchived.Value
                };
                transient.SetAlternateNamesList(alternateNames);

                var geology = ResolveMany(tagSet, TagTypeKeyConstant.Geology, record.Geology);
                var geologicAges = ResolveMany(tagSet, TagTypeKeyConstant.GeologicAge, record.GeologicAges);
                var mapStatuses = ResolveMany(tagSet, TagTypeKeyConstant.MapStatus, record.MapStatuses);
                var physiographic = ResolveMany(tagSet, TagTypeKeyConstant.PhysiographicProvince,
                    record.PhysiographicProvinces);
                var archeology = ResolveMany(tagSet, TagTypeKeyConstant.Archeology, record.Archeology);
                var biology = ResolveMany(tagSet, TagTypeKeyConstant.Biology, record.Biology);
                var other = ResolveMany(tagSet, TagTypeKeyConstant.CaveOther, record.OtherTags);
                var cartographers = ResolveMany(tagSet, TagTypeKeyConstant.People, record.CartographerNames);
                var reportedBy = ResolveMany(tagSet, TagTypeKeyConstant.People, record.ReportedByNames)
                    .DistinctBy(t => t.Id).ToList();

                if (!ValidateCave(transient, usedCountyNumbers, record, rowNumber, failedRecords,
                        skipCountyNumberConflictCheck: syncExisting))
                    continue;

                var tagIds = new Dictionary<CaveImportTagRole, IReadOnlyList<ImportTagLookup>>
                {
                    [CaveImportTagRole.Geology] = geology,
                    [CaveImportTagRole.GeologicAge] = geologicAges,
                    [CaveImportTagRole.MapStatus] = mapStatuses,
                    [CaveImportTagRole.PhysiographicProvince] = physiographic,
                    [CaveImportTagRole.Archeology] = archeology,
                    [CaveImportTagRole.Biology] = biology,
                    [CaveImportTagRole.Other] = other,
                    [CaveImportTagRole.Cartographer] = cartographers,
                    [CaveImportTagRole.ReportedBy] = reportedBy
                };

                var action = CaveImportAction.Insert;
                string? summary = null;
                if (existing is not null)
                {
                    deleteIds.Remove(existing.Id);
                    summary = BuildChangeSummary(existing, transient, tagIds, tagNamesById);
                    action = string.IsNullOrWhiteSpace(summary) ? CaveImportAction.NoChange : CaveImportAction.Update;
                }

                var tags = new List<PlannedCaveTag>();
                foreach (var (role, values) in tagIds)
                    foreach (var tag in values)
                        tags.Add(new PlannedCaveTag(IdGenerator.Generate(), caveId, tag.Id, role));

                planned.Add(new PlannedCave(
                    caveId, rowNumber, action, summary,
                    state.Id, state.Abbreviation, county.Id, county.DisplayId, county.Name,
                    record.CountyCaveNumber, transient.Name, alternateNames, transient.LengthFeet,
                    transient.DepthFeet, transient.MaxPitDepthFeet, transient.NumberOfPits,
                    transient.Narrative, transient.ReportedOn, transient.IsArchived, tags,
                    geology.Select(t => t.Name).ToList(),
                    geologicAges.Select(t => t.Name).ToList(),
                    mapStatuses.Select(t => t.Name).ToList(),
                    physiographic.Select(t => t.Name).ToList(),
                    archeology.Select(t => t.Name).ToList(),
                    biology.Select(t => t.Name).ToList(),
                    other.Select(t => t.Name).ToList(),
                    cartographers.Select(t => t.Name).ToList(),
                    reportedBy.Select(t => t.Name).ToList()));
            }
            catch (Exception exception)
            {
                failedRecords.Add(new(record, rowNumber, exception.Message));
            }
        }

        ThrowIfInvalid(failedRecords);

        var deletions = syncExisting
            ? existingCaves.Where(c => deleteIds.Contains(c.Id)).Select(c => new PlannedCaveDeletion(
                c.Id, c.StateAbbreviation, c.CountyName, c.CountyDisplayId, c.CountyNumber, c.Name)).ToList()
            : [];
        var targets = existingCaves.ToDictionary(c => c.Id, c => new CaveImportExistingTarget(
            c.Id, c.Version, c.CurrentRevisionId, c.StateId, c.CountyId, c.CountyDisplayId, c.CountyNumber, c.Name),
            StringComparer.Ordinal);

        return new CaveImportPlan(planningState.AccountId, syncExisting, planned, deletions,
            accountStateCreations, countyCreations, tagSet.Creations,
            tagNamesById, targets);
    }

    private static ImportTagResolutionSet ResolveTags(IReadOnlyList<CaveCsvModel> records,
        CaveImportPlanningState state, CancellationToken cancellationToken)
    {
        var requests = new List<(string Key, IEnumerable<string?> Names)>
        {
            (TagTypeKeyConstant.Geology, records.SelectMany(r => r.Geology.SplitAndTrim())),
            (TagTypeKeyConstant.GeologicAge, records.SelectMany(r => r.GeologicAges.SplitAndTrim())),
            (TagTypeKeyConstant.MapStatus, records.SelectMany(r => r.MapStatuses.SplitAndTrim())),
            (TagTypeKeyConstant.PhysiographicProvince, records.SelectMany(r => r.PhysiographicProvinces.SplitAndTrim())),
            (TagTypeKeyConstant.Archeology, records.SelectMany(r => r.Archeology.SplitAndTrim())),
            (TagTypeKeyConstant.Biology, records.SelectMany(r => r.Biology.SplitAndTrim())),
            (TagTypeKeyConstant.CaveOther, records.SelectMany(r => r.OtherTags.SplitAndTrim())),
            (TagTypeKeyConstant.People, records.SelectMany(r =>
                r.CartographerNames.SplitAndTrim().Concat(r.ReportedByNames.SplitAndTrim())))
        };
        return ImportTagResolver.Resolve(state.AccountId, state.EligibleTags, requests, cancellationToken);
    }

    private static List<ImportTagLookup> ResolveMany(ImportTagResolutionSet set, string key, string? raw) =>
        ImportTagResolver.ResolveMany(set, key, raw.SplitAndTrim());

    private static bool ValidateCave(Cave cave, HashSet<CaveImportUsedCountyNumber> usedCountyNumbers,
        CaveCsvModel currentRecord, int rowNumber, List<FailedCaveCsvRecord<CaveCsvModel>> failures,
        bool skipCountyNumberConflictCheck)
    {
        var valid = true;
        foreach (var property in typeof(Cave).GetProperties())
        {
            if (property.Name == nameof(Cave.Id)) continue;
            var max = property.GetCustomAttributes(typeof(MaxLengthAttribute), false)
                .OfType<MaxLengthAttribute>().FirstOrDefault();
            if (max is null || property.GetValue(cave) is not string value || value.Length <= max.Length) continue;
            failures.Add(new(currentRecord, rowNumber,
                $"{property.Name} exceeds the maximum allowed length of {max.Length}"));
            valid = false;
        }
        if (!skipCountyNumberConflictCheck)
        {
            var key = new CaveImportUsedCountyNumber(cave.CountyId, cave.CountyNumber);
            if (usedCountyNumbers.Contains(key))
            {
                failures.Add(new(currentRecord, rowNumber, "County number is already used"));
                valid = false;
            }
            else usedCountyNumbers.Add(key);
        }
        if (cave.NumberOfPits is < 0)
        {
            failures.Add(new(currentRecord, rowNumber, "Number of pits must be greater than or equal to 1!"));
            valid = false;
        }
        if (cave.LengthFeet is < 0)
        {
            failures.Add(new(currentRecord, rowNumber, "Length must be greater than or equal to 0!"));
            valid = false;
        }
        if (cave.DepthFeet is < 0)
        {
            failures.Add(new(currentRecord, rowNumber, "Depth must be greater than or equal to 0!"));
            valid = false;
        }
        if (cave.MaxPitDepthFeet is < 0)
        {
            failures.Add(new(currentRecord, rowNumber, "Pit depth must be greater than or equal to 0!"));
            valid = false;
        }
        return valid;
    }

    private static string? BuildChangeSummary(CaveImportExistingCave existing, Cave imported,
        IReadOnlyDictionary<CaveImportTagRole, IReadOnlyList<ImportTagLookup>> importedTags,
        IReadOnlyDictionary<string, string> names)
    {
        var changes = new List<string>();
        AddValueChange(changes, "Name", FormatText(existing.Name), FormatText(imported.Name));
        AddValueChange(changes, "Alternate names", FormatText(existing.AlternateNames), FormatText(imported.AlternateNames));
        AddValueChange(changes, "Length (ft)", FormatNumber(existing.LengthFeet), FormatNumber(imported.LengthFeet));
        AddValueChange(changes, "Depth (ft)", FormatNumber(existing.DepthFeet), FormatNumber(imported.DepthFeet));
        AddValueChange(changes, "Max pit depth (ft)", FormatNumber(existing.MaxPitDepthFeet), FormatNumber(imported.MaxPitDepthFeet));
        AddValueChange(changes, "Number of pits", FormatInt(existing.NumberOfPits), FormatInt(imported.NumberOfPits));
        AddNarrativeChange(changes, existing.Narrative, imported.Narrative);
        AddValueChange(changes, "Reported on", FormatDate(existing.ReportedOn), FormatDate(imported.ReportedOn));
        AddValueChange(changes, "Archived", FormatBool(existing.IsArchived), FormatBool(imported.IsArchived));
        AddSetChange(changes, "Geology", existing.GeologyTagIds, importedTags[CaveImportTagRole.Geology].Select(t => t.Id), names);
        AddSetChange(changes, "Geologic ages", existing.GeologicAgeTagIds, importedTags[CaveImportTagRole.GeologicAge].Select(t => t.Id), names);
        AddSetChange(changes, "Map statuses", existing.MapStatusTagIds, importedTags[CaveImportTagRole.MapStatus].Select(t => t.Id), names);
        AddSetChange(changes, "Physiographic provinces", existing.PhysiographicProvinceTagIds, importedTags[CaveImportTagRole.PhysiographicProvince].Select(t => t.Id), names);
        AddSetChange(changes, "Archeology", existing.ArcheologyTagIds, importedTags[CaveImportTagRole.Archeology].Select(t => t.Id), names);
        AddSetChange(changes, "Biology", existing.BiologyTagIds, importedTags[CaveImportTagRole.Biology].Select(t => t.Id), names);
        AddSetChange(changes, "Other tags", existing.OtherTagIds, importedTags[CaveImportTagRole.Other].Select(t => t.Id), names);
        AddSetChange(changes, "Cartographer names", existing.CartographerNameTagIds, importedTags[CaveImportTagRole.Cartographer].Select(t => t.Id), names);
        AddSetChange(changes, "Reported by names", existing.ReportedByNameTagIds, importedTags[CaveImportTagRole.ReportedBy].Select(t => t.Id), names);
        return changes.Count == 0 ? null : string.Join(" | ", changes);
    }

    private static void AddValueChange(List<string> changes, string label, string existing, string imported)
    {
        if (existing != imported) changes.Add($"{label} changed from {existing} to {imported}");
    }

    private static void AddNarrativeChange(List<string> changes, string? existing, string? imported)
    {
        var oldValue = NormalizeOptionalText(existing);
        var newValue = NormalizeOptionalText(imported);
        if (oldValue == newValue) return;
        if (oldValue is null) { changes.Add($"Narrative added ({newValue!.Length} chars)"); return; }
        if (newValue is null) { changes.Add("Narrative removed"); return; }
        changes.Add(oldValue.Length == newValue.Length
            ? $"Narrative updated ({newValue.Length} chars)"
            : $"Narrative updated ({oldValue.Length} -> {newValue.Length} chars)");
    }

    private static void AddSetChange(List<string> changes, string label, IEnumerable<string> existingIds,
        IEnumerable<string> importedIds, IReadOnlyDictionary<string, string> names)
    {
        var existing = existingIds.Select(id => names.GetValueOrDefault(id, id)).Select(NormalizeOptionalText)
            .Where(v => v != null).Distinct().OrderBy(v => v).ToList();
        var imported = importedIds.Select(id => names.GetValueOrDefault(id, id)).Select(NormalizeOptionalText)
            .Where(v => v != null).Distinct().OrderBy(v => v).ToList();
        var added = imported.Except(existing).ToList();
        var removed = existing.Except(imported).ToList();
        if (added.Count > 0) changes.Add($"Added {label.ToLowerInvariant()}: {string.Join(", ", added)}");
        if (removed.Count > 0) changes.Add($"Removed {label.ToLowerInvariant()}: {string.Join(", ", removed)}");
    }

    private static string FormatText(string? value) => string.IsNullOrWhiteSpace(value) ? "blank" : $"\"{value}\"";
    private static string? NormalizeOptionalText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string FormatNumber(double? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "blank";
    private static string FormatInt(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "blank";
    private static string FormatDate(DateTime? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "blank";
    private static string FormatBool(bool value) => value ? "Yes" : "No";

    private static void ThrowIfInvalid(List<FailedCaveCsvRecord<CaveCsvModel>> failures)
    {
        if (failures.Count == 0) return;
        throw ApiExceptionDictionary.InvalidImport(failures.OrderBy(f => f.RowNumber).ToList(), ImportType.Cave);
    }

}
