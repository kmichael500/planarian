using Planarian.Library.Exceptions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Import.Models;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class CaveImportPlannerTests
{
    [Fact]
    public void NewCaveProjectsScalarsAndInsertIntent()
    {
        var record = CaveRecord(7, value =>
        {
            value.CaveName = "  Pure Cave  ";
            value.AlternateNames = " North, South ";
            value.CaveLengthFt = 123.5;
            value.CaveDepthFt = 45;
            value.MaxPitDepthFt = 12;
            value.NumberOfPits = 3;
            value.Narrative = "  narrative  ";
            value.ReportedOnDate = "2026-08-01";
            value.IsArchived = true;
        });

        var plan = Plan([record]);

        var cave = Assert.Single(plan.Caves);
        Assert.Equal(CaveImportAction.Insert, cave.Action);
        Assert.Equal("Pure Cave", cave.Name);
        Assert.Equal(["North", "South"], cave.AlternateNames);
        Assert.Equal(123.5, cave.LengthFeet);
        Assert.Equal(45, cave.DepthFeet);
        Assert.Equal(12, cave.MaxPitDepthFeet);
        Assert.Equal(3, cave.NumberOfPits);
        Assert.Equal("narrative", cave.Narrative);
        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), cave.ReportedOn);
        Assert.True(cave.IsArchived);
    }

    [Fact]
    public void MissingCaveNameIsRejected() => AssertInvalid(CaveRecord(configure: value => value.CaveName = ""));

    [Fact]
    public void StateMatchingRemainsCaseSensitive() =>
        Assert.Throws<ApiException>(() => Plan([CaveRecord(configure: value => value.State = "aa")]));

    [Theory]
    [InlineData(-1, 1, 1, 1)]
    [InlineData(1, -1, 1, 1)]
    [InlineData(1, 1, -1, 1)]
    [InlineData(1, 1, 1, -1)]
    public void NegativeMeasurementsAreRejected(double length, double depth, double pit, int pits)
    {
        AssertInvalid(CaveRecord(configure: value =>
        {
            value.CaveLengthFt = length;
            value.CaveDepthFt = depth;
            value.MaxPitDepthFt = pit;
            value.NumberOfPits = pits;
        }));
    }

    [Fact]
    public void NonSyncDuplicateCountyNumberIsRejected()
    {
        var state = State(used: new HashSet<CaveImportUsedCountyNumber>
        {
            new("county", 7)
        });
        Assert.Throws<ApiException>(() => Plan([CaveRecord(7)], state));
    }

    [Fact]
    public void SyncDuplicateCompositeKeyIsRejected()
    {
        var records = new[] { CaveRecord(7), CaveRecord(7, value => value.CaveName = "Second") };
        Assert.Throws<ApiException>(() => Plan(records, State(existing: [Existing()]), sync: true));
    }

    [Fact]
    public void SemanticallyEqualExistingCavePlansNoChangeAndPreviewCanOmitIt()
    {
        var plan = Plan([CaveRecord()], State(existing: [Existing()]), sync: true);

        Assert.Equal(CaveImportAction.NoChange, Assert.Single(plan.Caves).Action);
        Assert.Empty(plan.CreatePreview(omitNoChange: true));
        Assert.Equal("no change", Assert.Single(plan.CreatePreview(omitNoChange: false)).Action);
    }

    [Fact]
    public void ChangedExistingCavePlansUpdateWithExpectedConcurrencyTarget()
    {
        var plan = Plan(
            [CaveRecord(configure: value => value.CaveName = "Updated")],
            State(existing: [Existing(version: 42, revision: "revision")]),
            sync: true);

        Assert.Equal(CaveImportAction.Update, Assert.Single(plan.Caves).Action);
        var target = Assert.Single(plan.ExistingTargets).Value;
        Assert.Equal((uint)42, target.Version);
        Assert.Equal("revision", target.CurrentRevisionId);
    }

    [Fact]
    public void SyncPlansDeletionForExistingCaveMissingFromInput()
    {
        var plan = Plan([CaveRecord(8)], State(existing: [Existing()]), sync: true);

        Assert.Equal("cave", Assert.Single(plan.Deletions).CaveId);
        Assert.Contains(plan.CreatePreview(omitNoChange: true), row => row.Action == "delete");
    }

    [Fact]
    public void MissingAccountStateAndCountyProduceCreationIntents()
    {
        var state = State(hasAccountState: false, counties: []);

        var plan = Plan([CaveRecord()], state);

        Assert.Equal("state", Assert.Single(plan.AccountStateCreations).StateId);
        var county = Assert.Single(plan.CountyCreations);
        Assert.Equal(("state", "A01", "Alpha"), (county.StateId, county.DisplayId, county.Name));
    }

    [Fact]
    public void ExistingTagIsReusedCaseInsensitivelyWithCanonicalSpelling()
    {
        var tag = Tag("geo", TagTypeKeyConstant.Geology, "Limestone", AccountId);

        var plan = Plan([CaveRecord(configure: value => value.Geology = "  limestone  ")], State(tags: [tag]));

        Assert.Empty(plan.TagCreations);
        var cave = Assert.Single(plan.Caves);
        Assert.Equal(["Limestone"], cave.Geology);
        Assert.Equal("geo", Assert.Single(cave.Tags).TagTypeId);
    }

    [Fact]
    public void CaseVariantsCreateOneTagUsingFirstEncounteredSpelling()
    {
        var records = new[]
        {
            CaveRecord(7, value => value.Geology = "Limestone, limestone"),
            CaveRecord(8, value => value.Geology = "LIMESTONE")
        };

        var plan = Plan(records);

        var creation = Assert.Single(plan.TagCreations);
        Assert.Equal("Limestone", creation.Name);
        Assert.All(plan.Caves, cave => Assert.Equal(creation.Id,
            Assert.Single(cave.Tags, tag => tag.Role == CaveImportTagRole.Geology).TagTypeId));
    }

    [Fact]
    public void SameTextUnderDifferentTagKeysRemainsIndependent()
    {
        var plan = Plan([CaveRecord(configure: value =>
        {
            value.Geology = "Active";
            value.MapStatuses = "active";
        })]);

        Assert.Equal(2, plan.TagCreations.Count);
        Assert.Contains(plan.TagCreations, tag => tag.Key == TagTypeKeyConstant.Geology);
        Assert.Contains(plan.TagCreations, tag => tag.Key == TagTypeKeyConstant.MapStatus);
    }

    [Fact]
    public void AccountTagPrecedesDefaultWhenNeitherSpellingIsExact()
    {
        var tags = new[]
        {
            Tag("default", TagTypeKeyConstant.Geology, "Foo", null, isDefault: true),
            Tag("account", TagTypeKeyConstant.Geology, "foo", AccountId)
        };

        var cave = Assert.Single(Plan([CaveRecord(configure: value => value.Geology = "FOO")], State(tags: tags)).Caves);

        Assert.Equal("account", Assert.Single(cave.Tags).TagTypeId);
        Assert.Equal(["foo"], cave.Geology);
    }

    [Fact]
    public void ForeignCustomTagIsExcludedFromEligiblePlanningState()
    {
        var foreignTagWasExcludedByRepository = State(tags: []);
        var plan = Plan([CaveRecord(configure: value => value.Geology = "Foreign Geo")], foreignTagWasExcludedByRepository);

        Assert.Equal("Foreign Geo", Assert.Single(plan.TagCreations).Name);
    }

    [Fact]
    public void PreexistingCaseOnlyDuplicatesResolveDeterministically()
    {
        var tags = new[]
        {
            Tag("zzzzzzzzzz", TagTypeKeyConstant.Geology, "Foo", AccountId),
            Tag("aaaaaaaaaa", TagTypeKeyConstant.Geology, "foo", AccountId)
        };

        var plan = Plan([CaveRecord(configure: value => value.Geology = "FOO")], State(tags: tags));

        Assert.Empty(plan.TagCreations);
        Assert.Equal("aaaaaaaaaa", Assert.Single(Assert.Single(plan.Caves).Tags).TagTypeId);
    }

    [Fact]
    public void TagRolesRemainIndependentAndPeopleUseFieldEncounterOrder()
    {
        var record = CaveRecord(configure: value =>
        {
            value.Geology = "Geo";
            value.GeologicAges = "Age";
            value.MapStatuses = "Map";
            value.PhysiographicProvinces = "Province";
            value.Archeology = "Artifact";
            value.Biology = "Bat";
            value.OtherTags = "Other";
            value.CartographerNames = "Alice";
            value.ReportedByNames = "alice, Bob";
        });

        var plan = Plan([record]);
        var cave = Assert.Single(plan.Caves);

        Assert.Equal(9, plan.TagCreations.Count);
        Assert.Equal("Alice", plan.TagCreations.First(tag => tag.Key == TagTypeKeyConstant.People).Name);
        Assert.Equal(["Alice"], cave.CartographerNames);
        Assert.Equal(["Alice", "Bob"], cave.ReportedByNames);
        Assert.Equal(9, cave.Tags.Select(tag => tag.Role).Distinct().Count());
    }

    [Fact]
    public void CancellationIsObservedDuringPlanning()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            new CaveImportPlanner().Plan([CaveRecord()], State(), false, cancellation.Token));
    }

    private static CaveImportPlan Plan(IReadOnlyList<CaveCsvModel> records,
        CaveImportPlanningState? state = null, bool sync = false) =>
        new CaveImportPlanner().Plan(records, state ?? State(), sync);

    private static void AssertInvalid(CaveCsvModel record) =>
        Assert.Throws<ApiException>(() => Plan([record]));

    private static CaveCsvModel CaveRecord(int countyNumber = 7, Action<CaveCsvModel>? configure = null)
    {
        var record = new CaveCsvModel
        {
            CaveName = "Pure Cave",
            State = "AA",
            CountyCode = "A01",
            CountyName = "Alpha",
            CountyCaveNumber = countyNumber,
            IsArchived = false
        };
        configure?.Invoke(record);
        return record;
    }

    private static CaveImportPlanningState State(
        IReadOnlyList<ImportTagLookup>? tags = null,
        IReadOnlyList<CaveImportExistingCave>? existing = null,
        IReadOnlySet<CaveImportUsedCountyNumber>? used = null,
        bool hasAccountState = true,
        IReadOnlyList<CaveImportCountyLookup>? counties = null) => new(
        AccountId,
        [new CaveImportStateLookup("state", "Alpha State", "AA")],
        hasAccountState ? new HashSet<string> { "state" } : new HashSet<string>(),
        counties ?? [new CaveImportCountyLookup("county", "state", "A01", "Alpha")],
        tags ?? [],
        existing ?? [],
        used ?? new HashSet<CaveImportUsedCountyNumber>());

    private static CaveImportExistingCave Existing(uint version = 3, string? revision = "revision") => new(
        "cave", version, revision, "state", "AA", "county", "Alpha", "A01", 7, "Pure Cave", "[]",
        null, null, null, null, null, null, false,
        [], [], [], [], [], [], [], [], []);

    private static ImportTagLookup Tag(string id, string key, string name, string? accountId,
        bool isDefault = false) => new(id, key, name, accountId, isDefault, false);

    private const string AccountId = "account";
}
