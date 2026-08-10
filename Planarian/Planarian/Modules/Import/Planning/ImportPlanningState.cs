namespace Planarian.Modules.Import.Planning;

public sealed record CaveImportStateLookup(string Id, string Name, string Abbreviation);
public sealed record CaveImportCountyLookup(string Id, string StateId, string DisplayId, string Name);
public sealed record CaveImportUsedCountyNumber(string CountyId, int CountyNumber);
public sealed record CaveImportExistingCave(
    string Id, uint Version, string? CurrentRevisionId,
    string StateId, string StateAbbreviation, string CountyId, string CountyName, string CountyDisplayId,
    int CountyNumber, string Name, string? AlternateNames, double? LengthFeet, double? DepthFeet,
    double? MaxPitDepthFeet, int? NumberOfPits, string? Narrative, DateTime? ReportedOn, bool IsArchived,
    IReadOnlyList<string> GeologyTagIds, IReadOnlyList<string> GeologicAgeTagIds,
    IReadOnlyList<string> MapStatusTagIds, IReadOnlyList<string> PhysiographicProvinceTagIds,
    IReadOnlyList<string> ArcheologyTagIds, IReadOnlyList<string> BiologyTagIds,
    IReadOnlyList<string> OtherTagIds, IReadOnlyList<string> CartographerNameTagIds,
    IReadOnlyList<string> ReportedByNameTagIds);

public sealed record CaveImportPlanningState(
    string AccountId,
    IReadOnlyList<CaveImportStateLookup> States,
    IReadOnlySet<string> AccountStateIds,
    IReadOnlyList<CaveImportCountyLookup> Counties,
    IReadOnlyList<ImportTagLookup> EligibleTags,
    IReadOnlyList<CaveImportExistingCave> ExistingCaves,
    IReadOnlySet<CaveImportUsedCountyNumber> UsedCountyNumbers);

public sealed record EntranceImportCaveLookup(
    string Id, string Name, string CountyDisplayId, int CountyNumber, uint Version, string? CurrentRevisionId);

public sealed record EntranceImportPlanningState(
    string AccountId,
    IReadOnlyList<ImportTagLookup> EligibleTags,
    IReadOnlyList<EntranceImportCaveLookup> Caves,
    IReadOnlyDictionary<string, int> ExistingEntranceCounts,
    IReadOnlyDictionary<string, int> ExistingPrimaryCounts);
