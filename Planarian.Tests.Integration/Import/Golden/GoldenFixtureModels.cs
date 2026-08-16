using Planarian.Modules.Import.Planning;

namespace Planarian.Tests.Integration.Import.Golden;

internal sealed class GoldenFixture
{
    public string BaselineCommit { get; init; } = string.Empty;
    public List<ApprovedSemanticDifference> ApprovedSemanticDifferences { get; init; } = [];
    public List<GoldenCase> Cases { get; init; } = [];
}

internal sealed record ApprovedSemanticDifference(string Id, string BaselineCommit, List<string> AffectedAreas,
    string HistoricalBehavior, string TargetBehavior, string Rationale);

internal sealed class GoldenCase
{
    public string Area { get; init; } = string.Empty;
    public string Behavior { get; init; } = string.Empty;
    public string Setup { get; init; } = "default";
    public string InputCsv { get; init; } = string.Empty;
    public bool Sync { get; init; }
    public GoldenValidation Validation { get; init; } = new();
    public GoldenPreview? BaselinePreview { get; init; }
    public GoldenCommittedState? BaselineCommitted { get; init; }
    public GoldenTargetOverride? TargetOverride { get; init; }
    public string CoverageTest { get; init; } = string.Empty;
    public GoldenPreview TargetPreview => TargetOverride?.Preview ?? BaselinePreview!;
    public GoldenCommittedState TargetCommitted
    {
        get
        {
            var preview = TargetPreview;
            var committed = TargetOverride?.Committed ?? BaselineCommitted!;
            return committed.WithExpectedTagTypes(preview.TagCreations);
        }
    }
}

internal sealed record GoldenTargetOverride(string ApprovedDifferenceId, GoldenPreview Preview,
    GoldenCommittedState Committed);

internal sealed record GoldenValidation(string Outcome = "", int StatusCode = 0, string? ErrorCode = null,
    string? Message = null, string? ReasonContains = null);

internal sealed record GoldenPreview(
    List<GoldenCavePreview> Caves,
    List<GoldenEntrancePreview> Entrances,
    List<string> CountyCreations,
    List<string> TagCreations)
{
    public static GoldenPreview From(CaveImportPlan plan) => new(
        plan.CreatePreview(omitNoChange: true).Select(GoldenCavePreview.From).ToList(), [],
        plan.CountyCreations.Select(value => $"{value.DisplayId}:{value.Name}").Order().ToList(),
        plan.TagCreations.Select(value => $"{value.Key}:{value.Name}").Order().ToList());

    public static GoldenPreview From(EntranceImportPlan plan) => new([], plan.CreatePreview()
            .Select(GoldenEntrancePreview.From).ToList(), [],
        plan.TagCreations.Select(value => $"{value.Key}:{value.Name}").Order().ToList());
}

internal sealed record GoldenCavePreview(
    string Action, string Key, string Name, string State, string CountyName,
    List<string> AlternateNames, double? LengthFeet, double? DepthFeet, double? MaxPitDepthFeet,
    int? NumberOfPits, string? ReportedOn, bool IsArchived, string? Narrative,
    SortedDictionary<string, List<string>> Tags)
{
    public static GoldenCavePreview From(Planarian.Modules.Account.Import.Models.CaveDryRunRecord value) => new(
        value.Action, $"{value.CountyCode}-{value.CountyCaveNumber}", value.CaveName, value.State,
        value.CountyName, value.AlternateNames.Order().ToList(), value.CaveLengthFeet, value.CaveDepthFeet,
        value.MaxPitDepthFeet, value.NumberOfPits, GoldenSemantic.Date(value.ReportedOnDate), value.IsArchived,
        value.Narrative, GoldenSemantic.CaveTags(value.Geology, value.GeologicAges, value.MapStatuses,
            value.PhysiographicProvinces, value.Archeology, value.Biology, value.OtherTags,
            value.CartographerNames, value.ReportedByNames));
}

internal sealed record GoldenEntrancePreview(
    string CaveKey, int CountChange, string? Name, bool IsPrimary, double Latitude, double Longitude,
    double Elevation, string LocationQuality, double? PitDepthFeet, string? ReportedOn, string? Description,
    SortedDictionary<string, List<string>> Tags)
{
    public static GoldenEntrancePreview From(Planarian.Modules.Account.Import.Models.EntranceDryRun value)
    {
        var caveKey = value.AssociatedCave.Split(' ', 2)[0];
        return new GoldenEntrancePreview(caveKey, value.EntranceCountChange, value.EntranceName,
            value.IsPrimaryEntrance, value.DecimalLatitude, value.DecimalLongitude, value.EntranceElevationFt,
            value.LocationQuality, value.EntrancePitDepth, GoldenSemantic.Date(value.ReportedOnDate),
            value.EntranceDescription, GoldenSemantic.EntranceTags(value.EntranceStatuses,
                value.EntranceHydrology, value.FieldIndication, value.ReportedByNames));
    }
}

internal sealed record CaveMarker(uint Version, string? RevisionId);
