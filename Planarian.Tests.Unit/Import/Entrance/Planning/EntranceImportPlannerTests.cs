using Planarian.Library.Exceptions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Import.Models;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class EntranceImportPlannerTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void LatitudeAndLongitudeAreRequiredIndependently(bool missingLatitude, bool missingLongitude)
    {
        var record = EntranceRecord(value =>
        {
            value.DecimalLatitude = missingLatitude ? null : 35;
            value.DecimalLongitude = missingLongitude ? null : -86;
        });

        AssertInvalid([record]);
    }

    [Theory]
    [InlineData(90.1, -86)]
    [InlineData(-90.1, -86)]
    [InlineData(35, 180.1)]
    [InlineData(35, -180.1)]
    public void CoordinateBoundsAreValidated(double latitude, double longitude) =>
        AssertInvalid([EntranceRecord(value =>
        {
            value.DecimalLatitude = latitude;
            value.DecimalLongitude = longitude;
        })]);

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(500, -1)]
    public void ElevationAndPitDepthMustBeNonNegative(double elevation, double pitDepth) =>
        AssertInvalid([EntranceRecord(value =>
        {
            value.EntranceElevationFt = elevation;
            value.EntrancePitDepth = pitDepth;
        })]);

    [Fact]
    public void InvalidOptionalDateBecomesNull()
    {
        var entrance = Assert.Single(Plan([EntranceRecord(value => value.ReportedOnDate = "not-a-date")]).Entrances);
        Assert.Null(entrance.ReportedOn);
    }

    [Fact]
    public void ExistingLocationQualityAndMultiValueTagsAreReusedCaseInsensitively()
    {
        var tags = new[]
        {
            Tag("quality", TagTypeKeyConstant.LocationQuality, "Survey Grade", AccountId),
            Tag("open", TagTypeKeyConstant.EntranceStatus, "Open", AccountId)
        };

        var plan = Plan([EntranceRecord(value =>
        {
            value.LocationQuality = " survey grade ";
            value.EntranceStatuses = "open, OPEN";
        })], State(tags: tags));

        Assert.Empty(plan.TagCreations);
        var entrance = Assert.Single(plan.Entrances);
        Assert.Equal("quality", entrance.LocationQualityTagId);
        Assert.Equal("open", Assert.Single(entrance.Tags).TagTypeId);
    }

    [Fact]
    public void CaseVariantsCreateOneTagUsingFirstSpellingAndCollapseDuplicateInput()
    {
        var records = new[]
        {
            EntranceRecord(value => value.EntranceStatuses = "Open, open"),
            EntranceRecord(value =>
            {
                value.EntranceName = "Other";
                value.IsPrimaryEntrance = false;
                value.EntranceStatuses = "OPEN";
            })
        };

        var plan = Plan(records);

        var status = Assert.Single(plan.TagCreations, tag => tag.Key == TagTypeKeyConstant.EntranceStatus);
        Assert.Equal("Open", status.Name);
        Assert.All(plan.Entrances, entrance => Assert.Equal(status.Id, Assert.Single(entrance.Tags).TagTypeId));
    }

    [Fact]
    public void ForeignCustomTagExcludedByRepositoryProducesLocalCreationIntent()
    {
        var plan = Plan([EntranceRecord(value => value.EntranceStatuses = "Open")]);
        Assert.Equal("Open", Assert.Single(plan.TagCreations,
            tag => tag.Key == TagTypeKeyConstant.EntranceStatus).Name);
    }

    [Fact]
    public void MultiValueTagsMapToIndependentRoles()
    {
        var plan = Plan([EntranceRecord(value =>
        {
            value.EntranceStatuses = "Open";
            value.EntranceHydrology = "Wet";
            value.FieldIndication = "Sink";
            value.ReportedByNames = "Alice, Bob";
        })]);
        var entrance = Assert.Single(plan.Entrances);

        Assert.Equal(5, entrance.Tags.Count);
        Assert.Equal(4, entrance.Tags.Select(tag => tag.Role).Distinct().Count());
        Assert.Equal(["Alice", "Bob"], entrance.ReportedByNames);
    }

    [Fact]
    public void TargetCaveLookupAndExpectedConcurrencyValuesAreProjected()
    {
        var plan = Plan([EntranceRecord()], State(version: 17, revision: "revision"));

        var target = Assert.Single(plan.Targets).Value;
        Assert.Equal("cave", target.CaveId);
        Assert.Equal((uint)17, target.Version);
        Assert.Equal("revision", target.CurrentRevisionId);
    }

    [Fact]
    public void UnknownTargetCaveIsRejected() =>
        Assert.Throws<ApiException>(() => Plan([EntranceRecord()], State(caves: [])));

    [Fact]
    public void ZeroPrimaryEntranceIsRejected() =>
        AssertInvalid([EntranceRecord(value => value.IsPrimaryEntrance = false)]);

    [Fact]
    public void MultipleImportedPrimaryEntrancesAreRejected() =>
        AssertInvalid([EntranceRecord(), EntranceRecord(value => value.EntranceName = "Other")]);

    [Fact]
    public void AppendRejectsImportedPrimaryWhenExistingPrimaryExists()
    {
        var state = State(existingCount: 1, existingPrimaryCount: 1);
        Assert.Throws<ApiException>(() => Plan([EntranceRecord()], state));
    }

    [Fact]
    public void AppendAllowsNonPrimaryWhenExistingPrimaryExists()
    {
        var state = State(existingCount: 1, existingPrimaryCount: 1);
        var plan = Plan([EntranceRecord(value => value.IsPrimaryEntrance = false)], state);

        Assert.False(Assert.Single(plan.Entrances).IsPrimary);
        Assert.Equal(1, Assert.Single(plan.CreatePreview()).EntranceCountChange);
    }

    [Fact]
    public void SyncReplacementIgnoresExistingPrimaryAndReportsFinalCountDelta()
    {
        var state = State(existingCount: 3, existingPrimaryCount: 1);
        var plan = Plan([EntranceRecord()], state, sync: true);

        Assert.True(plan.SyncExisting);
        Assert.Equal(-2, Assert.Single(plan.CreatePreview()).EntranceCountChange);
    }

    [Fact]
    public void AppendPreviewReportsImportedCountIncrease()
    {
        var state = State(existingCount: 2, existingPrimaryCount: 0);
        var plan = Plan([EntranceRecord()], state);

        Assert.Equal(1, Assert.Single(plan.CreatePreview()).EntranceCountChange);
    }

    [Fact]
    public void CancellationIsObservedDuringPlanning()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            new EntranceImportPlanner().Plan([EntranceRecord()], State(), false, cancellation.Token));
    }

    private static EntranceImportPlan Plan(IReadOnlyList<EntranceCsvModel> records,
        EntranceImportPlanningState? state = null, bool sync = false) =>
        new EntranceImportPlanner().Plan(records, state ?? State(), sync);

    private static void AssertInvalid(IReadOnlyList<EntranceCsvModel> records) =>
        Assert.Throws<ApiException>(() => Plan(records));

    private static EntranceCsvModel EntranceRecord(Action<EntranceCsvModel>? configure = null)
    {
        var record = new EntranceCsvModel
        {
            CountyCode = "A01",
            CountyCaveNumber = "7",
            EntranceName = "Main",
            DecimalLatitude = 35,
            DecimalLongitude = -86,
            EntranceElevationFt = 500,
            LocationQuality = "Survey Grade",
            IsPrimaryEntrance = true
        };
        configure?.Invoke(record);
        return record;
    }

    private static EntranceImportPlanningState State(
        IReadOnlyList<ImportTagLookup>? tags = null,
        IReadOnlyList<EntranceImportCaveLookup>? caves = null,
        int existingCount = 0,
        int existingPrimaryCount = 0,
        uint version = 3,
        string? revision = "revision")
    {
        var quality = Tag("quality", TagTypeKeyConstant.LocationQuality, "Survey Grade", null, isDefault: true);
        var caveRows = caves ?? [new EntranceImportCaveLookup("cave", "Pure Cave", "A01", 7, version, revision)];
        return new EntranceImportPlanningState(
            AccountId,
            tags is null ? [quality] : tags.Concat([quality]).DistinctBy(tag => tag.Id).ToList(),
            caveRows,
            existingCount == 0 ? new Dictionary<string, int>() : new Dictionary<string, int> { ["cave"] = existingCount },
            existingPrimaryCount == 0 ? new Dictionary<string, int>() : new Dictionary<string, int> { ["cave"] = existingPrimaryCount });
    }

    private static ImportTagLookup Tag(string id, string key, string name, string? accountId,
        bool isDefault = false) => new(id, key, name, accountId, isDefault, false);

    private const string AccountId = "account";
}
