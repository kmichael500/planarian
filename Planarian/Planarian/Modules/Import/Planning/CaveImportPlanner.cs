using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.DateTime;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database;
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
    private readonly PlanarianDbContext _db;
    private readonly AccountExecutionScope _scope;

    public CaveImportPlanner(PlanarianDbContext db, RequestUser requestUser)
    {
        _db = db;
        _scope = AccountExecutionScope.Require(requestUser);
    }

    public async Task<CaveImportPlan> PlanAsync(Stream stream, bool syncExisting,
        CancellationToken cancellationToken = default)
    {
        var failedRecords = new List<FailedCaveCsvRecord<CaveCsvModel>>();
        var records = await ParseAsync(stream, failedRecords, cancellationToken);

        var stateInputs = records.Select(r => r.State.Trim()).Distinct().ToList();
        var stateCandidates = await _db.States
            .Where(s => stateInputs.Contains(s.Name) || stateInputs.Contains(s.Abbreviation))
            .AsNoTracking()
            .Select(s => new StateLookup(s.Id, s.Name, s.Abbreviation))
            .ToListAsync(cancellationToken);
        var statesByInput = new Dictionary<string, StateLookup>(StringComparer.Ordinal);
        foreach (var input in stateInputs)
        {
            var state = stateCandidates.FirstOrDefault(s => s.Name == input || s.Abbreviation == input)
                        ?? throw ApiExceptionDictionary.NotFound("State");
            statesByInput[input] = state;
        }

        var existingAccountStateIds = await _db.AccountStates
            .Where(a => a.AccountId == _scope.AccountId)
            .AsNoTracking()
            .Select(a => a.StateId)
            .ToListAsync(cancellationToken);
        var accountStateCreations = statesByInput.Values.DistinctBy(s => s.Id)
            .Where(s => !existingAccountStateIds.Contains(s.Id))
            .Select(s => new AccountStateCreationIntent(IdGenerator.Generate(), _scope.AccountId, s.Id))
            .ToList();

        var tagSet = await ResolveTagsAsync(records, cancellationToken);
        var tagNamesById = tagSet.All
            .GroupBy(tag => tag.Id)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.Ordinal);

        var existingCounties = await _db.Counties
            .Where(c => c.AccountId == _scope.AccountId)
            .AsNoTracking()
            .Select(c => new CountyLookup(c.Id, c.StateId, c.DisplayId, c.Name, false))
            .ToListAsync(cancellationToken);
        var countyCandidates = new List<CountyLookup>();
        var seenCountyDisplayIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            var displayId = record.CountyCode.Trim();
            if (!seenCountyDisplayIds.Add(displayId)) continue;
            var state = statesByInput[record.State.Trim()];
            countyCandidates.Add(new CountyLookup(IdGenerator.Generate(), state.Id, displayId,
                record.CountyName.Trim(), true));
        }
        var countyCreations = countyCandidates
            .Where(candidate => existingCounties.All(existing => existing.DisplayId != candidate.DisplayId))
            .Select(c => new CountyCreationIntent(c.Id, _scope.AccountId, c.StateId, c.DisplayId, c.Name))
            .ToList();
        var allCounties = existingCounties.Concat(countyCreations.Select(c =>
                new CountyLookup(c.Id, c.StateId, c.DisplayId, c.Name, true)))
            .ToList();

        var existingCaves = syncExisting
            ? await LoadExistingCavesAsync(cancellationToken)
            : [];
        var existingByKey = existingCaves.ToDictionary(
            c => $"{c.StateId}:{c.CountyId}:{c.CountyNumber}", StringComparer.Ordinal);
        var deleteIds = existingCaves.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var seenCsvKeys = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        var usedCountyNumbers = (await _db.Caves.IgnoreQueryFilters()
                .Where(c => c.AccountId == _scope.AccountId)
                .AsNoTracking()
                .Select(c => new UsedCountyNumber(c.CountyId, c.CountyNumber))
                .ToListAsync(cancellationToken))
            .ToHashSet();

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

                ExistingCave? existing = null;
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
                    AccountId = _scope.AccountId,
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

        return new CaveImportPlan(_scope.AccountId, syncExisting, planned, deletions,
            accountStateCreations, countyCreations, tagSet.Creations,
            tagNamesById, targets);
    }

    private async Task<List<ExistingCave>> LoadExistingCavesAsync(CancellationToken cancellationToken)
    {
        return await _db.Caves.IgnoreQueryFilters()
            .Where(c => c.AccountId == _scope.AccountId)
            .AsNoTracking()
            .Select(c => new ExistingCave(
                c.Id, c.Version, c.CurrentRevisionId,
                c.StateId, c.State.Abbreviation, c.CountyId, c.County.Name, c.County.DisplayId,
                c.CountyNumber, c.Name, c.AlternateNames, c.LengthFeet, c.DepthFeet,
                c.MaxPitDepthFeet, c.NumberOfPits, c.Narrative, c.ReportedOn, c.IsArchived,
                c.GeologyTags.Select(t => t.TagTypeId).ToList(),
                c.GeologicAgeTags.Select(t => t.TagTypeId).ToList(),
                c.MapStatusTags.Select(t => t.TagTypeId).ToList(),
                c.PhysiographicProvinceTags.Select(t => t.TagTypeId).ToList(),
                c.ArcheologyTags.Select(t => t.TagTypeId).ToList(),
                c.BiologyTags.Select(t => t.TagTypeId).ToList(),
                c.CaveOtherTags.Select(t => t.TagTypeId).ToList(),
                c.CartographerNameTags.Select(t => t.TagTypeId).ToList(),
                c.CaveReportedByNameTags.Select(t => t.TagTypeId).ToList()))
            .ToListAsync(cancellationToken);
    }

    private Task<ImportTagResolutionSet> ResolveTagsAsync(IReadOnlyList<CaveCsvModel> records,
        CancellationToken cancellationToken)
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
            (TagTypeKeyConstant.People, records.SelectMany(r => r.CartographerNames.SplitAndTrim())),
            (TagTypeKeyConstant.People, records.SelectMany(r => r.ReportedByNames.SplitAndTrim()))
        };
        return ImportTagResolver.ResolveAsync(_db, _scope.AccountId, requests, cancellationToken);
    }

    private static List<ImportTagLookup> ResolveMany(ImportTagResolutionSet set, string key, string? raw) =>
        ImportTagResolver.ResolveMany(set, key, raw.SplitAndTrim());

    private static bool ValidateCave(Cave cave, HashSet<UsedCountyNumber> usedCountyNumbers,
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
            var key = new UsedCountyNumber(cave.CountyId, cave.CountyNumber);
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

    private static string? BuildChangeSummary(ExistingCave existing, Cave imported,
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

    private static async Task<List<CaveCsvModel>> ParseAsync(Stream stream,
        List<FailedCaveCsvRecord<CaveCsvModel>> failures, CancellationToken cancellationToken)
    {
        var records = new List<CaveCsvModel>();
        using var reader = new StreamReader(stream);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null };
        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<CaveCsvModelMap>();
        if (!await csv.ReadAsync()) return records;
        csv.ReadHeader();
        var rowNumber = 1;
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            var record = new CaveCsvModel();
            var errors = new List<string>();
            TryGet(csv, nameof(record.CaveName), true, errors, out string? caveName); if (!string.IsNullOrWhiteSpace(caveName)) record.CaveName = caveName;
            TryGet(csv, nameof(record.AlternateNames), false, errors, out string? alternate); record.AlternateNames = alternate;
            TryGet(csv, nameof(record.State), true, errors, out string? state); if (!string.IsNullOrWhiteSpace(state)) record.State = state;
            TryGet(csv, nameof(record.CountyCode), true, errors, out string? countyCode); if (!string.IsNullOrWhiteSpace(countyCode)) record.CountyCode = countyCode;
            TryGet(csv, nameof(record.CountyName), true, errors, out string? countyName); if (!string.IsNullOrWhiteSpace(countyName)) record.CountyName = countyName;
            TryGet(csv, nameof(record.CountyCaveNumber), true, errors, out int countyNumber); record.CountyCaveNumber = countyNumber;
            TryGet(csv, nameof(record.MapStatuses), false, errors, out string? map); record.MapStatuses = map;
            TryGet(csv, nameof(record.CartographerNames), false, errors, out string? cartographers); record.CartographerNames = cartographers;
            TryGet(csv, nameof(record.CaveLengthFt), false, errors, out double? length); record.CaveLengthFt = length;
            TryGet(csv, nameof(record.CaveDepthFt), false, errors, out double? depth); record.CaveDepthFt = depth;
            TryGet(csv, nameof(record.MaxPitDepthFt), false, errors, out double? pit); record.MaxPitDepthFt = pit;
            TryGet(csv, nameof(record.NumberOfPits), false, errors, out int? pits); record.NumberOfPits = pits;
            TryGet(csv, nameof(record.Narrative), false, errors, out string? narrative); record.Narrative = narrative;
            TryGet(csv, nameof(record.Geology), false, errors, out string? geology); record.Geology = geology;
            TryGet(csv, nameof(record.GeologicAges), false, errors, out string? ages); record.GeologicAges = ages;
            TryGet(csv, nameof(record.PhysiographicProvinces), false, errors, out string? provinces); record.PhysiographicProvinces = provinces;
            TryGet(csv, nameof(record.Archeology), false, errors, out string? archaeology); record.Archeology = archaeology;
            TryGet(csv, nameof(record.Biology), false, errors, out string? biology); record.Biology = biology;
            TryGet(csv, nameof(record.IsArchived), false, errors, out bool archived); record.IsArchived = archived;
            TryGet(csv, nameof(record.ReportedOnDate), false, errors, out string? reportedOn); record.ReportedOnDate = reportedOn;
            TryGet(csv, nameof(record.ReportedByNames), false, errors, out string? reportedBy); record.ReportedByNames = reportedBy;
            TryGet(csv, nameof(record.OtherTags), false, errors, out string? other); record.OtherTags = other;
            if (errors.Count == 0) records.Add(record);
            else foreach (var error in errors) failures.Add(new(record, rowNumber, error));
        }
        ThrowIfInvalid(failures);
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

    private static void ThrowIfInvalid(List<FailedCaveCsvRecord<CaveCsvModel>> failures)
    {
        if (failures.Count == 0) return;
        throw ApiExceptionDictionary.InvalidImport(failures.OrderBy(f => f.RowNumber).ToList(), ImportType.Cave);
    }

    private sealed record StateLookup(string Id, string Name, string Abbreviation);
    private sealed record CountyLookup(string Id, string StateId, string DisplayId, string Name, bool IsCreation);
    private sealed record UsedCountyNumber(string CountyId, int CountyNumber);
    private sealed record ExistingCave(
        string Id, uint Version, string? CurrentRevisionId,
        string StateId, string StateAbbreviation, string CountyId, string CountyName, string CountyDisplayId,
        int CountyNumber, string Name, string? AlternateNames, double? LengthFeet, double? DepthFeet,
        double? MaxPitDepthFeet, int? NumberOfPits, string? Narrative, DateTime? ReportedOn, bool IsArchived,
        IReadOnlyList<string> GeologyTagIds, IReadOnlyList<string> GeologicAgeTagIds,
        IReadOnlyList<string> MapStatusTagIds, IReadOnlyList<string> PhysiographicProvinceTagIds,
        IReadOnlyList<string> ArcheologyTagIds, IReadOnlyList<string> BiologyTagIds,
        IReadOnlyList<string> OtherTagIds, IReadOnlyList<string> CartographerNameTagIds,
        IReadOnlyList<string> ReportedByNameTagIds);
}
