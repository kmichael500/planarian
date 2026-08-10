using Planarian.Modules.Account.Import.Models;

namespace Planarian.Modules.Import.Planning;

public sealed record ImportTagCreationIntent(string Id, string Key, string Name);

public enum EntranceImportTagRole
{
    Status,
    Hydrology,
    FieldIndication,
    ReportedBy
}

public sealed record PlannedEntranceTag(
    string Id,
    string EntranceId,
    string TagTypeId,
    EntranceImportTagRole Role);

public sealed record EntranceImportCaveTarget(
    string CaveId,
    string CaveName,
    string CountyDisplayId,
    int CountyCaveNumber,
    uint Version,
    string? CurrentRevisionId,
    int ExistingEntranceCount,
    int ExistingPrimaryCount);

public sealed record PlannedEntrance(
    string Id,
    int CsvRowNumber,
    string CaveId,
    string CountyDisplayId,
    int CountyCaveNumber,
    string CaveName,
    string? Name,
    bool IsPrimary,
    string? Description,
    double Latitude,
    double Longitude,
    double Elevation,
    string LocationQualityTagId,
    string LocationQualityName,
    DateTime? ReportedOn,
    double? PitDepthFeet,
    IReadOnlyList<PlannedEntranceTag> Tags,
    IReadOnlyList<string> EntranceStatuses,
    IReadOnlyList<string> EntranceHydrology,
    IReadOnlyList<string> FieldIndication,
    IReadOnlyList<string> ReportedByNames);

public sealed class EntranceImportPlan
{
    public EntranceImportPlan(string accountId, bool syncExisting,
        IReadOnlyList<PlannedEntrance> entrances,
        IReadOnlyList<ImportTagCreationIntent> tagCreations,
        IReadOnlyDictionary<string, string> tagNamesById,
        IReadOnlyDictionary<string, EntranceImportCaveTarget> targets)
    {
        AccountId = accountId;
        SyncExisting = syncExisting;
        Entrances = entrances;
        TagCreations = tagCreations;
        TagNamesById = tagNamesById;
        Targets = targets;
    }

    public string AccountId { get; }
    public bool SyncExisting { get; }
    public IReadOnlyList<PlannedEntrance> Entrances { get; }
    public IReadOnlyList<ImportTagCreationIntent> TagCreations { get; }
    public IReadOnlyDictionary<string, string> TagNamesById { get; }
    public IReadOnlyDictionary<string, EntranceImportCaveTarget> Targets { get; }

    public List<EntranceDryRun> CreatePreview()
    {
        var importedCounts = Entrances.GroupBy(e => e.CaveId)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        return Entrances.Select(row =>
        {
            var target = Targets[row.CaveId];
            var existing = target.ExistingEntranceCount;
            var finalCount = SyncExisting ? importedCounts[row.CaveId] : existing + importedCounts[row.CaveId];
            return new EntranceDryRun
            {
                AssociatedCave = $"{row.CountyDisplayId}-{row.CountyCaveNumber} {row.CaveName}",
                EntranceCountChange = finalCount - existing,
                EntranceName = row.Name,
                IsPrimaryEntrance = row.IsPrimary,
                DecimalLatitude = row.Latitude,
                DecimalLongitude = row.Longitude,
                EntranceElevationFt = row.Elevation,
                LocationQuality = row.LocationQualityName,
                EntrancePitDepth = row.PitDepthFeet,
                EntranceStatuses = row.EntranceStatuses.ToList(),
                EntranceHydrology = row.EntranceHydrology.ToList(),
                FieldIndication = row.FieldIndication.ToList(),
                ReportedOnDate = row.ReportedOn,
                ReportedByNames = row.ReportedByNames.ToList(),
                EntranceDescription = row.Description
            };
        }).ToList();
    }
}
