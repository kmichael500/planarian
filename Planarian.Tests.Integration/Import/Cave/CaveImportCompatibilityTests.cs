using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class CaveImportCompatibilityTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ExistingAccountTagIsReusedCaseInsensitivelyAndPersistedWithCanonicalName()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ExistingAccountTagIsReusedCaseInsensitivelyAndPersistedWithCanonicalName));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var existing = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.Geology, "Foo");
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var import = new CaveImportTestHarness(db, db.RequestUser);
        var csv = CaveCsv("Case,County A,A01,20,AA,,,,,,,1,  foo  ,,,,,,,false,,");

        // Act
        var plan = await import.PlanCsvAsync(csv, syncExisting: false);
        await import.ExecuteAsync(plan, "case.csv");
        db.ChangeTracker.Clear();

        // Assert
        Assert.Empty(plan.TagCreations);
        var planned = Assert.Single(plan.Caves);
        Assert.Equal(["Foo"], planned.Geology);
        Assert.Contains(planned.Tags, tag => tag.TagTypeId == existing.Id);
        Assert.Equal(["Foo"], Assert.Single(plan.CreatePreview(false)).Geology);
        Assert.Single(await db.TagTypes.Where(tag => tag.AccountId == tenant.AccountId &&
            tag.Key == TagTypeKeyConstant.Geology && tag.Name.ToLower() == "foo").ToListAsync());
        var snapshot = await new CavePublishedSnapshotRepository(db, db.RequestUser).BuildAsync(planned.Id);
        Assert.Contains(snapshot.Tags, tag => tag.TagTypeId == existing.Id && tag.NameAtRevision == "Foo");
    }

    [Fact]
    public async Task CaseVariantsCreateAndPersistOneTagUsingFirstSpelling()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(CaseVariantsCreateAndPersistOneTagUsingFirstSpelling));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var import = new CaveImportTestHarness(db, db.RequestUser);
        var csv = CaveCsv(
            "First,County A,A01,20,AA,,,,,,,1,\"Limestone, limestone\",,,,,,,false,,",
            "Second,County A,A01,21,AA,,,,,,,1,LIMESTONE,,,,,,,false,,");

        // Act
        var plan = await import.PlanCsvAsync(csv, syncExisting: false);
        await import.ExecuteAsync(plan, "variants.csv");

        // Assert
        var creation = Assert.Single(plan.TagCreations, tag => tag.Key == TagTypeKeyConstant.Geology);
        Assert.Equal("Limestone", creation.Name);
        Assert.All(plan.Caves, cave => Assert.Equal([creation.Id], cave.Tags
            .Where(tag => tag.Role == CaveImportTagRole.Geology).Select(tag => tag.TagTypeId)));
        Assert.Single(await db.TagTypes.Where(tag => tag.AccountId == tenant.AccountId &&
            tag.Key == TagTypeKeyConstant.Geology && tag.Name.ToLower() == "limestone").ToListAsync());
    }

    [Fact]
    public async Task PreexistingCaseOnlyDuplicateTagsAreNotMutatedDuringPersistence()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(PreexistingCaseOnlyDuplicateTagsAreNotMutatedDuringPersistence));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await TestDataBuilder.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.Geology, "Foo", "zzzzzzzzzz");
        await TestDataBuilder.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.Geology, "foo", "aaaaaaaaaa");
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var import = new CaveImportTestHarness(db, db.RequestUser);

        // Act
        var plan = await import.PlanCsvAsync(
            CaveCsv("Legacy,County A,A01,20,AA,,,,,,,1,FOO,,,,,,,false,,"),
            syncExisting: false);
        await import.ExecuteAsync(plan, "legacy.csv");
        db.ChangeTracker.Clear();

        // Assert
        Assert.Empty(plan.TagCreations);
        Assert.Equal("aaaaaaaaaa", Assert.Single(Assert.Single(plan.Caves).Tags).TagTypeId);
        var tags = await db.TagTypes.Where(tag => tag.AccountId == tenant.AccountId &&
                tag.Key == TagTypeKeyConstant.Geology && tag.Name.ToLower() == "foo")
            .OrderBy(tag => tag.Id).Select(tag => $"{tag.Id}:{tag.Name}").ToListAsync();
        Assert.Equal(["aaaaaaaaaa:foo", "zzzzzzzzzz:Foo"], tags);
    }

    [Fact]
    public async Task ForeignCustomTagIsNotAssociatedAndLocalIntentPersists()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(ForeignCustomTagIsNotAssociatedAndLocalIntentPersists));
        var accountA = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var accountB = await TestDataBuilder.CreatePublishedCaveAsync(database, 'b');
        var foreign = await TestDataBuilder.AddTagAsync(database, accountB.AccountId,
            TagTypeKeyConstant.Geology, "Foreign Geo");
        await using var db = database.CreateDbContext("a", accountA.AccountId);
        var import = new CaveImportTestHarness(db, db.RequestUser);

        // Act
        var plan = await import.PlanCsvAsync(
            CaveCsv("Tags,County A,A01,22,AA,,,,,,,1,foreign geo,,,,,,,false,,"),
            syncExisting: false);
        await import.ExecuteAsync(plan, "foreign.csv");

        // Assert
        Assert.DoesNotContain(Assert.Single(plan.Caves).Tags, tag => tag.TagTypeId == foreign.Id);
        Assert.Contains(plan.TagCreations, tag => tag.Name == "foreign geo");
        Assert.False(await db.GeologyTags.AnyAsync(tag => tag.TagTypeId == foreign.Id));
        Assert.Single(await db.TagTypes.Where(tag => tag.AccountId == accountA.AccountId &&
            tag.Key == TagTypeKeyConstant.Geology && tag.Name.ToLower() == "foreign geo").ToListAsync());
    }

    [Fact]
    public async Task FullInsertPersistsScalarsTagsAndImportRevision()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(FullInsertPersistsScalarsTagsAndImportRevision));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var import = new CaveImportTestHarness(db, db.RequestUser);
        var csv = CaveCsv("Full,County A,A01,30,AA,Alt,Map,Cart,123.5,45.5,20.5,4,Limestone," +
                          "Mississippian,Plateau,Artifact,Bats,2026-08-01,Reporter,true,Interesting,Full narrative");

        // Act
        var plan = await import.PlanCsvAsync(csv, syncExisting: false);
        var planned = Assert.Single(plan.Caves);
        await import.ExecuteAsync(plan, "full.csv");
        var snapshot = await new CavePublishedSnapshotRepository(db, db.RequestUser).BuildAsync(planned.Id);

        // Assert
        Assert.Equal(9, plan.TagCreations.Count);
        Assert.Equal((planned.Id, tenant.AccountId, tenant.StateId, tenant.CountyId, 30, "Full"),
            (snapshot.CaveId, snapshot.AccountId, snapshot.State.Id, snapshot.County.Id,
                snapshot.CountyNumber, snapshot.Name));
        Assert.Equal(["Alt"], snapshot.AlternateNames);
        Assert.Equal((123.5, 45.5, 20.5, 4),
            (snapshot.LengthFeet, snapshot.DepthFeet, snapshot.MaxPitDepthFeet, snapshot.NumberOfPits));
        Assert.Equal("Full narrative", snapshot.Narrative);
        Assert.True(snapshot.IsArchived);
        AssertTags(snapshot.Tags,
            (SnapshotTagRole.Geology, "Limestone"),
            (SnapshotTagRole.GeologicAge, "Mississippian"),
            (SnapshotTagRole.MapStatus, "Map"),
            (SnapshotTagRole.PhysiographicProvince, "Plateau"),
            (SnapshotTagRole.Archeology, "Artifact"),
            (SnapshotTagRole.Biology, "Bats"),
            (SnapshotTagRole.CaveOther, "Interesting"),
            (SnapshotTagRole.Cartographer, "Cart"),
            (SnapshotTagRole.CaveReportedBy, "Reporter"));
        var cave = await db.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == planned.Id);
        var revision = await db.CaveRevisions.SingleAsync(row => row.Id == cave.CurrentRevisionId);
        Assert.Equal((CaveRevisionSource.Import, CaveRevisionOperation.Create),
            (revision.Source, revision.Operation));
        Assert.Equal(CaveSnapshotJson.Serialize(snapshot),
            CaveSnapshotJson.Serialize(CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion)));
    }

    [Fact]
    public async Task SyncUpdatePreservesEntranceAndFile()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(SyncUpdatePreservesEntranceAndFile));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await TestDataBuilder.AddEntranceAsync(database, tenant, "preserved0");
        var testFile = await TestDataBuilder.AddFileAsync(database, tenant, associateWithCave: true);
        await using (var seed = database.CreateDbContext("a", tenant.AccountId))
        {
            var file = await seed.Files.SingleAsync(row => row.Id == testFile.FileId);
            file.DisplayName = "Preserved display";
            file.ExpiresOn = new DateTime(2027, 1, 2, 0, 0, 0, DateTimeKind.Utc);
            await seed.SaveChangesAsync();
        }
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var snapshots = new CavePublishedSnapshotRepository(db, db.RequestUser);
        var before = await snapshots.BuildAsync(tenant.CaveId);
        var fileBefore = await FileStateAsync(db, testFile.FileId);
        var import = new CaveImportTestHarness(db, db.RequestUser);

        // Act
        var plan = await import.PlanCsvAsync(
            CaveCsv("Updated,County A,A01,1,AA,,,,200,50,12,2,,,,,,2026-08-01,,false,,Updated"),
            syncExisting: true);
        await import.ExecuteAsync(plan, "update.csv");
        db.ChangeTracker.Clear();

        // Assert
        var after = await snapshots.BuildAsync(tenant.CaveId);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Entrances),
            System.Text.Json.JsonSerializer.Serialize(after.Entrances));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Files),
            System.Text.Json.JsonSerializer.Serialize(after.Files));
        Assert.Equal(fileBefore, await FileStateAsync(db, testFile.FileId));
    }

    [Fact]
    public async Task SyncNoChangeWritesNoCaveTagOrRevisionRows()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(SyncNoChangeWritesNoCaveTagOrRevisionRows));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var sql = new SqlTimingInterceptor();
        await using var db = CreateObservedContext(database, tenant, sql);
        var import = new CaveImportTestHarness(db, db.RequestUser);
        var plan = await import.PlanCsvAsync(
            CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"),
            syncExisting: true);
        var caveBefore = await db.Caves.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == tenant.CaveId);
        var revisionIdsBefore = await db.CaveRevisions.Where(row => row.CaveId == tenant.CaveId)
            .OrderBy(row => row.Id).Select(row => row.Id).ToListAsync();
        sql.Reset();

        // Act
        await import.ExecuteAsync(plan, "no-change.csv");
        db.ChangeTracker.Clear();

        // Assert
        Assert.Equal(CaveImportAction.NoChange, Assert.Single(plan.Caves).Action);
        Assert.Empty(plan.CreatePreview(omitNoChange: true));
        var caveAfter = await db.Caves.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.Equal((caveBefore.Version, caveBefore.CurrentRevisionId),
            (caveAfter.Version, caveAfter.CurrentRevisionId));
        Assert.Equal(revisionIdsBefore, await db.CaveRevisions.Where(row => row.CaveId == tenant.CaveId)
            .OrderBy(row => row.Id).Select(row => row.Id).ToListAsync());
        var writes = sql.Items.Where(item => IsWrite(item.Sql)).Select(item => item.Sql).ToList();
        Assert.DoesNotContain(writes, statement => statement.Contains("\"Caves\"", StringComparison.Ordinal) ||
            statement.Contains("\"CaveRevisions\"", StringComparison.Ordinal) || CaveTagTables.Any(statement.Contains));
    }

    [Fact]
    public async Task CommitAbortsOnPlannedVersionDrift()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(CommitAbortsOnPlannedVersionDrift));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        CaveImportPlan plan;
        await using (var planningDb = database.CreateDbContext("a", tenant.AccountId))
        {
            var import = new CaveImportTestHarness(planningDb, planningDb.RequestUser);
            plan = await import.PlanCsvAsync(
                CaveCsv("Planned,County A,A01,1,AA,,,,100,10,5,1,,,,,,,,false,,planned"),
                syncExisting: true);
        }
        await using (var concurrent = database.CreateDbContext("a", tenant.AccountId))
        {
            var cave = await concurrent.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
            cave.Name = "Concurrent";
            await concurrent.SaveChangesAsync();
        }
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var execution = new CaveImportTestHarness(db, db.RequestUser);

        // Act / Assert
        await Assert.ThrowsAsync<CaveRevisionConflictException>(() => execution.ExecuteAsync(plan, "conflict.csv"));
        Assert.False(await db.CaveImportBatches.AnyAsync(batch => batch.SourceFileName == "conflict.csv"));
    }

    private static string CaveCsv(params string[] rows) =>
        ImportDryRunIntegrationTests.CaveHeader + "\n" + string.Join("\n", rows) + "\n";

    private static async Task<object> FileStateAsync(PlanarianDbContext db, string fileId) =>
        await db.Files.AsNoTracking().Where(file => file.Id == fileId).Select(file => new
        {
            file.Id,
            file.AccountId,
            file.CaveId,
            file.FileTypeTagId,
            file.FileName,
            file.DisplayName,
            file.BlobKey,
            file.BlobContainer,
            file.ExpiresOn
        }).SingleAsync();

    private static PlanarianDbContext CreateObservedContext(PostgresTestDatabase database,
        PublishedCaveTestData tenant, SqlTimingInterceptor sql)
    {
        var options = new DbContextOptionsBuilder<PlanarianDbContext>()
            .UseNpgsql(database.ConnectionString, builder =>
            {
                builder.MigrationsAssembly("Planarian.Migrations");
                builder.UseNetTopologySuite();
            })
            .AddInterceptors(sql)
            .Options;
        var db = new PlanarianDbContext(options);
        db.RequestUser = new RequestUser(db)
        {
            Id = "a",
            AccountId = tenant.AccountId,
            FirstName = "Test",
            LastName = "User"
        };
        return db;
    }

    private static void AssertTags(IReadOnlyList<SnapshotTagReference> actual,
        params (SnapshotTagRole Role, string Name)[] expected) =>
        Assert.Equal(
            expected.OrderBy(item => item.Role).ThenBy(item => item.Name)
                .Select(item => $"{item.Role}:{item.Name}"),
            actual.OrderBy(item => item.Role).ThenBy(item => item.NameAtRevision)
                .Select(item => $"{item.Role}:{item.NameAtRevision}"));

    private static bool IsWrite(string sql) =>
        sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase) ||
        sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) ||
        sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase);

    private static readonly string[] CaveTagTables =
    [
        "GeologyTags", "GeologicAgeTags", "MapStatusTags", "PhysiographicProvinceTags", "ArcheologyTags",
        "BiologyTags", "CaveOtherTags", "CartographerNameTags", "CaveReportedByNameTags"
    ];
}
