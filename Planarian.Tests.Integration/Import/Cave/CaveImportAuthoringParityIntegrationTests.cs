using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests;

public sealed partial class CaveImportAuthoringParityIntegrationTests : IClassFixture<PostgresTestServer>
{
    private readonly PostgresTestServer _fixture;

    public CaveImportAuthoringParityIntegrationTests(PostgresTestServer fixture) => _fixture = fixture;

    private static readonly DateTime ReportedOn =
        new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task InsertPersistsEveryCommonAuthorableFieldLikeDirectCreation()
    {
        await using var directDatabase = await _fixture.CreateDatabaseAsync(
            nameof(InsertPersistsEveryCommonAuthorableFieldLikeDirectCreation) + "_direct");
        await using var importDatabase = await _fixture.CreateDatabaseAsync(
            nameof(InsertPersistsEveryCommonAuthorableFieldLikeDirectCreation) + "_import");

        var directTenant = await AccountTestDataFactory.CreateAccountWithCountyAsync(directDatabase, 'a');
        var importTenant = await AccountTestDataFactory.CreateAccountWithCountyAsync(importDatabase, 'a');
        var directReferences = await SeedReferencesAsync(directDatabase, directTenant.AccountId);
        _ = await SeedReferencesAsync(importDatabase, importTenant.AccountId);

        await GrantCountyManagerAsync(directDatabase, directTenant, "manager");

        string directCaveId;
        await using (var manager = await CaveTestActor.CreateAsync(
                         directDatabase, directTenant.AccountId, "manager"))
        {
            var values = FullValues(directTenant, directReferences, countyNumber: 30);
            directCaveId = await manager.Services.Caves.AddCave(values, default);
        }

        string importedCaveId;
        string caveImportRevisionId;
        string caveImportBatchId;
        string entranceImportBatchId;
        await using (var importDb = importDatabase.CreateDbContext("import", importTenant.AccountId))
        {
            var caveImport = new CaveImportTestHarness(importDb, importDb.RequestUser);
            var cavePlan = await caveImport.PlanCsvAsync(
                CaveCsv(FullRow(importTenant, countyNumber: 30, archived: false)), syncExisting: false);
            importedCaveId = Assert.Single(cavePlan.Caves).Id;
            caveImportBatchId = (await caveImport.ExecuteAsync(cavePlan, "insert-parity.csv")).ImportBatchId;
            importDb.ChangeTracker.Clear();
            caveImportRevisionId = (await CurrentRevisionAsync(importDb, importedCaveId)).Id;

            var entranceImport = new EntranceImportTestHarness(importDb, importDb.RequestUser);
            var entrancePlan = await entranceImport.PlanCsvAsync(
                ImportDryRunIntegrationTests.EntranceHeader + "\n" +
                CsvRow("Primary", importTenant.CountyDisplayId, 30, true, 35, -86, 500,
                    "Survey Grade", null, "", "", "", "", "", "Primary entrance", "") + "\n",
                syncExisting: false);
            entranceImportBatchId = await entranceImport.ExecuteAsync(entrancePlan, "insert-parity-entrance.csv");
        }

        await using var directVerify = directDatabase.CreateDbContext("verify", directTenant.AccountId);
        await using var importVerify = importDatabase.CreateDbContext("verify", importTenant.AccountId);
        var directSnapshot = await new CavePublishedSnapshotRepository(directVerify, directVerify.RequestUser)
            .BuildAsync(directCaveId);
        var importSnapshot = await new CavePublishedSnapshotRepository(importVerify, importVerify.RequestUser)
            .BuildAsync(importedCaveId);

        ImportParityAssertions.AssertPublishedSnapshotsEqual(
            directSnapshot with { CaveId = "<new-cave>" },
            importSnapshot with { CaveId = "<new-cave>" },
            normalizeEntranceIds: true);

        var directRevision = await CurrentRevisionAsync(directVerify, directCaveId);
        var caveImportRevision = await importVerify.CaveRevisions.SingleAsync(row => row.Id == caveImportRevisionId);
        var entranceImportRevision = await CurrentRevisionAsync(importVerify, importedCaveId);
        Assert.Equal((CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Create, null, (string?)null),
            (directRevision.Source, directRevision.Operation, directRevision.PreviousRevisionId,
                directRevision.ImportBatchId));
        Assert.Equal((CaveRevisionSource.Import, CaveRevisionOperation.Create, null, caveImportBatchId),
            (caveImportRevision.Source, caveImportRevision.Operation, caveImportRevision.PreviousRevisionId,
                caveImportRevision.ImportBatchId));
        Assert.Empty(ReadSnapshot(caveImportRevision).Entrances);
        Assert.Equal((CaveRevisionSource.Import, CaveRevisionOperation.Update, caveImportRevisionId, entranceImportBatchId),
            (entranceImportRevision.Source, entranceImportRevision.Operation, entranceImportRevision.PreviousRevisionId,
                entranceImportRevision.ImportBatchId));
        ImportParityAssertions.AssertPublishedSnapshotsEqual(
            directSnapshot with { CaveId = "<new-cave>" },
            ReadSnapshot(entranceImportRevision) with { CaveId = "<new-cave>" },
            normalizeEntranceIds: true);
    }

    [Fact]
    public async Task UpdateClearAndNoOpMatchDirectAuthoringForEveryCommonField()
    {
        await using var directDatabase = await _fixture.CreateDatabaseAsync(
            nameof(UpdateClearAndNoOpMatchDirectAuthoringForEveryCommonField) + "_direct");
        await using var importDatabase = await _fixture.CreateDatabaseAsync(
            nameof(UpdateClearAndNoOpMatchDirectAuthoringForEveryCommonField) + "_import");

        var (directTenant, _) = await CreateMeasuredPublishedCaveAsync(
            directDatabase, 'a', null, null, null, null);
        var (importTenant, _) = await CreateMeasuredPublishedCaveAsync(
            importDatabase, 'a', null, null, null, null);
        var directReferences = await SeedReferencesAsync(directDatabase, directTenant.AccountId);
        _ = await SeedReferencesAsync(importDatabase, importTenant.AccountId);
        await CavePermissions.GrantManagerAsync(directDatabase, directTenant, "manager");

        string directFullRevisionId;
        await using (var manager = await CaveTestActor.CreateAsync(
                         directDatabase, directTenant.AccountId, "manager"))
        {
            var context = await manager.Services.Caves.GetEditAuthoringContextAsync(directTenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            ApplyFullTarget(values, directReferences);
            await manager.Services.Caves.AddCave(values, default);
            manager.Db.ChangeTracker.Clear();
            directFullRevisionId = (await CurrentRevisionAsync(manager.Db, directTenant.CaveId)).Id;
        }

        string importFullRevisionId;
        string firstImportBatchId;
        await using (var importDb = importDatabase.CreateDbContext("import-full", importTenant.AccountId))
        {
            var import = new CaveImportTestHarness(importDb, importDb.RequestUser);
            var plan = await import.PlanCsvAsync(
                CaveCsv(FullRow(importTenant, importTenant.CountyNumber, archived: false)), syncExisting: true);
            var result = await import.ExecuteAsync(plan, "update-all-fields.csv");
            firstImportBatchId = result.ImportBatchId;
            importDb.ChangeTracker.Clear();
            importFullRevisionId = (await CurrentRevisionAsync(importDb, importTenant.CaveId)).Id;
        }

        await AssertCurrentSnapshotsEqualAsync(
            directDatabase, directTenant, importDatabase, importTenant);

        await using (var directVerify = directDatabase.CreateDbContext("verify-full", directTenant.AccountId))
        {
            var revision = await directVerify.CaveRevisions.SingleAsync(row => row.Id == directFullRevisionId);
            Assert.Equal((directTenant.RevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, null),
                (revision.PreviousRevisionId, revision.Source, revision.Operation, revision.ImportBatchId));
        }

        await using (var importVerify = importDatabase.CreateDbContext("verify-full", importTenant.AccountId))
        {
            var revision = await importVerify.CaveRevisions.SingleAsync(row => row.Id == importFullRevisionId);
            Assert.Equal((importTenant.RevisionId, CaveRevisionSource.Import, CaveRevisionOperation.Update, firstImportBatchId),
                (revision.PreviousRevisionId, revision.Source, revision.Operation, revision.ImportBatchId));
        }

        string directClearedRevisionId;
        await using (var manager = await CaveTestActor.CreateAsync(
                         directDatabase, directTenant.AccountId, "manager"))
        {
            var context = await manager.Services.Caves.GetEditAuthoringContextAsync(directTenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            ApplyClearedTarget(values);
            await manager.Services.Caves.AddCave(values, default);
            manager.Db.ChangeTracker.Clear();
            directClearedRevisionId = (await CurrentRevisionAsync(manager.Db, directTenant.CaveId)).Id;
        }

        string importClearedRevisionId;
        string secondImportBatchId;
        await using (var importDb = importDatabase.CreateDbContext("import-clear", importTenant.AccountId))
        {
            var import = new CaveImportTestHarness(importDb, importDb.RequestUser);
            var plan = await import.PlanCsvAsync(
                CaveCsv(ClearedRow(importTenant, importTenant.CountyNumber)), syncExisting: true);
            var result = await import.ExecuteAsync(plan, "clear-all-fields.csv");
            secondImportBatchId = result.ImportBatchId;
            importDb.ChangeTracker.Clear();
            importClearedRevisionId = (await CurrentRevisionAsync(importDb, importTenant.CaveId)).Id;
        }

        await AssertCurrentSnapshotsEqualAsync(
            directDatabase, directTenant, importDatabase, importTenant);

        await using (var directVerify = directDatabase.CreateDbContext("verify-clear", directTenant.AccountId))
        {
            var revision = await directVerify.CaveRevisions.SingleAsync(row => row.Id == directClearedRevisionId);
            Assert.Equal((directFullRevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, null),
                (revision.PreviousRevisionId, revision.Source, revision.Operation, revision.ImportBatchId));
        }

        await using (var importVerify = importDatabase.CreateDbContext("verify-clear", importTenant.AccountId))
        {
            var revision = await importVerify.CaveRevisions.SingleAsync(row => row.Id == importClearedRevisionId);
            Assert.Equal((importFullRevisionId, CaveRevisionSource.Import, CaveRevisionOperation.Update, secondImportBatchId),
                (revision.PreviousRevisionId, revision.Source, revision.Operation, revision.ImportBatchId));
        }

        await using (var manager = await CaveTestActor.CreateAsync(
                         directDatabase, directTenant.AccountId, "manager"))
        {
            var context = await manager.Services.Caves.GetEditAuthoringContextAsync(directTenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            await manager.Services.Caves.AddCave(values, default);
            manager.Db.ChangeTracker.Clear();
            Assert.Equal(directClearedRevisionId,
                (await CurrentRevisionAsync(manager.Db, directTenant.CaveId)).Id);
        }

        await using (var importDb = importDatabase.CreateDbContext("import-no-op", importTenant.AccountId))
        {
            var revisionsBefore = await importDb.CaveRevisions.CountAsync(row => row.CaveId == importTenant.CaveId);
            var import = new CaveImportTestHarness(importDb, importDb.RequestUser);
            var plan = await import.PlanCsvAsync(
                CaveCsv(ClearedRow(importTenant, importTenant.CountyNumber)), syncExisting: true);
            Assert.Equal(CaveImportAction.NoChange, Assert.Single(plan.Caves).Action);
            var result = await import.ExecuteAsync(plan, "no-op-parity.csv");
            importDb.ChangeTracker.Clear();

            Assert.Equal(importClearedRevisionId,
                (await CurrentRevisionAsync(importDb, importTenant.CaveId)).Id);
            Assert.Equal(revisionsBefore,
                await importDb.CaveRevisions.CountAsync(row => row.CaveId == importTenant.CaveId));
            var batch = await importDb.CaveImportBatches.SingleAsync(row => row.Id == result.ImportBatchId);
            Assert.Equal((0, 0, 0, 1), (batch.InsertedCount, batch.UpdatedCount, batch.DeletedCount, batch.NoChangeCount));
        }
    }

}
