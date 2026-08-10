using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class EntranceImportCompatibilityTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ExistingLocationQualityAndStatusAreReusedAndPersistCanonicalNames()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ExistingLocationQualityAndStatusAreReusedAndPersistCanonicalNames));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var quality = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade");
        var status = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.EntranceStatus, "Open");
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var import = new EntranceImportTestHarness(db, db.RequestUser);
        var csv = EntranceCsv("Case,A01,1,true,35,-86,500,  survey grade  ,0,\"open, OPEN\",,,,,");

        // Act
        var plan = await import.PlanCsvAsync(csv, syncExisting: false);
        await import.ExecuteAsync(plan, "case.csv");
        db.ChangeTracker.Clear();

        // Assert
        Assert.Empty(plan.TagCreations);
        var planned = Assert.Single(plan.Entrances);
        Assert.Equal((quality.Id, "Survey Grade"),
            (planned.LocationQualityTagId, planned.LocationQualityName));
        Assert.Equal(status.Id, Assert.Single(planned.Tags).TagTypeId);
        var snapshot = await new CavePublishedSnapshotRepository(db, db.RequestUser).BuildAsync(tenant.CaveId);
        var entrance = Assert.Single(snapshot.Entrances);
        Assert.Equal((quality.Id, "Survey Grade"),
            (entrance.LocationQualityTagId, entrance.LocationQualityNameAtRevision));
        Assert.Contains(entrance.Tags, tag => tag.TagTypeId == status.Id && tag.NameAtRevision == "Open");
    }

    [Fact]
    public async Task CaseVariantsAcrossRowsCreateAndPersistOneEntranceTag()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(CaseVariantsAcrossRowsCreateAndPersistOneEntranceTag));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var import = new EntranceImportTestHarness(db, db.RequestUser);
        var csv = EntranceCsv(
            "Primary,A01,1,true,35,-86,500,Survey Grade,0,Open,,,,,,",
            "Other,A01,1,false,35.1,-86.1,510,survey grade,0,OPEN,,,,,,");

        // Act
        var plan = await import.PlanCsvAsync(csv, syncExisting: false);
        await import.ExecuteAsync(plan, "variants.csv");

        // Assert
        var status = Assert.Single(plan.TagCreations, tag => tag.Key == TagTypeKeyConstant.EntranceStatus);
        Assert.Equal("Open", status.Name);
        Assert.All(plan.Entrances, entrance => Assert.Equal(status.Id, Assert.Single(entrance.Tags).TagTypeId));
        Assert.Single(await db.TagTypes.Where(tag => tag.AccountId == tenant.AccountId &&
            tag.Key == TagTypeKeyConstant.EntranceStatus && tag.Name.ToLower() == "open").ToListAsync());
    }

    [Fact]
    public async Task ForeignCustomEntranceTagIsNotAssociatedAndLocalIntentPersists()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(ForeignCustomEntranceTagIsNotAssociatedAndLocalIntentPersists));
        var accountA = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var accountB = await TestDataBuilder.CreatePublishedCaveAsync(database, 'b');
        var foreign = await TestDataBuilder.AddTagAsync(database, accountB.AccountId,
            TagTypeKeyConstant.EntranceStatus, "Open");
        await using var db = database.CreateDbContext("a", accountA.AccountId);
        var import = new EntranceImportTestHarness(db, db.RequestUser);

        // Act
        var plan = await import.PlanCsvAsync(
            EntranceCsv("Case,A01,1,true,35,-86,500,Survey Grade,0,open,,,,,,"),
            syncExisting: false);
        await import.ExecuteAsync(plan, "foreign.csv");

        // Assert
        var local = Assert.Single(plan.TagCreations, tag => tag.Key == TagTypeKeyConstant.EntranceStatus);
        Assert.NotEqual(foreign.Id, local.Id);
        Assert.Contains(Assert.Single(plan.Entrances).Tags, tag => tag.TagTypeId == local.Id);
        Assert.False(await db.EntranceStatusTags.AnyAsync(tag => tag.TagTypeId == foreign.Id));
    }

    [Fact]
    public async Task FullInsertPersistsScalarsTagsAndPostgisXyz()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(FullInsertPersistsScalarsTagsAndPostgisXyz));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var import = new EntranceImportTestHarness(db, db.RequestUser);
        var csv = EntranceCsv("Main,A01,1,true,35.123456,-86.654321,612.5,Survey Grade,22.5," +
                              "Open,Wet,Sink,2026-08-01,Surveyor,Full description");

        // Act
        var plan = await import.PlanCsvAsync(csv, syncExisting: false);
        var planned = Assert.Single(plan.Entrances);
        await import.ExecuteAsync(plan, "full.csv");
        var snapshot = await new CavePublishedSnapshotRepository(db, db.RequestUser).BuildAsync(tenant.CaveId);

        // Assert
        var entrance = Assert.Single(snapshot.Entrances);
        Assert.Equal((planned.Id, "Main", true), (entrance.Id, entrance.Name, entrance.IsPrimary));
        Assert.Equal(35.123456, entrance.Latitude!.Value, 6);
        Assert.Equal(-86.654321, entrance.Longitude!.Value, 6);
        Assert.Equal(612.5, entrance.Elevation!.Value, 6);
        Assert.Equal(4326, entrance.Srid);
        Assert.Equal(22.5, entrance.PitDepthFeet);
        Assert.Equal("Survey Grade", entrance.LocationQualityNameAtRevision);
        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), entrance.ReportedOn);
        Assert.Equal("Full description", entrance.Description);
        AssertTags(entrance.Tags,
            (SnapshotTagRole.EntranceStatus, "Open"),
            (SnapshotTagRole.EntranceHydrology, "Wet"),
            (SnapshotTagRole.FieldIndication, "Sink"),
            (SnapshotTagRole.EntranceReportedBy, "Surveyor"));
    }

    [Fact]
    public async Task SyncReplacementPersistsOnePrimaryAndPublishesRevision()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(SyncReplacementPersistsOnePrimaryAndPublishesRevision));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await TestDataBuilder.AddEntranceAsync(database, tenant, "existing00");
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var priorRevision = (await db.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId))
            .CurrentRevisionId;
        var import = new EntranceImportTestHarness(db, db.RequestUser);
        var csv = EntranceCsv("Replacement,A01,1,true,35.2,-86.2,520,Survey Grade,7.5," +
                              "Open,Wet,Sink,2026-08-02,Surveyor,Replacement description");

        // Act
        var plan = await import.PlanCsvAsync(csv, syncExisting: true);
        var planned = Assert.Single(plan.Entrances);
        await import.ExecuteAsync(plan, "replace.csv");
        db.ChangeTracker.Clear();

        // Assert
        Assert.Equal(0, Assert.Single(plan.CreatePreview()).EntranceCountChange);
        var snapshot = await new CavePublishedSnapshotRepository(db, db.RequestUser).BuildAsync(tenant.CaveId);
        var entrance = Assert.Single(snapshot.Entrances);
        Assert.Equal(planned.Id, entrance.Id);
        Assert.DoesNotContain(snapshot.Entrances, row => row.Id == "existing00");
        Assert.True(entrance.IsPrimary);
        var cave = await db.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.NotEqual(priorRevision, cave.CurrentRevisionId);
        var revision = await db.CaveRevisions.SingleAsync(row => row.Id == cave.CurrentRevisionId);
        Assert.Equal((CaveRevisionSource.Import, CaveRevisionOperation.Update),
            (revision.Source, revision.Operation));
        Assert.Equal(CaveSnapshotJson.Serialize(snapshot),
            CaveSnapshotJson.Serialize(CaveSnapshotJson.Deserialize(revision.SnapshotJson,
                revision.SnapshotSchemaVersion)));
    }

    [Fact]
    public async Task SyncPreservesUnrelatedCaveAggregateAndRevisionPointer()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(SyncPreservesUnrelatedCaveAggregateAndRevisionPointer));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await TestDataBuilder.AddEntranceAsync(database, tenant, "existing00");
        var accountCounty = new AccountCountyTestData(tenant.AccountId, tenant.StateId, tenant.StateName,
            tenant.StateAbbreviation, tenant.CountyId, tenant.CountyName, tenant.CountyDisplayId);
        var unrelated = await TestDataBuilder.AddCaveAsync(database, accountCounty, "secondcav0", "Second", 2);
        var unrelatedPublished = await TestDataBuilder.PublishBaselineRevisionAsync(database, unrelated, "revision02");
        var secondQuality = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Second Quality");
        await using (var seed = database.CreateDbContext("a", tenant.AccountId))
        {
            seed.Entrances.Add(new Entrance
            {
                Id = "secondent0",
                CaveId = unrelated.CaveId,
                LocationQualityTagId = secondQuality.Id,
                Name = "Unrelated",
                Description = "Preserve me",
                ReportedOn = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                PitDepthFeet = 11,
                IsPrimary = true,
                Location = new Point(new CoordinateZ(-86, 35, 500)) { SRID = 4326 }
            });
            await seed.SaveChangesAsync();
        }
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var snapshots = new CavePublishedSnapshotRepository(db, db.RequestUser);
        var before = await snapshots.BuildAsync(unrelated.CaveId);
        var import = new EntranceImportTestHarness(db, db.RequestUser);

        // Act
        var plan = await import.PlanCsvAsync(
            EntranceCsv("Replacement,A01,1,true,35.2,-86.2,520,Survey Grade,0,,,,,,,"),
            syncExisting: true);
        await import.ExecuteAsync(plan, "sync.csv");
        db.ChangeTracker.Clear();

        // Assert
        var after = await snapshots.BuildAsync(unrelated.CaveId);
        Assert.Equal(CaveSnapshotJson.Serialize(before), CaveSnapshotJson.Serialize(after));
        var persisted = await db.Caves.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.Id == unrelated.CaveId);
        Assert.Equal(unrelatedPublished.RevisionId, persisted.CurrentRevisionId);
    }

    [Fact]
    public async Task CommitAbortsIfTargetCaveChangesAfterPlanning()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(CommitAbortsIfTargetCaveChangesAfterPlanning));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        EntranceImportPlan plan;
        await using (var planningDb = database.CreateDbContext("a", tenant.AccountId))
        {
            var import = new EntranceImportTestHarness(planningDb, planningDb.RequestUser);
            plan = await import.PlanCsvAsync(
                EntranceCsv("Planned,A01,1,true,35,-86,500,Survey Grade,0,,,,,,,"),
                syncExisting: false);
        }
        await using (var concurrent = database.CreateDbContext("a", tenant.AccountId))
        {
            var cave = await concurrent.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
            cave.Name = "Concurrent";
            await concurrent.SaveChangesAsync();
        }
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var execution = new EntranceImportTestHarness(db, db.RequestUser);

        // Act / Assert
        await Assert.ThrowsAsync<CaveRevisionConflictException>(() => execution.ExecuteAsync(plan, "conflict.csv"));
        Assert.Empty(await db.Entrances.IgnoreQueryFilters().Where(row => row.CaveId == tenant.CaveId).ToListAsync());
    }

    private static string EntranceCsv(params string[] rows) =>
        ImportDryRunIntegrationTests.EntranceHeader + "\n" + string.Join("\n", rows) + "\n";

    private static void AssertTags(IReadOnlyList<SnapshotTagReference> actual,
        params (SnapshotTagRole Role, string Name)[] expected) =>
        Assert.Equal(
            expected.OrderBy(item => item.Role).ThenBy(item => item.Name)
                .Select(item => $"{item.Role}:{item.Name}"),
            actual.OrderBy(item => item.Role).ThenBy(item => item.NameAtRevision)
                .Select(item => $"{item.Role}:{item.NameAtRevision}"));
}
