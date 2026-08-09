using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Modules.Caves.Revisions;
using Xunit;

namespace Planarian.Tests;

public sealed class CaveMutationCoordinatorTransactionIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task CallerOwnedTransactionPublishesManagerUpdateAndAdvancesPointer()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CallerOwnedTransactionPublishesManagerUpdateAndAdvancesPointer));
        var tenant = await TestDataScenarios.CreatePublishedCaveScenarioAsync(database, 'a');
        string revisionId;

        await using (var db = database.CreateDbContext("manager", tenant.AccountId))
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var coordinator = new CaveMutationRepository(db, db.RequestUser,
                new CavePublishedSnapshotRepository(db, db.RequestUser));
            var preparation = await coordinator.PrepareExistingAsync(tenant.CaveId, tenant.RevisionId);
            var cave = await db.Caves.IgnoreQueryFilters().SingleAsync(c => c.Id == tenant.CaveId);
            cave.Name = "Manager updated";
            await db.SaveChangesAsync();

            var result = await coordinator.PublishPreparedAsync(
                preparation, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update);
            Assert.True(result.CreatedRevision);
            revisionId = Assert.IsType<string>(result.RevisionId);
            await transaction.CommitAsync();
        }

        await using var verify = database.CreateDbContext("manager", tenant.AccountId);
        var persistedCave = await verify.Caves.IgnoreQueryFilters().SingleAsync(c => c.Id == tenant.CaveId);
        Assert.Equal(revisionId, persistedCave.CurrentRevisionId);
        var revision = await verify.CaveRevisions.SingleAsync(r => r.Id == revisionId);
        Assert.Equal(tenant.RevisionId, revision.PreviousRevisionId);
        Assert.Equal(CaveRevisionSource.ManagerEdit, revision.Source);
        Assert.Equal(CaveRevisionOperation.Update, revision.Operation);
        Assert.Equal("Manager updated",
            CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion).Name);
    }

    [Fact]
    public async Task CallerOwnedDeletePublishesFinalTombstoneWithPreDeleteSnapshot()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CallerOwnedDeletePublishesFinalTombstoneWithPreDeleteSnapshot));
        var tenant = await TestDataScenarios.CreatePublishedCaveScenarioAsync(database, 'a');
        string tombstoneId;

        await using (var db = database.CreateDbContext("manager", tenant.AccountId))
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var coordinator = new CaveMutationRepository(db, db.RequestUser,
                new CavePublishedSnapshotRepository(db, db.RequestUser));
            var preparation = await coordinator.PrepareExistingAsync(tenant.CaveId, tenant.RevisionId);
            var cave = await db.Caves.IgnoreQueryFilters().SingleAsync(c => c.Id == tenant.CaveId);
            db.Caves.Remove(cave);
            await db.SaveChangesAsync();

            var result = await coordinator.PublishPreparedDeleteAsync(
                preparation, CaveRevisionSource.ManagerEdit);
            tombstoneId = Assert.IsType<string>(result.RevisionId);
            await transaction.CommitAsync();
        }

        await using var verify = database.CreateDbContext("manager", tenant.AccountId);
        Assert.False(await verify.Caves.IgnoreQueryFilters().AnyAsync(c => c.Id == tenant.CaveId));
        var tombstone = await verify.CaveRevisions.SingleAsync(r => r.Id == tombstoneId);
        Assert.Equal(tenant.RevisionId, tombstone.PreviousRevisionId);
        Assert.Equal(CaveRevisionSource.ManagerEdit, tombstone.Source);
        Assert.Equal(CaveRevisionOperation.Delete, tombstone.Operation);
        var snapshot = CaveSnapshotJson.Deserialize(tombstone.SnapshotJson, tombstone.SnapshotSchemaVersion);
        Assert.Equal(tenant.CaveId, snapshot.CaveId);
        Assert.Equal("Cave A", snapshot.Name);
    }

    [Fact]
    public async Task PreparedSemanticNoOpDoesNotCreateRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(PreparedSemanticNoOpDoesNotCreateRevision));
        var tenant = await TestDataScenarios.CreatePublishedCaveScenarioAsync(database, 'a');

        await using (var db = database.CreateDbContext("manager", tenant.AccountId))
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var coordinator = new CaveMutationRepository(db, db.RequestUser,
                new CavePublishedSnapshotRepository(db, db.RequestUser));
            var preparation = await coordinator.PrepareExistingAsync(tenant.CaveId, tenant.RevisionId);
            var result = await coordinator.PublishPreparedAsync(
                preparation, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update);
            Assert.False(result.CreatedRevision);
            Assert.Equal(tenant.RevisionId, result.RevisionId);
            await transaction.CommitAsync();
        }

        await using var verify = database.CreateDbContext("manager", tenant.AccountId);
        Assert.Equal(1, await verify.CaveRevisions.CountAsync(r => r.CaveId == tenant.CaveId));
    }
}
