using System.Collections;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class MainImportBehaviorGoldenFixtureTests(PostgresIntegrationFixture fixture)
    : IClassFixture<PostgresIntegrationFixture>
{
    private const string BaselineCommit = "11cdd9edc58d85bcf14a9d82c797f715d3a0e2ae";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static IEnumerable<object[]> GoldenCases() => LoadFixture().Cases
        .Select(item => new object[] { item.Area, item.Behavior });

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public async Task FixtureCaseDirectlyExecutesImporter(string area, string behavior)
    {
        var item = Assert.Single(LoadFixture().Cases,
            value => value.Area == area && value.Behavior == behavior);
        await using var database = await fixture.CreateDatabaseAsync($"golden_{area}_{behavior}");
        var tenant = await IntegrationTestData.SeedTenantAsync(database, 'a');
        await ApplySetupAsync(database, tenant, item.Setup);

        var beforeDatabase = await NormalizedDatabaseState.CaptureAllAsync(database);
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var beforeCaves = await LoadCaveMarkersAsync(db, tenant.AccountId);

        if (area == "cave")
            await ExecuteCaveCaseAsync(item, database, db, tenant, beforeDatabase, beforeCaves);
        else if (area == "entrance")
            await ExecuteEntranceCaseAsync(item, database, db, tenant, beforeDatabase, beforeCaves);
        else
            throw new InvalidOperationException($"Unsupported golden area '{area}'.");
    }

    [Fact]
    public void FixtureRemainsAnchoredCompleteAndTraceable()
    {
        var golden = LoadFixture();
        Assert.Equal(BaselineCommit, golden.BaselineCommit);
        Assert.Equal(18, golden.Cases.Count);
        var required = new[]
        {
            "required-fields", "invalid-numeric", "optional-date", "state-resolution", "county-creation",
            "reference-case", "tags-and-insert", "update-and-preserve-relationships", "no-change",
            "sync-deletion", "required-coordinates", "coordinate-ranges", "elevation-pit-validation",
            "location-quality-tags-geometry", "append-primary-rules", "sync-replacement",
            "targeted-deletion-scope"
        };
        foreach (var behavior in required) Assert.Contains(golden.Cases, item => item.Behavior == behavior);

        var methods = new[]
            {
                typeof(CaveImportCompatibilityTests), typeof(EntranceImportCompatibilityTests),
                typeof(ImportPreviewCommitEquivalenceTests)
            }
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var item in golden.Cases)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.InputCsv));
            Assert.Contains(item.Validation.Outcome, new[] { "success", "error" });
            Assert.Contains(item.CoverageTest, methods);
            if (item.Validation.Outcome == "success")
            {
                Assert.NotNull(item.Preview);
                Assert.NotNull(item.Committed);
            }
        }
    }

    private static async Task ExecuteCaveCaseAsync(GoldenCase item, PostgresTestDatabase database,
        Planarian.Model.Database.PlanarianDbContext db, TenantSeed tenant, string beforeDatabase,
        IReadOnlyDictionary<string, CaveMarker> beforeCaves)
    {
        var planner = new CaveImportPlanner(db, db.RequestUser);
        await using var csv = ImportDryRunIntegrationTests.CsvStream(
            ImportDryRunIntegrationTests.CaveHeader + "\n" + item.InputCsv + "\n");

        CaveImportPlan plan;
        try
        {
            plan = await planner.PlanAsync(csv, item.Sync);
        }
        catch (ApiException exception)
        {
            AssertValidationError(item, exception);
            Assert.Equal(beforeDatabase, await NormalizedDatabaseState.CaptureAllAsync(database));
            return;
        }

        AssertValidationSuccess(item);
        var actualPreview = GoldenPreview.From(plan);
        AssertGoldenEqual(item.Preview!, actualPreview, $"{item.Area}/{item.Behavior} preview");

        var reader = new CavePublishedSnapshotReader(db, db.RequestUser);
        await new CaveImportExecutor(db, db.RequestUser, reader,
            new ImportRevisionPublisher(db, db.RequestUser)).ExecuteAsync(plan, $"golden-{item.Behavior}.csv");
        db.ChangeTracker.Clear();

        var actualCommitted = await GoldenCommittedState.CaptureAsync(db, tenant.AccountId, beforeCaves);
        AssertGoldenEqual(item.Committed!, actualCommitted, $"{item.Area}/{item.Behavior} committed");
    }

    private static async Task ExecuteEntranceCaseAsync(GoldenCase item, PostgresTestDatabase database,
        Planarian.Model.Database.PlanarianDbContext db, TenantSeed tenant, string beforeDatabase,
        IReadOnlyDictionary<string, CaveMarker> beforeCaves)
    {
        var planner = new EntranceImportPlanner(db, db.RequestUser);
        await using var csv = ImportDryRunIntegrationTests.CsvStream(
            ImportDryRunIntegrationTests.EntranceHeader + "\n" + item.InputCsv + "\n");

        EntranceImportPlan plan;
        try
        {
            plan = await planner.PlanAsync(csv, item.Sync);
        }
        catch (ApiException exception)
        {
            AssertValidationError(item, exception);
            Assert.Equal(beforeDatabase, await NormalizedDatabaseState.CaptureAllAsync(database));
            return;
        }

        AssertValidationSuccess(item);
        var actualPreview = GoldenPreview.From(plan);
        AssertGoldenEqual(item.Preview!, actualPreview, $"{item.Area}/{item.Behavior} preview");

        var reader = new CavePublishedSnapshotReader(db, db.RequestUser);
        await new EntranceImportExecutor(db, db.RequestUser, reader,
            new ImportRevisionPublisher(db, db.RequestUser)).ExecuteAsync(plan, $"golden-{item.Behavior}.csv");
        db.ChangeTracker.Clear();

        var actualCommitted = await GoldenCommittedState.CaptureAsync(db, tenant.AccountId, beforeCaves);
        AssertGoldenEqual(item.Committed!, actualCommitted, $"{item.Area}/{item.Behavior} committed");
    }

    private static void AssertValidationSuccess(GoldenCase item) =>
        Assert.Equal("success", item.Validation.Outcome);

    private static void AssertValidationError(GoldenCase item, ApiException exception)
    {
        Assert.Equal("error", item.Validation.Outcome);
        Assert.Equal(item.Validation.StatusCode, exception.StatusCode);
        Assert.Equal(item.Validation.ErrorCode, exception.ErrorCode.ToString());
        if (item.Validation.Message is not null) Assert.Equal(item.Validation.Message, exception.Message);
        if (item.Validation.ReasonContains is not null)
            Assert.Contains(item.Validation.ReasonContains, ExtractReasons(exception), StringComparison.Ordinal);
    }

    private static string ExtractReasons(ApiException exception)
    {
        if (exception.Data is not IEnumerable values) return string.Empty;
        return string.Join("\n", values.Cast<object>().Select(value =>
            value.GetType().GetProperty("Reason")?.GetValue(value)?.ToString() ?? string.Empty));
    }

    private static void AssertGoldenEqual<T>(T expected, T actual, string label)
    {
        var expectedJson = JsonSerializer.Serialize(expected, JsonOptions);
        var actualJson = JsonSerializer.Serialize(actual, JsonOptions);
        Assert.True(expectedJson == actualJson,
            $"Golden mismatch for {label}.\nExpected:\n{expectedJson}\nActual:\n{actualJson}");
    }

    private static GoldenFixture LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "import-main-11cdd9e.json");
        return JsonSerializer.Deserialize<GoldenFixture>(System.IO.File.ReadAllText(path), JsonOptions)
               ?? throw new InvalidOperationException("Golden fixture could not be deserialized.");
    }

    private static async Task ApplySetupAsync(PostgresTestDatabase database, TenantSeed tenant, string setup)
    {
        switch (setup)
        {
            case "default":
                return;
            case "existing-geology-case":
                await using (var db = database.CreateDbContext("a", tenant.AccountId))
                {
                    db.TagTypes.Add(Tag(tenant.AccountId, TagTypeKeyConstant.Geology, "Foo"));
                    await db.SaveChangesAsync();
                }
                return;
            case "existing-entrance-and-file":
                await SeedEntranceAsync(database, tenant, "preserved0", "Preserved", true);
                await using (var db = database.CreateDbContext("a", tenant.AccountId))
                {
                    var file = await db.Files.SingleAsync(value => value.Id == tenant.FileId);
                    file.CaveId = tenant.CaveId;
                    file.DisplayName = "Preserved display";
                    file.ExpiresOn = new DateTime(2027, 1, 2, 0, 0, 0, DateTimeKind.Utc);
                    await db.SaveChangesAsync();
                }
                return;
            case "existing-primary":
                await SeedEntranceAsync(database, tenant, "existing00", "Existing", true);
                return;
            case "existing-primary-and-unrelated-cave":
                await SeedEntranceAsync(database, tenant, "existing00", "Existing", true);
                await SeedUnrelatedCaveAsync(database, tenant);
                return;
            default:
                throw new InvalidOperationException($"Unknown golden setup '{setup}'.");
        }
    }

    private static async Task SeedEntranceAsync(PostgresTestDatabase database, TenantSeed tenant, string id,
        string name, bool primary)
    {
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var quality = Tag(tenant.AccountId, TagTypeKeyConstant.LocationQuality, "Survey Grade");
        db.TagTypes.Add(quality);
        await db.SaveChangesAsync();
        db.Entrances.Add(new Entrance
        {
            Id = id, CaveId = tenant.CaveId, Name = name, LocationQualityTagId = quality.Id,
            IsPrimary = primary, Location = new Point(new CoordinateZ(-86, 35, 500)) { SRID = 4326 }
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedUnrelatedCaveAsync(PostgresTestDatabase database, TenantSeed tenant)
    {
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var cave = new Cave
        {
            Id = "secondcav0", AccountId = tenant.AccountId, StateId = tenant.StateId, CountyId = tenant.CountyId,
            CountyNumber = 2, Name = "Second", IsArchived = false
        };
        var quality = Tag(tenant.AccountId, TagTypeKeyConstant.LocationQuality, "Second Quality");
        db.Caves.Add(cave);
        db.TagTypes.Add(quality);
        await db.SaveChangesAsync();
        db.Entrances.Add(new Entrance
        {
            Id = "secondent0", CaveId = cave.Id, LocationQualityTagId = quality.Id, Name = "Unrelated",
            Description = "Preserve me", ReportedOn = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            PitDepthFeet = 11, IsPrimary = true,
            Location = new Point(new CoordinateZ(-86, 35, 500)) { SRID = 4326 }
        });
        await db.SaveChangesAsync();
    }

    private static TagType Tag(string accountId, string key, string name) => new(name, key)
    {
        Id = IdGenerator.Generate(), AccountId = accountId, IsDefault = false
    };

    private static async Task<IReadOnlyDictionary<string, CaveMarker>> LoadCaveMarkersAsync(
        Planarian.Model.Database.PlanarianDbContext db, string accountId) =>
        await db.Caves.IgnoreQueryFilters().Where(cave => cave.AccountId == accountId).AsNoTracking()
            .ToDictionaryAsync(cave => cave.Id, cave => new CaveMarker(cave.Version, cave.CurrentRevisionId));
}

internal sealed class GoldenFixture
{
    public string BaselineCommit { get; init; } = string.Empty;
    public List<GoldenCase> Cases { get; init; } = [];
}

internal sealed class GoldenCase
{
    public string Area { get; init; } = string.Empty;
    public string Behavior { get; init; } = string.Empty;
    public string Setup { get; init; } = "default";
    public string InputCsv { get; init; } = string.Empty;
    public bool Sync { get; init; }
    public GoldenValidation Validation { get; init; } = new();
    public GoldenPreview? Preview { get; init; }
    public GoldenCommittedState? Committed { get; init; }
    public string CoverageTest { get; init; } = string.Empty;
}

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

internal sealed record GoldenCommittedState(
    List<string> TenantCaveKeys,
    List<string> TenantCountyCodes,
    List<GoldenCommittedCave> Caves)
{
    public static async Task<GoldenCommittedState> CaptureAsync(Planarian.Model.Database.PlanarianDbContext db,
        string accountId, IReadOnlyDictionary<string, CaveMarker> before)
    {
        var caveRows = await db.Caves.IgnoreQueryFilters().Where(cave => cave.AccountId == accountId)
            .Include(cave => cave.County).Include(cave => cave.State).AsNoTracking().ToListAsync();
        var reader = new CavePublishedSnapshotReader(db, db.RequestUser);
        var caves = new List<GoldenCommittedCave>();
        foreach (var cave in caveRows.OrderBy(value => value.County.DisplayId).ThenBy(value => value.CountyNumber))
        {
            var snapshot = await reader.BuildAsync(cave.Id);
            var markerChanged = !before.TryGetValue(cave.Id, out var marker) ||
                                marker.Version != cave.Version || marker.RevisionId != cave.CurrentRevisionId;
            string? operation = null;
            if (markerChanged && cave.CurrentRevisionId is not null)
                operation = (await db.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
                    .SingleAsync(value => value.Id == cave.CurrentRevisionId)).Operation.ToString();
            var fileNames = await db.Files.IgnoreQueryFilters().Where(file => file.CaveId == cave.Id)
                .OrderBy(file => file.FileName).Select(file => file.FileName).ToListAsync();
            caves.Add(GoldenCommittedCave.From(snapshot, markerChanged, operation, fileNames));
        }

        var counties = await db.Counties.IgnoreQueryFilters().Where(county => county.AccountId == accountId)
            .OrderBy(county => county.DisplayId).Select(county => $"{county.DisplayId}:{county.Name}").ToListAsync();
        return new GoldenCommittedState(caves.Select(cave => cave.Key).ToList(), counties, caves);
    }
}

internal sealed record GoldenCommittedCave(
    string Key, string Name, string State, string CountyName, List<string> AlternateNames,
    double? LengthFeet, double? DepthFeet, double? MaxPitDepthFeet, int? NumberOfPits,
    string? ReportedOn, bool IsArchived, string? Narrative, SortedDictionary<string, List<string>> Tags,
    List<GoldenCommittedEntrance> Entrances, List<string> FileNames,
    bool VersionOrRevisionChanged, string? LatestRevisionOperation)
{
    public static GoldenCommittedCave From(CavePublishedSnapshotV1 value, bool changed, string? operation,
        List<string> fileNames) => new(
        $"{value.County.DisplayIdAtRevision}-{value.CountyNumber}", value.Name,
        value.State.AbbreviationAtRevision ?? string.Empty, value.County.NameAtRevision,
        value.AlternateNames.Order().ToList(), value.LengthFeet, value.DepthFeet, value.MaxPitDepthFeet,
        value.NumberOfPits, GoldenSemantic.Date(value.ReportedOn), value.IsArchived, value.Narrative,
        GoldenSemantic.SnapshotTags(value.Tags), value.Entrances.Select(GoldenCommittedEntrance.From)
            .OrderBy(entrance => entrance.Name).ThenBy(entrance => entrance.Latitude).ToList(),
        fileNames, changed, operation);
}

internal sealed record GoldenCommittedEntrance(
    string? Name, bool IsPrimary, double? Latitude, double? Longitude, double? Elevation, int? Srid,
    string? LocationQuality, double? PitDepthFeet, string? ReportedOn, string? Description,
    SortedDictionary<string, List<string>> Tags)
{
    public static GoldenCommittedEntrance From(CaveEntranceSnapshotV1 value) => new(
        value.Name, value.IsPrimary, value.Latitude, value.Longitude, value.Elevation, value.Srid,
        value.LocationQualityNameAtRevision, value.PitDepthFeet, GoldenSemantic.Date(value.ReportedOn),
        value.Description, GoldenSemantic.SnapshotTags(value.Tags));
}

internal static class GoldenSemantic
{
    public static string? Date(DateTime? value) => value?.ToString("yyyy-MM-dd");

    public static SortedDictionary<string, List<string>> CaveTags(
        IEnumerable<string> geology, IEnumerable<string> ages, IEnumerable<string> map,
        IEnumerable<string> provinces, IEnumerable<string> archeology, IEnumerable<string> biology,
        IEnumerable<string> other, IEnumerable<string> cartographers, IEnumerable<string> reporters) =>
        NonEmpty(("Geology", geology), ("GeologicAge", ages), ("MapStatus", map),
            ("PhysiographicProvince", provinces), ("Archeology", archeology), ("Biology", biology),
            ("CaveOther", other), ("Cartographer", cartographers), ("CaveReportedBy", reporters));

    public static SortedDictionary<string, List<string>> EntranceTags(
        IEnumerable<string> statuses, IEnumerable<string> hydrology, IEnumerable<string> field,
        IEnumerable<string> reporters) => NonEmpty(("EntranceStatus", statuses),
        ("EntranceHydrology", hydrology), ("FieldIndication", field), ("EntranceReportedBy", reporters));

    public static SortedDictionary<string, List<string>> SnapshotTags(IEnumerable<SnapshotTagReference> tags) =>
        new(tags.GroupBy(tag => tag.Role.ToString()).OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Select(tag => tag.NameAtRevision).Order().ToList()));

    private static SortedDictionary<string, List<string>> NonEmpty(
        params (string Role, IEnumerable<string> Values)[] groups) => new(groups
        .Select(group => (group.Role, Values: group.Values.Order().ToList()))
        .Where(group => group.Values.Count > 0)
        .ToDictionary(group => group.Role, group => group.Values));
}
