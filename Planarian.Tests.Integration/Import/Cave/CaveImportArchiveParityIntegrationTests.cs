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

public sealed partial class CaveImportAuthoringParityIntegrationTests
{
    [Fact]
    public async Task ArchiveAndUnarchiveReachSamePublishedStateWithExplicitOperationDifference()
    {
        await using var directDatabase = await _fixture.CreateDatabaseAsync(
            nameof(ArchiveAndUnarchiveReachSamePublishedStateWithExplicitOperationDifference) + "_direct");
        await using var importDatabase = await _fixture.CreateDatabaseAsync(
            nameof(ArchiveAndUnarchiveReachSamePublishedStateWithExplicitOperationDifference) + "_import");

        var directTenant = await CaveTestDataFactory.CreatePublishedCaveAsync(directDatabase, 'a');
        var importTenant = await CaveTestDataFactory.CreatePublishedCaveAsync(importDatabase, 'a');
        await CavePermissions.GrantManagerAsync(directDatabase, directTenant, "manager");

        string directArchiveRevisionId;
        await using (var manager = await CaveTestActor.CreateAsync(
                         directDatabase, directTenant.AccountId, "manager"))
        {
            await manager.Services.Caves.ArchiveCave(directTenant.CaveId);
            manager.Db.ChangeTracker.Clear();
            directArchiveRevisionId = (await CurrentRevisionAsync(manager.Db, directTenant.CaveId)).Id;
        }

        string importArchiveRevisionId;
        string importArchiveBatchId;
        await using (var importDb = importDatabase.CreateDbContext("archive-import", importTenant.AccountId))
        {
            var import = new CaveImportTestHarness(importDb, importDb.RequestUser);
            var plan = await import.PlanCsvAsync(
                CaveCsv(BaselineRow(importTenant, archived: true)), syncExisting: true);
            importArchiveBatchId = (await import.ExecuteAsync(plan, "archive.csv")).ImportBatchId;
            importDb.ChangeTracker.Clear();
            importArchiveRevisionId = (await CurrentRevisionAsync(importDb, importTenant.CaveId)).Id;
        }

        await AssertCurrentSnapshotsEqualAsync(
            directDatabase, directTenant, importDatabase, importTenant);

        await using (var directVerify = directDatabase.CreateDbContext("archive-direct-verify", directTenant.AccountId))
        await using (var importVerify = importDatabase.CreateDbContext("archive-import-verify", importTenant.AccountId))
        {
            var directRevision = await directVerify.CaveRevisions.SingleAsync(row => row.Id == directArchiveRevisionId);
            var importRevision = await importVerify.CaveRevisions.SingleAsync(row => row.Id == importArchiveRevisionId);
            Assert.Equal(
                (directTenant.RevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Archive, (string?)null),
                (directRevision.PreviousRevisionId, directRevision.Source, directRevision.Operation,
                    directRevision.ImportBatchId));
            Assert.Equal(
                (importTenant.RevisionId, CaveRevisionSource.Import, CaveRevisionOperation.Update, importArchiveBatchId),
                (importRevision.PreviousRevisionId, importRevision.Source, importRevision.Operation,
                    importRevision.ImportBatchId));
        }

        string directUnarchiveRevisionId;
        await using (var manager = await CaveTestActor.CreateAsync(
                         directDatabase, directTenant.AccountId, "manager"))
        {
            await manager.Services.Caves.UnarchiveCave(directTenant.CaveId);
            manager.Db.ChangeTracker.Clear();
            directUnarchiveRevisionId = (await CurrentRevisionAsync(manager.Db, directTenant.CaveId)).Id;
        }

        string importUnarchiveRevisionId;
        string importUnarchiveBatchId;
        await using (var importDb = importDatabase.CreateDbContext("unarchive-import", importTenant.AccountId))
        {
            var import = new CaveImportTestHarness(importDb, importDb.RequestUser);
            var plan = await import.PlanCsvAsync(
                CaveCsv(BaselineRow(importTenant, archived: false)), syncExisting: true);
            importUnarchiveBatchId = (await import.ExecuteAsync(plan, "unarchive.csv")).ImportBatchId;
            importDb.ChangeTracker.Clear();
            importUnarchiveRevisionId = (await CurrentRevisionAsync(importDb, importTenant.CaveId)).Id;
        }

        await AssertCurrentSnapshotsEqualAsync(
            directDatabase, directTenant, importDatabase, importTenant);

        await using var directFinal = directDatabase.CreateDbContext("unarchive-direct-verify", directTenant.AccountId);
        await using var importFinal = importDatabase.CreateDbContext("unarchive-import-verify", importTenant.AccountId);
        var directUnarchive = await directFinal.CaveRevisions.SingleAsync(
            row => row.Id == directUnarchiveRevisionId);
        var importUnarchive = await importFinal.CaveRevisions.SingleAsync(
            row => row.Id == importUnarchiveRevisionId);
        Assert.Equal(
            (directArchiveRevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Unarchive, (string?)null),
            (directUnarchive.PreviousRevisionId, directUnarchive.Source, directUnarchive.Operation,
                directUnarchive.ImportBatchId));
        Assert.Equal(
            (importArchiveRevisionId, CaveRevisionSource.Import, CaveRevisionOperation.Update, importUnarchiveBatchId),
            (importUnarchive.PreviousRevisionId, importUnarchive.Source, importUnarchive.Operation,
                importUnarchive.ImportBatchId));
    }

}
