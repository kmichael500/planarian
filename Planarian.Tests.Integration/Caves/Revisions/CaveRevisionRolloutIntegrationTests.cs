using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Modules.Caves.Revisions;
using Xunit;
using Planarian.Operations.CaveRevisionHistory;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveRevisionRolloutIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task RolloutInitializesEveryExistingCaveAcrossAccountsWithOneBoundaryAndIsIdempotent()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RolloutInitializesEveryExistingCaveAcrossAccountsWithOneBoundaryAndIsIdempotent));
        var accountA = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var accountB = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'b');
        var caveA1 = await CaveTestDataFactory.AddCaveAsync(database, accountA, "legacy001a", "Legacy A1", 1);
        var caveA2 = await CaveTestDataFactory.AddCaveAsync(database, accountA, "legacy002a", "Legacy A2", 2);
        var caveB1 = await CaveTestDataFactory.AddCaveAsync(database, accountB, "legacy001b", "Legacy B1", 1);

        var result = await CaveRevisionRolloutRepository.InitializeAsync(database.ConnectionString);

        Assert.False(result.AlreadyComplete);
        Assert.Equal(3, result.InitializedCaveCount);
        Assert.Equal(2, result.BatchCount);

        await using var verifyDb = database.CreateDbContext("verify", accountA.AccountId);
        var caves = await verifyDb.Caves.IgnoreQueryFilters().AsNoTracking()
            .Where(cave => cave.Id == caveA1.CaveId || cave.Id == caveA2.CaveId || cave.Id == caveB1.CaveId)
            .OrderBy(cave => cave.Id)
            .ToListAsync();
        Assert.Equal(3, caves.Count);
        Assert.All(caves, cave => Assert.NotNull(cave.CurrentRevisionId));
        Assert.All(caves, cave => Assert.Null(cave.ModifiedOn));
        Assert.All(caves, cave => Assert.Null(cave.ModifiedByUserId));

        var revisions = await verifyDb.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
            .Where(revision => revision.CaveId == caveA1.CaveId || revision.CaveId == caveA2.CaveId ||
                               revision.CaveId == caveB1.CaveId)
            .OrderBy(revision => revision.CaveId)
            .ToListAsync();
        Assert.Equal(3, revisions.Count);
        Assert.Single(revisions.Select(revision => revision.CreatedOn).Distinct());
        Assert.All(revisions, revision => Assert.Equal(result.RevisionHistoryStartedOn, revision.CreatedOn));
        Assert.All(revisions, revision => Assert.Equal(CaveRevisionSource.SystemBaseline, revision.Source));
        Assert.All(revisions, revision => Assert.Equal(CaveRevisionOperation.Baseline, revision.Operation));
        Assert.All(revisions, revision => Assert.Null(revision.PreviousRevisionId));
        Assert.All(revisions, revision => Assert.Null(revision.CreatedByUserId));

        var names = revisions.ToDictionary(
            revision => revision.CaveId,
            revision => CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion).Name,
            StringComparer.Ordinal);
        Assert.Equal("Legacy A1", names[caveA1.CaveId]);
        Assert.Equal("Legacy A2", names[caveA2.CaveId]);
        Assert.Equal("Legacy B1", names[caveB1.CaveId]);

        var rerun = await CaveRevisionRolloutRepository.InitializeAsync(database.ConnectionString);
        Assert.True(rerun.AlreadyComplete);
        Assert.Equal(0, rerun.InitializedCaveCount);
        Assert.Equal(0, rerun.BatchCount);
        Assert.Equal(result.RevisionHistoryStartedOn, rerun.RevisionHistoryStartedOn);
        Assert.Equal(3, await verifyDb.CaveRevisions.IgnoreQueryFilters().CountAsync());
    }



    [Fact]
    public async Task ConcurrentRolloutCommandsConvergeOnOneHistoryBoundary()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ConcurrentRolloutCommandsConvergeOnOneHistoryBoundary));
        var tenant = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        _ = await CaveTestDataFactory.AddCaveAsync(database, tenant, "legacy001a", "Legacy 1", 1);
        _ = await CaveTestDataFactory.AddCaveAsync(database, tenant, "legacy002a", "Legacy 2", 2);
        _ = await CaveTestDataFactory.AddCaveAsync(database, tenant, "legacy003a", "Legacy 3", 3);

        await using var firstDb = database.CreateDbContext("rollout-a", tenant.AccountId);
        await using var secondDb = database.CreateDbContext("rollout-b", tenant.AccountId);
        var results = await Task.WhenAll(
            new CaveRevisionRolloutRepository(firstDb).InitializeAsync(),
            new CaveRevisionRolloutRepository(secondDb).InitializeAsync());

        Assert.Equal(3, results.Sum(result => result.InitializedCaveCount));
        Assert.Single(results.Where(result => !result.AlreadyComplete));
        Assert.Single(results.Where(result => result.AlreadyComplete));
        Assert.Single(results.Select(result => result.RevisionHistoryStartedOn).Distinct());

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(3, await verify.CaveRevisions.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await verify.CaveRevisions.IgnoreQueryFilters()
            .Select(revision => revision.CreatedOn).Distinct().CountAsync());
        Assert.Equal(0, await verify.Caves.IgnoreQueryFilters().CountAsync(cave => cave.CurrentRevisionId == null));
    }

    [Fact]
    public async Task RolloutResumesPartialInitializationUsingTheOriginalHistoryBoundary()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RolloutResumesPartialInitializationUsingTheOriginalHistoryBoundary));
        var tenant = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var first = await CaveTestDataFactory.AddCaveAsync(database, tenant, "legacy001a", "Legacy First", 1);
        var second = await CaveTestDataFactory.AddCaveAsync(database, tenant, "legacy002a", "Legacy Second", 2);
        var boundary = new DateTime(2026, 10, 25, 17, 30, 0, DateTimeKind.Utc);

        await using (var seed = database.CreateDbContext("partial-rollout", tenant.AccountId))
        {
            var snapshot = await new CavePublishedSnapshotRepository(seed, seed.RequestUser).BuildAsync(first.CaveId);
            var revision = new CaveRevision
            {
                Id = "rollbase01",
                AccountId = tenant.AccountId,
                CaveId = first.CaveId,
                Source = CaveRevisionSource.SystemBaseline,
                Operation = CaveRevisionOperation.Baseline,
                SnapshotSchemaVersion = snapshot.SchemaVersion,
                SnapshotJson = CaveSnapshotJson.Serialize(snapshot),
                CreatedOn = boundary
            };
            seed.CaveRevisions.Add(revision);
            await seed.SaveChangesAsync();
            // SaveChanges assigns its own CreatedOn; preserve the deployment boundary explicitly because the
            // production rollout writer bypasses the audit interceptor for this system-generated record.
            await seed.CaveRevisions.IgnoreQueryFilters().Where(row => row.Id == revision.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.CreatedOn, boundary));
            await seed.Caves.IgnoreQueryFilters().Where(cave => cave.Id == first.CaveId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(cave => cave.CurrentRevisionId, revision.Id));
        }

        await using var rolloutDb = database.CreateDbContext("resume", tenant.AccountId);
        var result = await new CaveRevisionRolloutRepository(rolloutDb).InitializeAsync();

        Assert.False(result.AlreadyComplete);
        Assert.Equal(1, result.InitializedCaveCount);
        Assert.Equal(1, result.BatchCount);
        Assert.Equal(boundary, result.RevisionHistoryStartedOn);

        var revisions = await rolloutDb.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
            .Where(revision => revision.CaveId == first.CaveId || revision.CaveId == second.CaveId)
            .OrderBy(revision => revision.CaveId)
            .ToListAsync();
        Assert.Equal(2, revisions.Count);
        Assert.All(revisions, revision => Assert.Equal(boundary, revision.CreatedOn));
        Assert.All(revisions, revision => Assert.Equal(CaveRevisionOperation.Baseline, revision.Operation));
        Assert.NotNull((await rolloutDb.Caves.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(cave => cave.Id == second.CaveId)).CurrentRevisionId);
    }

    [Fact]
    public async Task RolloutRefusesToCreateALateBaselineAfterPublishedHistoryHasStarted()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RolloutRefusesToCreateALateBaselineAfterPublishedHistoryHasStarted));
        var tenant = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var existing = await CaveTestDataFactory.AddCaveAsync(database, tenant, "legacy001a", "Existing", 1);

        string currentRevisionId;
        await using (var rolloutDb = database.CreateDbContext("rollout", tenant.AccountId))
        {
            var result = await new CaveRevisionRolloutRepository(rolloutDb).InitializeAsync();
            Assert.Equal(1, result.InitializedCaveCount);
            currentRevisionId = (await rolloutDb.Caves.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(cave => cave.Id == existing.CaveId)).CurrentRevisionId!;
        }

        await using (var manager = database.CreateDbContext("manager", tenant.AccountId))
        {
            var snapshots = new CavePublishedSnapshotRepository(manager, manager.RequestUser);
            var mutations = new CaveMutationRepository(manager, manager.RequestUser, snapshots);
            var update = await mutations.PublishExistingAsync(existing.CaveId, currentRevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, cave => cave.Name = "Edited after rollout");
            Assert.True(update.CreatedRevision);
        }

        var uninitialized = await CaveTestDataFactory.AddCaveAsync(
            database, tenant, "legacy002a", "Improper late legacy Cave", 2);

        await using (var rerunDb = database.CreateDbContext("rerun", tenant.AccountId))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new CaveRevisionRolloutRepository(rerunDb).InitializeAsync());
            Assert.Contains("history is already active", error.Message, StringComparison.Ordinal);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Null((await verify.Caves.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(cave => cave.Id == uninitialized.CaveId)).CurrentRevisionId);
        Assert.Equal(2, await verify.CaveRevisions.IgnoreQueryFilters().CountAsync());
        Assert.Empty(await verify.CaveRevisions.IgnoreQueryFilters()
            .Where(revision => revision.CaveId == uninitialized.CaveId).ToListAsync());
    }

}
