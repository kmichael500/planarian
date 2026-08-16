using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Planarian.Modules.Tags.Repositories;
using Xunit;

namespace Planarian.Tests;

public sealed class TagMergeLockOrderingIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task CaveImportWaitsForTagBeforeTakingCaveLockThenRejectsStalePlan()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CaveImportWaitsForTagBeforeTakingCaveLockThenRejectsStalePlan));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Geology, "Lock Source", "geology00a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Geology, "Lock Destination", "geology00b");
        cave = cave with { RevisionId = await AttachGeologyAndPublishAsync(database, cave, source.Id) };

        CaveImportPlan plan;
        await using (var planning = database.CreateDbContext("planner", cave.AccountId))
            plan = await new CaveImportTestHarness(planning, planning.RequestUser).PlanCsvAsync(
                ImportDryRunIntegrationTests.CaveHeader + "\n" +
                "Cave A,County A,A01,1,AA,,,,,,,1,Lock Source,,,,,,,false,,\n", true);

        var pause = new HoldDestructiveTagLockInterceptor();
        await using var mergeDb = database.CreateDbContext("manager", cave.AccountId, pause);
        var merge = MergeRepository(mergeDb).ExecuteAsync([source.Id], destination.Id);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var importDb = database.CreateDbContext("importer", cave.AccountId);
        await importDb.Database.OpenConnectionAsync();
        var importPid = await BackendPidAsync(importDb);
        var import = new CaveImportTestHarness(importDb, importDb.RequestUser).ExecuteAsync(plan, "ordered.csv");
        await WaitForLockWaitAsync(database, cave.AccountId, importPid);
        await AssertCaveLockAvailableNowaitAsync(database, cave.AccountId, cave.CaveId);

        pause.Resume.TrySetResult();
        await merge;
        await Assert.ThrowsAsync<CaveRevisionConflictException>(() => import);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal(destination.Id, (await verify.GeologyTags.SingleAsync()).TagTypeId);
        Assert.False(await verify.CaveImportBatches.AnyAsync(batch => batch.SourceFileName == "ordered.csv"));
    }

    [Fact]
    public async Task EntranceImportWaitsForTagBeforeTakingCaveLockThenRejectsStalePlan()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(EntranceImportWaitsForTagBeforeTakingCaveLockThenRejectsStalePlan));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.EntranceStatus, "Open", "status000a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.EntranceStatus, "Restricted", "status000b");
        await EntranceTestData.AddEntranceAsync(database, cave, "entrance0a",
            locationQualityTagId: quality.Id, entranceStatusTagId: source.Id);
        cave = cave with { RevisionId = await PublishCurrentAsync(database, cave) };

        EntranceImportPlan plan;
        await using (var planning = database.CreateDbContext("planner", cave.AccountId))
            plan = await new EntranceImportTestHarness(planning, planning.RequestUser).PlanCsvAsync(
                ImportDryRunIntegrationTests.EntranceHeader + "\n" +
                "Planned,A01,1,false,35,-86,500,Survey Grade,0,Open,,,,,,\n", false);

        var pause = new HoldDestructiveTagLockInterceptor();
        await using var mergeDb = database.CreateDbContext("manager", cave.AccountId, pause);
        var merge = MergeRepository(mergeDb).ExecuteAsync([source.Id], destination.Id);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var importDb = database.CreateDbContext("importer", cave.AccountId);
        await importDb.Database.OpenConnectionAsync();
        var importPid = await BackendPidAsync(importDb);
        var import = new EntranceImportTestHarness(importDb, importDb.RequestUser)
            .ExecuteAsync(plan, "entrance-ordered.csv");
        await WaitForLockWaitAsync(database, cave.AccountId, importPid);
        await AssertCaveLockAvailableNowaitAsync(database, cave.AccountId, cave.CaveId);

        pause.Resume.TrySetResult();
        await merge;
        await Assert.ThrowsAsync<CaveRevisionConflictException>(() => import);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal(destination.Id, (await verify.EntranceStatusTags.SingleAsync()).TagTypeId);
        Assert.False(await verify.CaveImportBatches.AnyAsync(batch =>
            batch.SourceFileName == "entrance-ordered.csv"));
    }

    [Fact]
    public async Task CaveImportRemovingSourceHoldsCaveWhileMergeWaitsThenMergeDoesNotReintroduceDestination()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CaveImportRemovingSourceHoldsCaveWhileMergeWaitsThenMergeDoesNotReintroduceDestination));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Geology, "Removal Source", "geology00a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Geology, "Removal Destination", "geology00b");
        cave = cave with { RevisionId = await AttachGeologyAndPublishAsync(database, cave, source.Id) };

        CaveImportPlan plan;
        var row = string.Join(',', new[] { "Cave A", "County A", "A01", "1", "AA", "", "", "", "", "", "",
            "0", "", "", "", "", "", "", "", "false", "", "" });
        await using (var planning = database.CreateDbContext("planner", cave.AccountId))
            plan = await new CaveImportTestHarness(planning, planning.RequestUser).PlanCsvAsync(
                ImportDryRunIntegrationTests.CaveHeader + "\n" + row + "\n", true);

        var pause = new HoldImportAfterCaveLockInterceptor();
        await using var importDb = database.CreateDbContext("importer", cave.AccountId, pause);
        var import = new CaveImportTestHarness(importDb, importDb.RequestUser)
            .ExecuteAsync(plan, "remove-source.csv");
        await pause.CaveLockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.DoesNotContain(source.Id, pause.ReferenceLockedIds);

        await using var mergeDb = database.CreateDbContext("manager", cave.AccountId);
        await mergeDb.Database.OpenConnectionAsync();
        var mergePid = await BackendPidAsync(mergeDb);
        var merge = MergeRepository(mergeDb).ExecuteAsync([source.Id], destination.Id);
        await WaitForLockWaitAsync(database, cave.AccountId, mergePid);

        pause.Resume.TrySetResult();
        await import;
        await merge;

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Empty(await verify.GeologyTags.Where(tag => tag.CaveId == cave.CaveId).ToListAsync());
        var currentRevisionId = await verify.Caves.IgnoreQueryFilters().Where(row => row.Id == cave.CaveId)
            .Select(row => row.CurrentRevisionId).SingleAsync();
        Assert.NotEqual(cave.RevisionId, currentRevisionId);
        Assert.Equal(3, await verify.CaveRevisions.CountAsync(row => row.CaveId == cave.CaveId));
        Assert.Equal(CaveRevisionSource.Import,
            (await verify.CaveRevisions.SingleAsync(row => row.Id == currentRevisionId)).Source);
    }

    [Fact]
    public async Task EntranceImportRemovingSourceHoldsCaveWhileMergeWaitsThenMergeDoesNotReintroduceDestination()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(EntranceImportRemovingSourceHoldsCaveWhileMergeWaitsThenMergeDoesNotReintroduceDestination));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.EntranceStatus, "Removal Source", "status000a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.EntranceStatus, "Removal Destination", "status000b");
        await EntranceTestData.AddEntranceAsync(database, cave, "entrance0a",
            locationQualityTagId: quality.Id, entranceStatusTagId: source.Id);
        cave = cave with { RevisionId = await PublishCurrentAsync(database, cave) };

        EntranceImportPlan plan;
        var row = string.Join(',', new[] { "Planned", "A01", "1", "true", "35", "-86", "500", "Survey Grade",
            "0", "", "", "", "", "", "" });
        await using (var planning = database.CreateDbContext("planner", cave.AccountId))
            plan = await new EntranceImportTestHarness(planning, planning.RequestUser).PlanCsvAsync(
                ImportDryRunIntegrationTests.EntranceHeader + "\n" + row + "\n", true);

        var pause = new HoldImportAfterCaveLockInterceptor();
        await using var importDb = database.CreateDbContext("importer", cave.AccountId, pause);
        var import = new EntranceImportTestHarness(importDb, importDb.RequestUser)
            .ExecuteAsync(plan, "entrance-remove-source.csv");
        await pause.CaveLockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.DoesNotContain(source.Id, pause.ReferenceLockedIds);

        await using var mergeDb = database.CreateDbContext("manager", cave.AccountId);
        await mergeDb.Database.OpenConnectionAsync();
        var mergePid = await BackendPidAsync(mergeDb);
        var merge = MergeRepository(mergeDb).ExecuteAsync([source.Id], destination.Id);
        await WaitForLockWaitAsync(database, cave.AccountId, mergePid);

        pause.Resume.TrySetResult();
        await import;
        await merge;

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Empty(await verify.EntranceStatusTags.ToListAsync());
        var currentRevisionId = await verify.Caves.IgnoreQueryFilters().Where(row => row.Id == cave.CaveId)
            .Select(row => row.CurrentRevisionId).SingleAsync();
        Assert.NotEqual(cave.RevisionId, currentRevisionId);
        Assert.Equal(3, await verify.CaveRevisions.CountAsync(row => row.CaveId == cave.CaveId));
        Assert.Equal(CaveRevisionSource.Import,
            (await verify.CaveRevisions.SingleAsync(row => row.Id == currentRevisionId)).Source);
    }

    private static TagTypeMergeExecutionRepository MergeRepository(
        Planarian.Model.Database.PlanarianDbContext db) => new(db, db.RequestUser,
        new TagReferenceLockRepository(db, db.RequestUser),
        new CavePublishedSnapshotRepository(db, db.RequestUser),
        new CaveBulkRevisionRepository(db, db.RequestUser));

    private static async Task<string> AttachGeologyAndPublishAsync(PostgresTestDatabase database,
        PublishedCaveTestData cave, string tagId)
    {
        await using var db = database.CreateDbContext("attach-geology", cave.AccountId);
        db.GeologyTags.Add(new GeologyTag
            { Id = IdGenerator.Generate(), CaveId = cave.CaveId, TagTypeId = tagId });
        await db.SaveChangesAsync();
        return await PublishCurrentAsync(database, cave, db);
    }

    private static async Task<string> PublishCurrentAsync(PostgresTestDatabase database,
        PublishedCaveTestData cave, Planarian.Model.Database.PlanarianDbContext? existing = null)
    {
        await using var owned = existing is null ? database.CreateDbContext("publish-current", cave.AccountId) : null;
        var db = existing ?? owned!;
        var mutations = new CaveMutationRepository(db, db.RequestUser,
            new CavePublishedSnapshotRepository(db, db.RequestUser));
        return (await mutations.PublishExistingAsync(cave.CaveId, cave.RevisionId,
            CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, _ => { })).RevisionId!;
    }

    private static async Task<int> BackendPidAsync(Planarian.Model.Database.PlanarianDbContext db)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "select pg_backend_pid()";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task WaitForLockWaitAsync(PostgresTestDatabase database, string accountId, int backendPid)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var observer = database.CreateDbContext("order-observer", accountId);
            await observer.Database.OpenConnectionAsync();
            await using var command = observer.Database.GetDbConnection().CreateCommand();
            command.CommandText = "select \"wait_event_type\" from pg_stat_activity where pid = @pid";
            command.Parameters.Add(new NpgsqlParameter<int>("pid", backendPid));
            if (string.Equals(await command.ExecuteScalarAsync() as string, "Lock", StringComparison.Ordinal)) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("Import did not wait on the expected TagType lock.");
    }

    private static async Task AssertCaveLockAvailableNowaitAsync(PostgresTestDatabase database,
        string accountId, string caveId)
    {
        await using var observer = database.CreateDbContext("cave-lock-probe", accountId);
        await using var transaction = await observer.Database.BeginTransactionAsync();
        await observer.Caves.FromSqlInterpolated(
            $"select *, xmin from \"Caves\" where \"AccountId\" = {accountId} and \"Id\" = {caveId} for update nowait")
            .IgnoreQueryFilters().AsNoTracking().SingleAsync();
        await transaction.RollbackAsync();
    }
}

internal sealed class HoldDestructiveTagLockInterceptor : DbCommandInterceptor
{
    private int _paused;
    public TaskCompletionSource LockAcquired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
        CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("from \"TagTypes\"", StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains("for update", StringComparison.OrdinalIgnoreCase) &&
            Interlocked.Exchange(ref _paused, 1) == 0)
        {
            LockAcquired.TrySetResult();
            await Resume.Task.WaitAsync(cancellationToken);
        }
        return result;
    }
}

internal sealed class HoldImportAfterCaveLockInterceptor : DbCommandInterceptor
{
    private int _paused;
    private readonly List<string> _referenceLockedIds = [];
    public TaskCompletionSource CaveLockAcquired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public IReadOnlyList<string> ReferenceLockedIds => _referenceLockedIds;

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        RecordReferenceIds(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        RecordReferenceIds(command);
        if (command.CommandText.Contains("\"CurrentRevisionId\"", StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains("from \"Caves\"", StringComparison.OrdinalIgnoreCase) &&
            Interlocked.Exchange(ref _paused, 1) == 0)
        {
            CaveLockAcquired.TrySetResult();
            await Resume.Task.WaitAsync(cancellationToken);
        }
        return result;
    }

    private void RecordReferenceIds(DbCommand command)
    {
        if (!command.CommandText.Contains("for key share", StringComparison.OrdinalIgnoreCase)) return;
        _referenceLockedIds.AddRange(command.Parameters.Cast<DbParameter>()
            .Where(parameter => parameter.ParameterName == "tag_type_ids")
            .SelectMany(parameter => parameter.Value as string[] ?? []));
    }
}
