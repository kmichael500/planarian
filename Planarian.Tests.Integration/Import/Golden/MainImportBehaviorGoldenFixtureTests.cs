using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Xunit;

using Planarian.Tests;

namespace Planarian.Tests.Integration.Import.Golden;

public sealed class MainImportBehaviorGoldenFixtureTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    private const string BaselineCommit = "11cdd9edc58d85bcf14a9d82c797f715d3a0e2ae";
    private static readonly IReadOnlyDictionary<string, string> RelocatedCoverageTests =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["RequiredCaveNameIsRejected"] = "MissingCaveNameIsRejected",
            ["NegativeNumbersRemainInvalid"] = "NegativeMeasurementsAreRejected",
            ["ExistingAccountTagIsReusedCaseInsensitivelyWithCanonicalName"] =
                "ExistingTagIsReusedCaseInsensitivelyWithCanonicalSpelling",
            ["FullInsertOwnsCaveScalarsAndAllSupportedTagRoles"] = "NewCaveProjectsScalarsAndInsertIntent",
            ["SyncNoChangeIsOmittedAndCreatesNoRevision"] =
                "SemanticallyEqualExistingCavePlansNoChangeAndPreviewCanOmitIt",
            ["MissingLatitudeIsRejectedIndependently"] = "LatitudeAndLongitudeAreRequiredIndependently",
            ["NumericValidationMatchesMain"] = "CoordinateBoundsAreValidated",
            ["ExistingLocationQualityAndMultiValueTagAreReusedCaseInsensitively"] =
                "ExistingLocationQualityAndMultiValueTagsAreReusedCaseInsensitively",
            ["FullInsertPersistsScalarsTagsAndPostgisXYZ"] = "FullInsertPersistsScalarsTagsAndPostgisXyz",
            ["ExistingAndImportedPrimaryConflictIsRejected"] =
                "AppendRejectsImportedPrimaryWhenExistingPrimaryExists",
            ["SyncReplacementUsesImportedFinalPrimaryAndReplacesExisting"] =
                "SyncReplacementIgnoresExistingPrimaryAndReportsFinalCountDelta",
            ["SyncPreservesUnrelatedCave"] = "SyncPreservesUnrelatedCaveAggregateAndRevisionPointer"
        };
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
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ApplySetupAsync(database, tenant, item.Setup);

        var beforeDatabase = await NormalizedDatabaseState.CaptureAllAsync(database);
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var beforeCaves = await LoadCaveMarkersAsync(db, tenant.AccountId);
        var beforeTags = await LoadTagTypesAsync(db, tenant.AccountId);

        if (area == "cave")
            await ExecuteCaveCaseAsync(item, database, db, tenant, beforeDatabase, beforeCaves, beforeTags);
        else if (area == "entrance")
            await ExecuteEntranceCaseAsync(item, database, db, tenant, beforeDatabase, beforeCaves, beforeTags);
        else
            throw new InvalidOperationException($"Unsupported golden area '{area}'.");
    }

    [Fact]
    public void FixtureRemainsAnchoredCompleteAndTraceable()
    {
        var golden = LoadFixture();
        Assert.Equal(BaselineCommit, golden.BaselineCommit);
        var difference = Assert.Single(golden.ApprovedSemanticDifferences);
        Assert.Equal("case-insensitive-tag-creation-deduplication", difference.Id);
        Assert.Equal(BaselineCommit, difference.BaselineCommit);
        Assert.Equal(19, golden.Cases.Count);
        var required = new[]
        {
            "required-fields", "invalid-numeric", "optional-date", "state-resolution", "county-creation",
            "reference-case", "tags-and-insert", "update-and-preserve-relationships", "no-change",
            "sync-deletion", "required-coordinates", "coordinate-ranges", "elevation-pit-validation",
            "location-quality-reference-case", "location-quality-tags-geometry", "append-primary-rules", "sync-replacement",
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
        methods.UnionWith(PlannerUnitTestMethodNames());
        foreach (var item in golden.Cases)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.InputCsv));
            Assert.Contains(item.Validation.Outcome, new[] { "success", "error" });
            var currentCoverageTest = RelocatedCoverageTests.GetValueOrDefault(item.CoverageTest, item.CoverageTest);
            Assert.Contains(currentCoverageTest, methods);
            if (item.Validation.Outcome == "success")
            {
                Assert.NotNull(item.BaselinePreview);
                Assert.NotNull(item.BaselineCommitted);
            }
        }
        var registered = golden.ApprovedSemanticDifferences.Select(value => value.Id)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(golden.ApprovedSemanticDifferences.Count, registered.Count);
        Assert.All(golden.Cases.GroupBy(value => (value.Area, value.Behavior)), group => Assert.Single(group));
        var overrides = golden.Cases.Where(value => value.TargetOverride is not null).ToList();
        Assert.All(overrides, value => Assert.Contains(value.TargetOverride!.ApprovedDifferenceId, registered));
        Assert.All(golden.ApprovedSemanticDifferences, value =>
            Assert.Contains(overrides, item => item.TargetOverride!.ApprovedDifferenceId == value.Id));
        Assert.Empty(ValidateSemanticOverrides(golden));
    }

    [Fact]
    public void SemanticOverrideValidatorRejectsUnknownDifferenceId()
    {
        var golden = LoadFixture();
        var item = Assert.Single(golden.Cases, value => value.Behavior == "reference-case");
        var invalid = item.TargetOverride! with { ApprovedDifferenceId = "unregistered-semantic-difference" };
        Assert.Contains(ValidateOverride(golden, item, invalid),
            error => error.Contains("unregistered", StringComparison.Ordinal));
    }

    [Fact]
    public void SemanticOverrideValidatorRejectsUnrelatedChangeUnderValidId()
    {
        var golden = LoadFixture();
        var item = Assert.Single(golden.Cases, value => value.Behavior == "reference-case");
        var original = item.TargetOverride!;
        var caves = original.Preview.Caves.ToList();
        caves[0] = caves[0] with { Name = "Unrelated masked Cave name" };
        var invalid = original with { Preview = original.Preview with { Caves = caves } };
        Assert.Contains(ValidateOverride(golden, item, invalid),
            error => error.Contains("unrelated preview semantics", StringComparison.Ordinal));
    }

    [Fact]
    public void SemanticOverrideValidatorAcceptsBaselineAndRegisteredTagCorrections()
    {
        var golden = LoadFixture();
        Assert.Contains(golden.Cases, value => value.TargetOverride is null);
        Assert.Empty(ValidateSemanticOverrides(golden));
    }

    private static async Task ExecuteCaveCaseAsync(GoldenCase item, PostgresTestDatabase database,
        Planarian.Model.Database.PlanarianDbContext db, PublishedCaveTestData tenant, string beforeDatabase,
        IReadOnlyDictionary<string, CaveMarker> beforeCaves, IReadOnlyList<GoldenTagType> beforeTags)
    {
        var planner = new CaveImportTestHarness(db, db.RequestUser);
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
        AssertGoldenEqual(item.TargetPreview, actualPreview, $"{item.Area}/{item.Behavior} preview");

        var reader = new CavePublishedSnapshotRepository(db, db.RequestUser);
        await new CaveImportExecutionRepository(db, db.RequestUser, reader,
            new CaveBulkRevisionRepository(db, db.RequestUser),
            new Planarian.Modules.Tags.Repositories.TagReferenceLockRepository(db, db.RequestUser),
            new CountyReferenceLockRepository(db, db.RequestUser))
            .ExecuteAsync(plan, $"golden-{item.Behavior}.csv");
        db.ChangeTracker.Clear();

        var actualCommitted = await GoldenCommittedState.CaptureAsync(db, tenant.AccountId, beforeCaves, beforeTags);
        AssertGoldenEqual(item.TargetCommitted, actualCommitted, $"{item.Area}/{item.Behavior} committed");
    }

    private static async Task ExecuteEntranceCaseAsync(GoldenCase item, PostgresTestDatabase database,
        Planarian.Model.Database.PlanarianDbContext db, PublishedCaveTestData tenant, string beforeDatabase,
        IReadOnlyDictionary<string, CaveMarker> beforeCaves, IReadOnlyList<GoldenTagType> beforeTags)
    {
        var planner = new EntranceImportTestHarness(db, db.RequestUser);
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
        AssertGoldenEqual(item.TargetPreview, actualPreview, $"{item.Area}/{item.Behavior} preview");

        var reader = new CavePublishedSnapshotRepository(db, db.RequestUser);
        await new EntranceImportExecutionRepository(db, db.RequestUser, reader,
            new CaveBulkRevisionRepository(db, db.RequestUser),
            new Planarian.Modules.Tags.Repositories.TagReferenceLockRepository(db, db.RequestUser))
            .ExecuteAsync(plan, $"golden-{item.Behavior}.csv");
        db.ChangeTracker.Clear();

        var actualCommitted = await GoldenCommittedState.CaptureAsync(db, tenant.AccountId, beforeCaves, beforeTags);
        AssertGoldenEqual(item.TargetCommitted, actualCommitted, $"{item.Area}/{item.Behavior} committed");
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

    private static IReadOnlyList<string> ValidateSemanticOverrides(GoldenFixture golden)
    {
        var errors = new List<string>();
        foreach (var item in golden.Cases.Where(value => value.TargetOverride is not null))
            errors.AddRange(ValidateOverride(golden, item, item.TargetOverride!));
        return errors;
    }

    private static IReadOnlyList<string> ValidateOverride(GoldenFixture golden, GoldenCase item,
        GoldenTargetOverride target)
    {
        var errors = new List<string>();
        if (!golden.ApprovedSemanticDifferences.Any(value => value.Id == target.ApprovedDifferenceId))
            errors.Add($"Override {item.Area}/{item.Behavior} uses unregistered semantic difference '{target.ApprovedDifferenceId}'.");
        if (target.ApprovedDifferenceId != "case-insensitive-tag-creation-deduplication") return errors;

        var baselinePreview = item.BaselinePreview!;
        var remainingBaselineCreations = baselinePreview.TagCreations.ToList();
        foreach (var creation in target.Preview.TagCreations)
        {
            var index = remainingBaselineCreations.FindIndex(value => value == creation);
            if (index < 0)
            {
                errors.Add($"Override {item.Area}/{item.Behavior} adds or changes a tag creation outside the approved correction.");
                break;
            }
            remainingBaselineCreations.RemoveAt(index);
        }
        if (remainingBaselineCreations.Count == 0)
            errors.Add($"Override {item.Area}/{item.Behavior} does not exercise the approved tag-case correction.");
        var associatedTags = AssociatedTags(baselinePreview).ToList();
        foreach (var removed in remainingBaselineCreations)
        {
            var (key, name) = SplitTagCreation(removed);
            if (!associatedTags.Any(tag => tag.Key == key &&
                    tag.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase) && tag.Name != name))
                errors.Add($"Override {item.Area}/{item.Behavior} suppresses tag creation '{removed}' without an associated case-only canonical match.");
        }
        var permittedPreview = baselinePreview with { TagCreations = target.Preview.TagCreations };
        if (GoldenJson(permittedPreview) != GoldenJson(target.Preview))
            errors.Add($"Override {item.Area}/{item.Behavior} changes unrelated preview semantics.");

        var baselineCommitted = item.BaselineCommitted!.WithExpectedTagTypes(baselinePreview.TagCreations);
        var targetCommitted = target.Committed.WithExpectedTagTypes(target.Preview.TagCreations);
        if (GoldenJson(targetCommitted.TagTypes) != GoldenJson(ExpectedTagTypes(target.Preview.TagCreations)))
            errors.Add($"Override {item.Area}/{item.Behavior} supplies a committed TagType delta not derived from its approved tag creations.");
        var permittedCommitted = baselineCommitted with { TagTypes = targetCommitted.TagTypes };
        if (GoldenJson(permittedCommitted) != GoldenJson(targetCommitted))
            errors.Add($"Override {item.Area}/{item.Behavior} changes unrelated committed semantics.");
        return errors;
    }

    private static IEnumerable<(string Key, string Name)> AssociatedTags(GoldenPreview preview)
    {
        foreach (var cave in preview.Caves)
        foreach (var tag in cave.Tags)
        foreach (var name in tag.Value)
            yield return (tag.Key is "Cartographer" or "CaveReportedBy" ? TagTypeKeyConstant.People : tag.Key, name);
        foreach (var entrance in preview.Entrances)
        {
            yield return (TagTypeKeyConstant.LocationQuality, entrance.LocationQuality);
            foreach (var tag in entrance.Tags)
            foreach (var name in tag.Value)
                yield return (tag.Key == "EntranceReportedBy" ? TagTypeKeyConstant.People : tag.Key, name);
        }
    }

    private static (string Key, string Name) SplitTagCreation(string creation)
    {
        var separator = creation.IndexOf(':');
        return (creation[..separator], creation[(separator + 1)..]);
    }

    private static GoldenTagTypeDelta ExpectedTagTypes(IEnumerable<string> creations) => new(
        creations.Select(creation =>
        {
            var (key, name) = SplitTagCreation(creation);
            return new GoldenTagType(key, name, "account");
        }).OrderBy(tag => tag.Key).ThenBy(tag => tag.Name).ToList(), []);

    private static string GoldenJson<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static GoldenFixture LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "import-main-11cdd9e.json");
        return JsonSerializer.Deserialize<GoldenFixture>(System.IO.File.ReadAllText(path), JsonOptions)
               ?? throw new InvalidOperationException("Golden fixture could not be deserialized.");
    }

    private static IEnumerable<string> PlannerUnitTestMethodNames()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !System.IO.File.Exists(Path.Combine(directory.FullName, "Planarian", "Planarian.sln")))
            directory = directory.Parent;
        if (directory is null)
            throw new DirectoryNotFoundException("Could not locate repository root for planner test traceability.");

        var plannerTestDirectory = Path.Combine(directory.FullName, "Planarian.Tests.Unit", "Import");
        return Directory.EnumerateFiles(plannerTestDirectory, "*PlannerTests.cs", SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(System.IO.File.ReadAllText(path),
                    @"public\s+(?:async\s+)?(?:Task|void)\s+(?<name>[A-Za-z0-9_]+)\s*\(")
                .Select(match => match.Groups["name"].Value));
    }

    private static async Task ApplySetupAsync(PostgresTestDatabase database, PublishedCaveTestData tenant, string setup)
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
            case "existing-location-quality":
                await using (var db = database.CreateDbContext("a", tenant.AccountId))
                {
                    db.TagTypes.Add(Tag(tenant.AccountId, TagTypeKeyConstant.LocationQuality, "Survey Grade"));
                    await db.SaveChangesAsync();
                }
                return;
            case "existing-entrance-and-file":
                await SeedEntranceAsync(database, tenant, "preserved0", "Preserved", true);
                var testFile = await FileTestDataFactory.AddFileAsync(database, tenant);
                await using (var db = database.CreateDbContext("a", tenant.AccountId))
                {
                    var file = await db.Files.SingleAsync(value => value.Id == testFile.FileId);
                    file.CaveId = tenant.CaveId;
                    // The main-anchored Golden scenario observes the complete filename as seed-a.pdf.
                    // Legacy DisplayName was not part of that observable contract, so do not change Name here.
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

    private static async Task SeedEntranceAsync(PostgresTestDatabase database, PublishedCaveTestData tenant, string id,
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

    private static async Task SeedUnrelatedCaveAsync(PostgresTestDatabase database, PublishedCaveTestData tenant)
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

    private static async Task<IReadOnlyList<GoldenTagType>> LoadTagTypesAsync(
        Planarian.Model.Database.PlanarianDbContext db, string accountId)
    {
        var tags = await db.TagTypes.IgnoreQueryFilters()
            .Where(tag => tag.AccountId == accountId || tag.IsDefault)
            .AsNoTracking().ToListAsync();
        return tags.Select(tag => GoldenTagType.From(tag, accountId))
            .OrderBy(tag => tag.Key).ThenBy(tag => tag.Name).ThenBy(tag => tag.Ownership).ToList();
    }
}
