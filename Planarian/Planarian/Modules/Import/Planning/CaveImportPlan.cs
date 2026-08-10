using Planarian.Modules.Account.Import.Models;

namespace Planarian.Modules.Import.Planning;

public enum CaveImportAction
{
    Insert,
    Update,
    NoChange,
    Delete
}

public enum CaveImportTagRole
{
    Geology,
    GeologicAge,
    MapStatus,
    PhysiographicProvince,
    Archeology,
    Biology,
    Other,
    Cartographer,
    ReportedBy
}

public sealed record AccountStateCreationIntent(string Id, string AccountId, string StateId);
public sealed record CountyCreationIntent(string Id, string AccountId, string StateId, string DisplayId, string Name);
public sealed record PlannedCaveTag(string Id, string CaveId, string TagTypeId, CaveImportTagRole Role);

public sealed record CaveImportExistingTarget(
    string CaveId,
    uint Version,
    string? CurrentRevisionId,
    string StateId,
    string CountyId,
    string CountyDisplayId,
    int CountyNumber,
    string CaveName);

public sealed record PlannedCave(
    string Id,
    int CsvRowNumber,
    CaveImportAction Action,
    string? ChangeSummary,
    string StateId,
    string StateAbbreviation,
    string CountyId,
    string CountyDisplayId,
    string CountyName,
    int CountyNumber,
    string Name,
    IReadOnlyList<string> AlternateNames,
    double? LengthFeet,
    double? DepthFeet,
    double? MaxPitDepthFeet,
    int? NumberOfPits,
    string? Narrative,
    DateTime? ReportedOn,
    bool IsArchived,
    IReadOnlyList<PlannedCaveTag> Tags,
    IReadOnlyList<string> Geology,
    IReadOnlyList<string> GeologicAges,
    IReadOnlyList<string> MapStatuses,
    IReadOnlyList<string> PhysiographicProvinces,
    IReadOnlyList<string> Archeology,
    IReadOnlyList<string> Biology,
    IReadOnlyList<string> OtherTags,
    IReadOnlyList<string> CartographerNames,
    IReadOnlyList<string> ReportedByNames);

public sealed record PlannedCaveDeletion(
    string CaveId,
    string StateAbbreviation,
    string CountyName,
    string CountyDisplayId,
    int CountyNumber,
    string CaveName);

public sealed class CaveImportPlan
{
    public CaveImportPlan(string accountId, bool syncExisting,
        IReadOnlyList<PlannedCave> caves,
        IReadOnlyList<PlannedCaveDeletion> deletions,
        IReadOnlyList<AccountStateCreationIntent> accountStateCreations,
        IReadOnlyList<CountyCreationIntent> countyCreations,
        IReadOnlyList<ImportTagCreationIntent> tagCreations,
        IReadOnlyDictionary<string, string> tagNamesById,
        IReadOnlyDictionary<string, CaveImportExistingTarget> existingTargets)
    {
        AccountId = accountId;
        SyncExisting = syncExisting;
        Caves = caves;
        Deletions = deletions;
        AccountStateCreations = accountStateCreations;
        CountyCreations = countyCreations;
        TagCreations = tagCreations;
        TagNamesById = tagNamesById;
        ExistingTargets = existingTargets;
    }

    public string AccountId { get; }
    public bool SyncExisting { get; }
    public IReadOnlyList<PlannedCave> Caves { get; }
    public IReadOnlyList<PlannedCaveDeletion> Deletions { get; }
    public IReadOnlyList<AccountStateCreationIntent> AccountStateCreations { get; }
    public IReadOnlyList<CountyCreationIntent> CountyCreations { get; }
    public IReadOnlyList<ImportTagCreationIntent> TagCreations { get; }
    public IReadOnlyDictionary<string, string> TagNamesById { get; }
    public IReadOnlyDictionary<string, CaveImportExistingTarget> ExistingTargets { get; }

    public List<CaveDryRunRecord> CreatePreview(bool omitNoChange)
    {
        var records = Caves
            .Where(cave => !omitNoChange || cave.Action != CaveImportAction.NoChange)
            .Select(cave => new CaveDryRunRecord
            {
                CountyCode = cave.CountyDisplayId,
                CountyCaveNumber = cave.CountyNumber,
                CaveName = cave.Name,
                ChangesSummary = cave.ChangeSummary,
                Action = cave.Action switch
                {
                    CaveImportAction.Insert => "insert",
                    CaveImportAction.Update => "update",
                    CaveImportAction.NoChange => "no change",
                    _ => throw new ArgumentOutOfRangeException()
                },
                State = cave.StateAbbreviation,
                CountyName = cave.CountyName,
                AlternateNames = cave.AlternateNames.ToList(),
                CaveLengthFeet = cave.LengthFeet,
                CaveDepthFeet = cave.DepthFeet,
                MaxPitDepthFeet = cave.MaxPitDepthFeet,
                NumberOfPits = cave.NumberOfPits,
                ReportedOnDate = cave.ReportedOn,
                IsArchived = cave.IsArchived,
                Geology = cave.Geology.ToList(),
                ReportedByNames = cave.ReportedByNames.ToList(),
                Biology = cave.Biology.ToList(),
                Archeology = cave.Archeology.ToList(),
                CartographerNames = cave.CartographerNames.ToList(),
                GeologicAges = cave.GeologicAges.ToList(),
                PhysiographicProvinces = cave.PhysiographicProvinces.ToList(),
                OtherTags = cave.OtherTags.ToList(),
                MapStatuses = cave.MapStatuses.ToList(),
                Narrative = cave.Narrative
            }).ToList();

        if (SyncExisting)
            records.AddRange(Deletions.Select(cave => new CaveDryRunRecord
            {
                Action = "delete",
                ChangesSummary = null,
                State = cave.StateAbbreviation,
                CountyName = cave.CountyName,
                CountyCode = cave.CountyDisplayId,
                CountyCaveNumber = cave.CountyNumber,
                CaveName = cave.CaveName
            }));
        return records;
    }
}
