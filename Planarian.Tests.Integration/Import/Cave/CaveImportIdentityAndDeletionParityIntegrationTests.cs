using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests;

public sealed partial class CaveImportAuthoringParityIntegrationTests
{
    [Fact]
    public async Task CountyNumberIdentityMoveRetainsDirectCaveButImportRecordsDeleteAndCreate()
    {
        await using var directDatabase = await _fixture.CreateDatabaseAsync(
            nameof(CountyNumberIdentityMoveRetainsDirectCaveButImportRecordsDeleteAndCreate) + "_direct");
        await using var importDatabase = await _fixture.CreateDatabaseAsync(
            nameof(CountyNumberIdentityMoveRetainsDirectCaveButImportRecordsDeleteAndCreate) + "_import");

        var (directTenant, _) = await CreateMeasuredPublishedCaveAsync(
            directDatabase, 'a', null, null, null, null);
        var (importTenant, _) = await CreateMeasuredPublishedCaveAsync(
            importDatabase, 'a', null, null, null, null);
        await CavePermissions.GrantManagerAsync(directDatabase, directTenant, "manager");
        await CavePermissions.GrantManagerAsync(importDatabase, importTenant, "manager");

        // Give both starting aggregates the same nonblank Entrance content so the direct retained-ID
        // path and the import delete/create + Entrance append path can be compared as complete aggregates.
        directTenant = directTenant with
        {
            RevisionId = await NamePrimaryEntranceAsync(directDatabase, directTenant, "manager")
        };
        importTenant = importTenant with
        {
            RevisionId = await NamePrimaryEntranceAsync(importDatabase, importTenant, "manager")
        };

        string directMoveRevisionId;
        await using (var manager = await CaveTestActor.CreateAsync(
                         directDatabase, directTenant.AccountId, "manager"))
        {
            var context = await manager.Services.Caves.GetEditAuthoringContextAsync(directTenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            values.CountyNumber = 2;
            values.IsCountyNumberManuallySet = true;
            await manager.Services.Caves.AddCave(values, default);
            manager.Db.ChangeTracker.Clear();
            directMoveRevisionId = (await CurrentRevisionAsync(manager.Db, directTenant.CaveId)).Id;
        }

        string newImportedCaveId;
        string caveBatchId;
        string createRevisionId;
        await using (var db = importDatabase.CreateDbContext("identity-import", importTenant.AccountId))
        {
            var import = new CaveImportTestHarness(db, db.RequestUser);
            var plan = await import.PlanCsvAsync(
                CaveCsv(CsvRow(importTenant.CaveName, importTenant.CountyName, importTenant.CountyDisplayId, 2,
                    importTenant.StateAbbreviation, "", "", "", null, null, null, null,
                    "", "", "", "", "", "", "", false, "", "Identity narrative")),
                syncExisting: true);
            var inserted = Assert.Single(plan.Caves, cave => cave.Action == CaveImportAction.Insert);
            Assert.Equal(importTenant.CaveId, Assert.Single(plan.Deletions).CaveId);
            newImportedCaveId = inserted.Id;
            caveBatchId = (await import.ExecuteAsync(plan, "identity-move.csv")).ImportBatchId;
            db.ChangeTracker.Clear();
            createRevisionId = (await CurrentRevisionAsync(db, newImportedCaveId)).Id;
        }

        string entranceBatchId;
        await using (var db = importDatabase.CreateDbContext("identity-entrance", importTenant.AccountId))
        {
            var import = new EntranceImportTestHarness(db, db.RequestUser);
            var plan = await import.PlanCsvAsync(
                ImportDryRunIntegrationTests.EntranceHeader + "\n" +
                CsvRow("Primary", importTenant.CountyDisplayId, 2, true, 35, -86, 500,
                    "Survey Grade", null, "", "", "", "", "", "Identity entrance", "") + "\n",
                syncExisting: false);
            entranceBatchId = await import.ExecuteAsync(plan, "identity-entrance.csv");
        }

        await using var directVerify = directDatabase.CreateDbContext("verify", directTenant.AccountId);
        await using var importVerify = importDatabase.CreateDbContext("verify", importTenant.AccountId);
        var directSnapshot = await new CavePublishedSnapshotRepository(directVerify, directVerify.RequestUser)
            .BuildAsync(directTenant.CaveId);
        var importSnapshot = await new CavePublishedSnapshotRepository(importVerify, importVerify.RequestUser)
            .BuildAsync(newImportedCaveId);
        ImportParityAssertions.AssertPublishedSnapshotsEqual(
            directSnapshot with { CaveId = "<identity-moved-cave>" },
            importSnapshot with { CaveId = "<identity-moved-cave>" },
            normalizeEntranceIds: true);

        var directMove = await directVerify.CaveRevisions.SingleAsync(row => row.Id == directMoveRevisionId);
        Assert.Equal((directTenant.RevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update),
            (directMove.PreviousRevisionId, directMove.Source, directMove.Operation));

        var caveBatchRevisions = await importVerify.CaveRevisions
            .Where(row => row.ImportBatchId == caveBatchId)
            .ToListAsync();
        Assert.Equal(2, caveBatchRevisions.Count);
        var deleted = Assert.Single(caveBatchRevisions, row => row.CaveId == importTenant.CaveId);
        Assert.Equal((importTenant.RevisionId, CaveRevisionOperation.Delete),
            (deleted.PreviousRevisionId, deleted.Operation));
        var created = Assert.Single(caveBatchRevisions, row => row.CaveId == newImportedCaveId);
        Assert.Equal(createRevisionId, created.Id);
        Assert.Equal((CaveRevisionOperation.Create, (string?)null),
            (created.Operation, created.PreviousRevisionId));

        var current = await CurrentRevisionAsync(importVerify, newImportedCaveId);
        Assert.Equal((createRevisionId, CaveRevisionSource.Import, CaveRevisionOperation.Update, entranceBatchId),
            (current.PreviousRevisionId, current.Source, current.Operation, current.ImportBatchId));
    }

    [Fact]
    public async Task DeletePublishesTheSamePreDeleteTombstoneAsDirectHardDelete()
    {
        await using var directDatabase = await _fixture.CreateDatabaseAsync(
            nameof(DeletePublishesTheSamePreDeleteTombstoneAsDirectHardDelete) + "_direct");
        await using var importDatabase = await _fixture.CreateDatabaseAsync(
            nameof(DeletePublishesTheSamePreDeleteTombstoneAsDirectHardDelete) + "_import");

        var (directTenant, _) = await CreateMeasuredPublishedCaveAsync(
            directDatabase, 'a', null, null, null, null);
        var (importTenant, _) = await CreateMeasuredPublishedCaveAsync(
            importDatabase, 'a', null, null, null, null);
        var directReferences = await SeedReferencesAsync(directDatabase, directTenant.AccountId);
        var importReferences = await SeedReferencesAsync(importDatabase, importTenant.AccountId);
        await CavePermissions.GrantManagerAsync(directDatabase, directTenant, "manager");

        var directBeforeDeleteRevisionId = await PopulateExistingCaveAsync(
            directDatabase, directTenant, directReferences, "manager");
        var importBeforeDeleteRevisionId = await PopulateExistingCaveByImportAsync(
            importDatabase, importTenant);

        CavePublishedSnapshotV1 directBefore;
        CavePublishedSnapshotV1 importBefore;
        await using (var directBeforeDb = directDatabase.CreateDbContext("before", directTenant.AccountId))
            directBefore = await new CavePublishedSnapshotRepository(directBeforeDb, directBeforeDb.RequestUser)
                .BuildAsync(directTenant.CaveId);
        await using (var importBeforeDb = importDatabase.CreateDbContext("before", importTenant.AccountId))
            importBefore = await new CavePublishedSnapshotRepository(importBeforeDb, importBeforeDb.RequestUser)
                .BuildAsync(importTenant.CaveId);
        ImportParityAssertions.AssertPublishedSnapshotsEqual(directBefore, importBefore);

        await using (var manager = await CaveTestActor.CreateAsync(
                         directDatabase, directTenant.AccountId, "manager"))
            await manager.Services.Caves.DeleteCave(directTenant.CaveId, default);

        string deleteBatchId;
        await using (var importDb = importDatabase.CreateDbContext("delete-import", importTenant.AccountId))
        {
            var import = new CaveImportTestHarness(importDb, importDb.RequestUser);
            var plan = await import.PlanCsvAsync(ImportDryRunIntegrationTests.CaveHeader + "\n", syncExisting: true);
            Assert.Equal(importTenant.CaveId, Assert.Single(plan.Deletions).CaveId);
            deleteBatchId = (await import.ExecuteAsync(plan, "delete-parity.csv")).ImportBatchId;
        }

        await using var directVerify = directDatabase.CreateDbContext("verify", directTenant.AccountId);
        await using var importVerify = importDatabase.CreateDbContext("verify", importTenant.AccountId);
        Assert.False(await directVerify.Caves.IgnoreQueryFilters()
            .AnyAsync(row => row.AccountId == directTenant.AccountId && row.Id == directTenant.CaveId));
        Assert.False(await importVerify.Caves.IgnoreQueryFilters()
            .AnyAsync(row => row.AccountId == importTenant.AccountId && row.Id == importTenant.CaveId));

        var directDelete = Assert.Single(await directVerify.CaveRevisions
            .Where(row => row.CaveId == directTenant.CaveId && row.Operation == CaveRevisionOperation.Delete)
            .ToListAsync());
        var importDelete = Assert.Single(await importVerify.CaveRevisions
            .Where(row => row.CaveId == importTenant.CaveId && row.Operation == CaveRevisionOperation.Delete)
            .ToListAsync());

        Assert.Equal(directBeforeDeleteRevisionId, directDelete.PreviousRevisionId);
        Assert.Equal(importBeforeDeleteRevisionId, importDelete.PreviousRevisionId);
        Assert.Equal((CaveRevisionSource.ManagerEdit, (string?)null),
            (directDelete.Source, directDelete.ImportBatchId));
        Assert.Equal((CaveRevisionSource.Import, deleteBatchId),
            (importDelete.Source, importDelete.ImportBatchId));
        ImportParityAssertions.AssertPublishedSnapshotsEqual(ReadSnapshot(directDelete), ReadSnapshot(importDelete));
        ImportParityAssertions.AssertPublishedSnapshotsEqual(directBefore, ReadSnapshot(directDelete));
    }

}
