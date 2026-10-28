using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;

namespace Planarian.Tests;

public sealed class CaveImportMixedBatchIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    private const string LinePlotGeoJson =
        """{"type":"FeatureCollection","features":[{"type":"Feature","properties":{},"geometry":{"type":"LineString","coordinates":[[-86,35],[-85.9,35.1]]}}]}""";

    [Fact]
    public async Task SyncKitchenSinkPersistsExactGraphRevisionsAndBatchProvenance()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(SyncKitchenSinkPersistsExactGraphRevisionsAndBatchProvenance));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');

        var update = await PublishAsync(database,
            await CaveTestDataFactory.AddCaveAsync(database, account, "mixupdate1", "Update Cave", 1),
            "mixrevup01");
        var noChange = await PublishAsync(database,
            await CaveTestDataFactory.AddCaveAsync(database, account, "mixnochg01", "No Change Cave", 2),
            "mixrevno01");
        var deleted = await PublishAsync(database,
            await CaveTestDataFactory.AddCaveAsync(database, account, "mixdelete1", "Delete Cave", 3),
            "mixrevdel1");

        var updateAssets = await SeedAggregateAssetsAsync(database, update, "update-user", "u");
        var deleteAssets = await SeedAggregateAssetsAsync(database, deleted, "delete-user", "d");
        var updatePredecessor = await PublishCurrentAggregateAsync(database, update);
        var deletePredecessor = await PublishCurrentAggregateAsync(database, deleted);

        CavePublishedSnapshotV1 updateBefore;
        CavePublishedSnapshotV1 deleteBefore;
        await using (var before = database.CreateDbContext("before", account.AccountId))
        {
            var beforeSnapshots = new CavePublishedSnapshotRepository(before, before.RequestUser);
            updateBefore = await beforeSnapshots.BuildAsync(update.CaveId);
            deleteBefore = await beforeSnapshots.BuildAsync(deleted.CaveId);
        }

        string noChangeRevisionBefore;
        await using (var before = database.CreateDbContext("nochange-before", account.AccountId))
            noChangeRevisionBefore = (await before.Caves.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == noChange.CaveId)).CurrentRevisionId!;

        CaveImportExecutionResult execution;
        CaveImportPlan plan;
        await using (var db = database.CreateDbContext("import", account.AccountId))
        {
            var import = new CaveImportTestHarness(db, db.RequestUser);
            plan = await import.PlanCsvAsync(CaveCsv(
                CaveRow("Updated Cave", account, 1, lengthFeet: 250, narrative: "Updated narrative"),
                CaveRow("No Change Cave", account, 2),
                CaveRow("Inserted Cave", account, 4, lengthFeet: 75, narrative: "Inserted narrative")),
                syncExisting: true);

            Assert.Equal(CaveImportAction.Update,
                Assert.Single(plan.Caves, cave => cave.CountyNumber == 1).Action);
            Assert.Equal(CaveImportAction.NoChange,
                Assert.Single(plan.Caves, cave => cave.CountyNumber == 2).Action);
            Assert.Equal(CaveImportAction.Insert,
                Assert.Single(plan.Caves, cave => cave.CountyNumber == 4).Action);
            Assert.Equal(deleted.CaveId, Assert.Single(plan.Deletions).CaveId);

            execution = await import.ExecuteAsync(plan, "mixed-kitchen-sink.csv");
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        var batch = await verify.CaveImportBatches.SingleAsync(row => row.Id == execution.ImportBatchId);
        Assert.Equal("mixed-kitchen-sink.csv", batch.SourceFileName);
        Assert.Equal(CaveImportKind.CaveCsv, batch.Kind);
        Assert.True(batch.SyncExisting);
        Assert.Equal((3, 1, 1, 1, 1),
            (batch.SourceRecordCount, batch.InsertedCount, batch.UpdatedCount,
                batch.DeletedCount, batch.NoChangeCount));

        var liveCaves = await verify.Caves.IgnoreQueryFilters()
            .Where(row => row.AccountId == account.AccountId)
            .OrderBy(row => row.CountyNumber)
            .ToListAsync();
        Assert.Equal([1, 2, 4], liveCaves.Select(row => row.CountyNumber).ToList());
        Assert.Equal("Updated Cave", liveCaves.Single(row => row.CountyNumber == 1).Name);
        Assert.Equal(250, liveCaves.Single(row => row.CountyNumber == 1).LengthFeet);
        Assert.Equal("Updated narrative", liveCaves.Single(row => row.CountyNumber == 1).Narrative);
        Assert.Equal("No Change Cave", liveCaves.Single(row => row.CountyNumber == 2).Name);
        Assert.Equal("Inserted Cave", liveCaves.Single(row => row.CountyNumber == 4).Name);

        var snapshots = new CavePublishedSnapshotRepository(verify, verify.RequestUser);
        var updateAfter = await snapshots.BuildAsync(update.CaveId);
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(updateBefore.Entrances),
            System.Text.Json.JsonSerializer.Serialize(updateAfter.Entrances));
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(updateBefore.Files),
            System.Text.Json.JsonSerializer.Serialize(updateAfter.Files));
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(updateBefore.LinePlots),
            System.Text.Json.JsonSerializer.Serialize(updateAfter.LinePlots));

        Assert.True(await verify.Entrances.IgnoreQueryFilters().AnyAsync(row => row.Id == updateAssets.EntranceId));
        Assert.True(await verify.Files.IgnoreQueryFilters().AnyAsync(row => row.Id == updateAssets.FileId));
        Assert.True(await verify.CaveGeoJsons.IgnoreQueryFilters().AnyAsync(row => row.Id == updateAssets.LinePlotId));
        Assert.True(await verify.Favorites.IgnoreQueryFilters().AnyAsync(row => row.CaveId == update.CaveId));
        Assert.True(await verify.CavePermissions.IgnoreQueryFilters().AnyAsync(row => row.CaveId == update.CaveId));

        Assert.False(await verify.Caves.IgnoreQueryFilters().AnyAsync(row => row.Id == deleted.CaveId));
        Assert.False(await verify.Entrances.IgnoreQueryFilters().AnyAsync(row => row.Id == deleteAssets.EntranceId));
        Assert.False(await verify.Files.IgnoreQueryFilters().AnyAsync(row => row.Id == deleteAssets.FileId));
        Assert.False(await verify.CaveGeoJsons.IgnoreQueryFilters().AnyAsync(row => row.Id == deleteAssets.LinePlotId));
        Assert.False(await verify.Favorites.IgnoreQueryFilters().AnyAsync(row => row.CaveId == deleted.CaveId));
        Assert.False(await verify.CavePermissions.IgnoreQueryFilters().AnyAsync(row => row.CaveId == deleted.CaveId));
        Assert.Contains(execution.BlobDeletes,
            blob => blob.BlobContainer == "test" && blob.BlobKey == deleteAssets.BlobKey);
        Assert.DoesNotContain(execution.BlobDeletes,
            blob => blob.BlobContainer == "test" && blob.BlobKey == updateAssets.BlobKey);

        var batchRevisions = await verify.CaveRevisions
            .Where(row => row.ImportBatchId == execution.ImportBatchId)
            .OrderBy(row => row.CaveId)
            .ToListAsync();
        Assert.Equal(3, batchRevisions.Count);
        Assert.All(batchRevisions, revision => Assert.Equal(CaveRevisionSource.Import, revision.Source));

        var updateRevision = Assert.Single(batchRevisions, revision => revision.CaveId == update.CaveId);
        Assert.Equal(CaveRevisionOperation.Update, updateRevision.Operation);
        Assert.Equal(updatePredecessor, updateRevision.PreviousRevisionId);
        Assert.Equal(updateRevision.Id,
            liveCaves.Single(row => row.Id == update.CaveId).CurrentRevisionId);

        var deleteRevision = Assert.Single(batchRevisions, revision => revision.CaveId == deleted.CaveId);
        Assert.Equal(CaveRevisionOperation.Delete, deleteRevision.Operation);
        Assert.Equal(deletePredecessor, deleteRevision.PreviousRevisionId);
        Assert.Equal(CaveSnapshotJson.Serialize(deleteBefore),
            CaveSnapshotJson.Serialize(ReadSnapshot(deleteRevision)));

        var insertedPlan = Assert.Single(plan.Caves, cave => cave.CountyNumber == 4);
        var insertRevision = Assert.Single(batchRevisions, revision => revision.CaveId == insertedPlan.Id);
        Assert.Equal(CaveRevisionOperation.Create, insertRevision.Operation);
        Assert.Null(insertRevision.PreviousRevisionId);
        Assert.Equal(insertRevision.Id,
            liveCaves.Single(row => row.Id == insertedPlan.Id).CurrentRevisionId);

        var noChangeCave = liveCaves.Single(row => row.Id == noChange.CaveId);
        Assert.Equal(noChangeRevisionBefore, noChangeCave.CurrentRevisionId);
        Assert.DoesNotContain(batchRevisions, revision => revision.CaveId == noChange.CaveId);
    }

    private static async Task<PublishedCaveTestData> PublishAsync(
        PostgresTestDatabase database, CaveTestData cave, string revisionId) =>
        await CaveTestDataFactory.PublishBaselineRevisionAsync(database, cave, revisionId);

    private static async Task<AggregateAssets> SeedAggregateAssetsAsync(
        PostgresTestDatabase database, PublishedCaveTestData cave, string userId, string prefix)
    {
        var locationQuality = await ReferenceTestData.AddTagAsync(
            database, cave.AccountId, TagTypeKeyConstant.LocationQuality, $"{prefix} Survey Grade");
        var entranceId = $"{prefix}entrance1";
        await EntranceTestData.AddEntranceAsync(database, cave, entranceId,
            locationQualityTagId: locationQuality.Id);

        var fileType = await ReferenceTestData.AddTagAsync(
            database, cave.AccountId, TagTypeKeyConstant.File, $"{prefix} Document");
        var fileId = $"{prefix}file00001";
        var blobKey = $"{prefix}-blob";
        await using (var fileSeed = database.CreateDbContext("file-seed", cave.AccountId))
        {
            fileSeed.Files.Add(new Planarian.Model.Database.Entities.RidgeWalker.File
            {
                Id = fileId,
                AccountId = cave.AccountId,
                CaveId = cave.CaveId,
                FileTypeTagId = fileType.Id,
                Name = $"{prefix}-file",
                Extension = ".pdf",
                BlobContainer = "test",
                BlobKey = blobKey
            });
            await fileSeed.SaveChangesAsync();
        }

        await CavePermissions.GrantManagerAsync(database, cave, userId);
        string linePlotId;
        await using (var seed = database.CreateDbContext(userId, cave.AccountId))
        {
            await CavePermissions.EnsureAccountUserAsync(seed, cave.AccountId);
            await seed.RequestUser.Initialize(cave.AccountId, seed.RequestUser.Id);
            var linePlot = new CaveGeoJson
            {
                Id = $"{prefix}plot0001",
                CaveId = cave.CaveId,
                Name = $"{prefix} plot",
                GeoJson = LinePlotGeoJson
            };
            seed.CaveGeoJsons.Add(linePlot);
            seed.Favorites.Add(new Favorite
            {
                UserId = seed.RequestUser.Id,
                AccountId = cave.AccountId,
                CaveId = cave.CaveId
            });
            await seed.SaveChangesAsync();
            linePlotId = linePlot.Id;
        }

        return new AggregateAssets(entranceId, fileId, linePlotId, blobKey);
    }

    private static async Task<string> PublishCurrentAggregateAsync(
        PostgresTestDatabase database, PublishedCaveTestData cave)
    {
        await using var db = database.CreateDbContext("aggregate-baseline", cave.AccountId);
        var mutation = await new CaveMutationRepository(
                db, db.RequestUser, new CavePublishedSnapshotRepository(db, db.RequestUser))
            .PublishExistingAsync(cave.CaveId, cave.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, _ => { });
        return mutation.RevisionId!;
    }

    private static CavePublishedSnapshotV1 ReadSnapshot(CaveRevision revision) =>
        CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);

    private static string CaveCsv(params string[] rows) =>
        ImportDryRunIntegrationTests.CaveHeader + "\n" + string.Join("\n", rows) + "\n";

    private static string CaveRow(
        string name, AccountCountyTestData account, int countyNumber,
        double? lengthFeet = null, string? narrative = null) =>
        CsvRow(name, account.CountyName, account.CountyDisplayId, countyNumber, account.StateAbbreviation,
            "", "", "", lengthFeet, null, null, null, "", "", "", "", "", "", "", false, "", narrative);

    private static string CsvRow(params object?[] values) =>
        string.Join(',', values.Select(value =>
        {
            var text = value switch
            {
                null => "",
                bool boolean => boolean ? "true" : "false",
                IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => value.ToString() ?? ""
            };
            return text.Contains(',') || text.Contains('"')
                ? "\"" + text.Replace("\"", "\"\"") + "\""
                : text;
        }));

    private sealed record AggregateAssets(string EntranceId, string FileId, string LinePlotId, string BlobKey);
}
