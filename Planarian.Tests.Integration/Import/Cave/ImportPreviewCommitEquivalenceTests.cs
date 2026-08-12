using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Modules.Account.Import.Models;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class ImportPreviewCommitEquivalenceTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public Task CaveInsertPreviewMatchesCommit() => VerifyCaveAsync(
        "insert",
        syncExisting: false,
        "Committed Cave,Committed County,COM,77,AA,Alt,Needs Mapping,Mapper,300,55,22,3,Limestone,Mississippian,Plateau,None,Bats,2026-08-01,Reporter,false,Interesting,Committed narrative",
        CaveImportAction.Insert);

    [Fact]
    public Task CaveUpdatePreviewMatchesCommit() => VerifyCaveAsync(
        "update",
        syncExisting: true,
        "Updated Cave,County A,A01,1,AA,Alt,Map,Mapper,300,55,22,3,Limestone,Mississippian,Plateau,None,Bats,2026-08-01,Reporter,true,Interesting,Updated narrative",
        CaveImportAction.Update);

    [Fact]
    public async Task CaveNoChangePreviewAndCommitAreEquivalent()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CaveNoChangePreviewAndCommitAreEquivalent));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var import = new CaveImportTestHarness(db, db.RequestUser);
        var csv = ImportDryRunIntegrationTests.CaveHeader +
            "\nCave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,\n";
        var before = await new CavePublishedSnapshotRepository(db, db.RequestUser).BuildAsync(tenant.CaveId);
        var revisionBefore = (await db.Caves.IgnoreQueryFilters()
            .SingleAsync(cave => cave.Id == tenant.CaveId)).CurrentRevisionId;

        // Act
        var plan = await import.PlanCsvAsync(csv, syncExisting: true);
        await import.ExecuteAsync(plan, "no-change.csv");
        db.ChangeTracker.Clear();
        var after = await new CavePublishedSnapshotRepository(db, db.RequestUser).BuildAsync(tenant.CaveId);

        // Assert
        Assert.Equal(CaveImportAction.NoChange, Assert.Single(plan.Caves).Action);
        Assert.Equal("no change", Assert.Single(plan.CreatePreview(omitNoChange: false)).Action);
        Assert.Empty(plan.CreatePreview(omitNoChange: true));
        Assert.Equal(CaveSnapshotJson.Serialize(before), CaveSnapshotJson.Serialize(after));
        Assert.Equal(revisionBefore, (await db.Caves.IgnoreQueryFilters()
            .SingleAsync(cave => cave.Id == tenant.CaveId)).CurrentRevisionId);
    }

    [Fact]
    public async Task CaveSyncDeletionPreviewMatchesCommit()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(CaveSyncDeletionPreviewMatchesCommit));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var import = new CaveImportTestHarness(db, db.RequestUser);
        var csv = ImportDryRunIntegrationTests.CaveHeader +
            "\nReplacement,Replacement County,REP,2,AA,,,,10,2,1,1,,,,,,,,false,,Replacement\n";
        var revisionsBefore = await db.CaveRevisions.CountAsync(revision => revision.CaveId == tenant.CaveId);

        // Act
        var plan = await import.PlanCsvAsync(csv, syncExisting: true);
        await import.ExecuteAsync(plan, "delete.csv");

        // Assert
        Assert.Equal(tenant.CaveId, Assert.Single(plan.Deletions).CaveId);
        var preview = Assert.Single(plan.CreatePreview(omitNoChange: true), row => row.Action == "delete");
        Assert.Equal(("Cave A", "A01", 1),
            (preview.CaveName, preview.CountyCode, preview.CountyCaveNumber));
        Assert.False(await db.Caves.IgnoreQueryFilters().AnyAsync(cave => cave.Id == tenant.CaveId));
        var revisions = await db.CaveRevisions.Where(revision => revision.CaveId == tenant.CaveId)
            .OrderByDescending(revision => revision.CreatedOn).ToListAsync();
        Assert.Equal(revisionsBefore + 1, revisions.Count);
        Assert.Equal(CaveRevisionOperation.Delete, revisions[0].Operation);
    }

    [Fact]
    public Task EntranceInsertPreviewMatchesCommit() =>
        VerifyEntranceAsync("insert", syncExisting: false, seedExisting: false, importPrimary: true);

    [Fact]
    public Task EntranceNonSyncAppendPreviewMatchesCommit() =>
        VerifyEntranceAsync("append", syncExisting: false, seedExisting: true, importPrimary: false);

    [Fact]
    public Task EntranceSyncReplacementPreviewMatchesCommit() =>
        VerifyEntranceAsync("replace", syncExisting: true, seedExisting: true, importPrimary: true);

    private async Task VerifyCaveAsync(
        string scenario, bool syncExisting, string row, CaveImportAction expectedAction)
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync($"preview_cave_{scenario}");
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var import = new CaveImportTestHarness(db, db.RequestUser);

        // Act
        var plan = await import.PlanCsvAsync(
            ImportDryRunIntegrationTests.CaveHeader + '\n' + row + '\n', syncExisting);
        var planned = Assert.Single(plan.Caves);
        var preview = Assert.Single(plan.CreatePreview(omitNoChange: true));
        await import.ExecuteAsync(plan, $"{scenario}.csv");
        db.ChangeTracker.Clear();

        // Assert
        Assert.Equal(expectedAction, planned.Action);
        var snapshot = await new CavePublishedSnapshotRepository(db, db.RequestUser).BuildAsync(planned.Id);
        AssertCavePreview(preview, snapshot);
        var cave = await db.Caves.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == planned.Id);
        Assert.NotNull(cave.CurrentRevisionId);
        Assert.True(await db.CaveRevisions.AnyAsync(
            revision => revision.Id == cave.CurrentRevisionId && revision.Source == CaveRevisionSource.Import));
        Assert.True(await db.CaveImportBatches.AnyAsync(batch => batch.SourceFileName == $"{scenario}.csv"));
    }

    private async Task VerifyEntranceAsync(
        string scenario, bool syncExisting, bool seedExisting, bool importPrimary)
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync($"preview_entrance_{scenario}");
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        if (seedExisting)
            await EntranceTestData.AddEntranceAsync(database, tenant, "existing00", isPrimary: true);

        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var import = new EntranceImportTestHarness(db, db.RequestUser);
        var csv = ImportDryRunIntegrationTests.EntranceHeader +
            $"\nImported,A01,1,{importPrimary.ToString().ToLowerInvariant()},35.123,-86.456,612,Survey Grade,20,Open,Wet,Sink,2026-08-01,Surveyor,Entrance description\n";

        // Act
        var plan = await import.PlanCsvAsync(csv, syncExisting);
        var planned = Assert.Single(plan.Entrances);
        var preview = Assert.Single(plan.CreatePreview());
        await import.ExecuteAsync(plan, $"{scenario}.csv");
        db.ChangeTracker.Clear();

        // Assert
        var snapshot = await new CavePublishedSnapshotRepository(db, db.RequestUser).BuildAsync(tenant.CaveId);
        var committed = Assert.Single(snapshot.Entrances, entrance => entrance.Id == planned.Id);
        AssertEntrancePreview(preview, committed);
        Assert.Equal(preview.EntranceCountChange, snapshot.Entrances.Count - (seedExisting ? 1 : 0));
        if (syncExisting)
            Assert.DoesNotContain(snapshot.Entrances, entrance => entrance.Id == "existing00");

        var cave = await db.Caves.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == tenant.CaveId);
        Assert.NotNull(cave.CurrentRevisionId);
        Assert.True(await db.CaveRevisions.AnyAsync(
            revision => revision.Id == cave.CurrentRevisionId && revision.Source == CaveRevisionSource.Import));
    }

    private static void AssertCavePreview(CaveDryRunRecord preview, CavePublishedSnapshotV1 snapshot)
    {
        Assert.Equal(preview.CaveName, snapshot.Name);
        Assert.Equal(preview.CountyCode, snapshot.County.DisplayIdAtRevision);
        Assert.Equal(preview.CountyName, snapshot.County.NameAtRevision);
        Assert.Equal(preview.CountyCaveNumber, snapshot.CountyNumber);
        Assert.Equal(preview.State, snapshot.State.AbbreviationAtRevision);
        Assert.Equal(preview.AlternateNames, snapshot.AlternateNames);
        Assert.Equal(preview.CaveLengthFeet, snapshot.LengthFeet);
        Assert.Equal(preview.CaveDepthFeet, snapshot.DepthFeet);
        Assert.Equal(preview.MaxPitDepthFeet, snapshot.MaxPitDepthFeet);
        Assert.Equal(preview.NumberOfPits, snapshot.NumberOfPits);
        Assert.Equal(preview.ReportedOnDate, snapshot.ReportedOn);
        Assert.Equal(preview.IsArchived, snapshot.IsArchived);
        Assert.Equal(preview.Narrative, snapshot.Narrative);
        AssertRole(preview.Geology, snapshot, SnapshotTagRole.Geology);
        AssertRole(preview.GeologicAges, snapshot, SnapshotTagRole.GeologicAge);
        AssertRole(preview.MapStatuses, snapshot, SnapshotTagRole.MapStatus);
        AssertRole(preview.PhysiographicProvinces, snapshot, SnapshotTagRole.PhysiographicProvince);
        AssertRole(preview.Archeology, snapshot, SnapshotTagRole.Archeology);
        AssertRole(preview.Biology, snapshot, SnapshotTagRole.Biology);
        AssertRole(preview.OtherTags, snapshot, SnapshotTagRole.CaveOther);
        AssertRole(preview.CartographerNames, snapshot, SnapshotTagRole.Cartographer);
        AssertRole(preview.ReportedByNames, snapshot, SnapshotTagRole.CaveReportedBy);
    }

    private static void AssertEntrancePreview(EntranceDryRun preview, CaveEntranceSnapshotV1 entrance)
    {
        Assert.Equal(preview.EntranceName, entrance.Name);
        Assert.Equal(preview.IsPrimaryEntrance, entrance.IsPrimary);
        Assert.Equal(preview.EntranceDescription, entrance.Description);
        Assert.Equal(preview.DecimalLatitude, entrance.Latitude!.Value, 6);
        Assert.Equal(preview.DecimalLongitude, entrance.Longitude!.Value, 6);
        Assert.Equal(preview.EntranceElevationFt, entrance.Elevation!.Value, 6);
        Assert.Equal(4326, entrance.Srid);
        Assert.Equal(preview.LocationQuality, entrance.LocationQualityNameAtRevision);
        Assert.Equal(preview.EntrancePitDepth, entrance.PitDepthFeet);
        Assert.Equal(preview.ReportedOnDate, entrance.ReportedOn);
        AssertRole(preview.EntranceStatuses, entrance, SnapshotTagRole.EntranceStatus);
        AssertRole(preview.EntranceHydrology, entrance, SnapshotTagRole.EntranceHydrology);
        AssertRole(preview.FieldIndication, entrance, SnapshotTagRole.FieldIndication);
        AssertRole(preview.ReportedByNames, entrance, SnapshotTagRole.EntranceReportedBy);
    }

    private static void AssertRole(
        IEnumerable<string> expected, CavePublishedSnapshotV1 snapshot, SnapshotTagRole role) =>
        Assert.Equal(expected.Order(), snapshot.Tags.Where(tag => tag.Role == role)
            .Select(tag => tag.NameAtRevision).Order());

    private static void AssertRole(
        IEnumerable<string> expected, CaveEntranceSnapshotV1 entrance, SnapshotTagRole role) =>
        Assert.Equal(expected.Order(), entrance.Tags.Where(tag => tag.Role == role)
            .Select(tag => tag.NameAtRevision).Order());
}
