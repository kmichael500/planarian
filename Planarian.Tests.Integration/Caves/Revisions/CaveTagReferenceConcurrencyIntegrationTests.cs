using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Tags.Repositories;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

using Planarian.Tests;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveTagReferenceConcurrencyIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Theory]
    [InlineData(TagTypeKeyConstant.People, "people000a", true)]
    [InlineData(TagTypeKeyConstant.Biology, "biology00a", false)]
    public async Task RecordedExistingReferenceDeletedBeforeLockFailsWithoutPartialPublication(
        string key, string tagId, bool people)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(RecordedExistingReferenceDeletedBeforeLockFailsWithoutPartialPublication)}_{key}");
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await ReferenceTestData.AddTagAsync(database, cave.AccountId, key, "Selected Reference", tagId);
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        await CavePermissions.GrantManagerAsync(database, cave, "reviewer");
        string requestId;
        await using (var contributor = await CaveTestActor.CreateAsync(database, cave.AccountId, "contributor"))
        {
            var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Proposed name");
            if (people) values.CartographerNameTagIds = [tagId];
            else values.BiologyTagIds = [tagId];
            requestId = await contributor.ChangeRequests.CreateAsync(cave.CaveId, values, cave.RevisionId, default);
        }

        await AssertDisappearingReferenceFailsAsync(database, cave, requestId, tagId);
    }

    [Fact]
    public async Task NewPeopleIntentMatchedBeforeLockDoesNotResurrectDisappearedCandidate()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(NewPeopleIntentMatchedBeforeLockDoesNotResurrectDisappearedCandidate));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        await CavePermissions.GrantManagerAsync(database, cave, "reviewer");
        string requestId;
        await using (var contributor = await CaveTestActor.CreateAsync(database, cave.AccountId, "contributor"))
        {
            var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Proposed name");
            values.CartographerNameTagIds = ["Jane Doe"];
            requestId = await contributor.ChangeRequests.CreateAsync(cave.CaveId, values, cave.RevisionId, default);
        }
        const string candidateId = "people000b";
        await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.People, "Jane Doe", candidateId);

        await AssertDisappearingReferenceFailsAsync(database, cave, requestId, candidateId);
    }

    [Fact]
    public async Task PublishedMutationKeyShareBlocksMergeUntilMutationCommitsThenMergePublishesNextRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PublishedMutationKeyShareBlocksMergeUntilMutationCommitsThenMergePublishesNextRevision));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Concurrent Source", "biology00a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Concurrent Destination", "biology00b");
        await CavePermissions.GrantManagerAsync(database, cave, "writer");

        var holdReferenceLock = new HoldAcquiredTagReferenceLockInterceptor();
        await using var writerDb = database.CreateDbContext("writer", cave.AccountId, holdReferenceLock);
        await CavePermissions.EnsureAccountUserAsync(writerDb, cave.AccountId);
        await writerDb.RequestUser.Initialize(cave.AccountId, writerDb.RequestUser.Id);
        var writerServices = IntegrationTestServices.For(writerDb);
        var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Writer committed first");
        values.BiologyTagIds = [source.Id];
        var writer = writerServices.Caves.AddCave(values, default);
        await holdReferenceLock.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var mergeDb = database.CreateDbContext("manager", cave.AccountId);
        await mergeDb.Database.OpenConnectionAsync();
        int mergeBackendPid;
        await using (var pidCommand = mergeDb.Database.GetDbConnection().CreateCommand())
        {
            pidCommand.CommandText = "select pg_backend_pid()";
            mergeBackendPid = Convert.ToInt32(await pidCommand.ExecuteScalarAsync());
        }
        var snapshots = new CavePublishedSnapshotRepository(mergeDb, mergeDb.RequestUser);
        var mergeRepository = new TagTypeMergeExecutionRepository(mergeDb, mergeDb.RequestUser,
            new TagReferenceLockRepository(mergeDb, mergeDb.RequestUser), snapshots,
            new CaveBulkRevisionRepository(mergeDb, mergeDb.RequestUser));
        var merge = mergeRepository.ExecuteAsync([source.Id], destination.Id);
        await WaitForLockWaitAsync(database, cave.AccountId, mergeBackendPid);

        holdReferenceLock.Resume.TrySetResult();
        await writer;
        string writerRevisionId;
        await using (var afterWriter = database.CreateDbContext("after-writer", cave.AccountId))
            writerRevisionId = (await afterWriter.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId))
                .CurrentRevisionId!;
        await merge;

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal(destination.Id, (await verify.BiologyTags.SingleAsync()).TagTypeId);
        var currentRevisionId = (await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId))
            .CurrentRevisionId;
        var mergeRevision = await verify.CaveRevisions.SingleAsync(revision => revision.Id == currentRevisionId);
        Assert.Equal(writerRevisionId, mergeRevision.PreviousRevisionId);
        Assert.Equal(CaveRevisionSource.ManagerEdit, mergeRevision.Source);
    }

    [Fact]
    public async Task RemovingPublishedSourceLocksOldIdentityAndWaitingMergeDoesNotReintroduceIt()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RemovingPublishedSourceLocksOldIdentityAndWaitingMergeDoesNotReintroduceIt));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Removal Source", "biology00a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Removal Destination", "biology00b");
        await CavePermissions.GrantManagerAsync(database, cave, "writer");
        string beforeRevision;
        await using (var seed = database.CreateDbContext("seed-source", cave.AccountId))
        {
            seed.BiologyTags.Add(new BiologyTag
                { Id = IdGenerator.Generate(), CaveId = cave.CaveId, TagTypeId = source.Id });
            await seed.SaveChangesAsync();
            var mutations = new CaveMutationRepository(seed, seed.RequestUser,
                new CavePublishedSnapshotRepository(seed, seed.RequestUser));
            beforeRevision = (await mutations.PublishExistingAsync(cave.CaveId, cave.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, _ => { })).RevisionId!;
        }

        var holdReferenceLock = new HoldAcquiredTagReferenceLockInterceptor();
        await using var writerDb = database.CreateDbContext("writer", cave.AccountId, holdReferenceLock);
        await CavePermissions.EnsureAccountUserAsync(writerDb, cave.AccountId);
        await writerDb.RequestUser.Initialize(cave.AccountId, writerDb.RequestUser.Id);
        var values = CaveChangeRequestTestSupport.PublishableValues(
            cave with { RevisionId = beforeRevision }, quality.Id, "Removed source");
        values.BiologyTagIds = [];
        var writer = IntegrationTestServices.For(writerDb).Caves.AddCave(values, default);
        await holdReferenceLock.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Contains(source.Id, holdReferenceLock.LockedIds);

        await using var mergeDb = database.CreateDbContext("manager", cave.AccountId);
        await mergeDb.Database.OpenConnectionAsync();
        int mergeBackendPid;
        await using (var command = mergeDb.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "select pg_backend_pid()";
            mergeBackendPid = Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        var mergeRepository = new TagTypeMergeExecutionRepository(mergeDb, mergeDb.RequestUser,
            new TagReferenceLockRepository(mergeDb, mergeDb.RequestUser),
            new CavePublishedSnapshotRepository(mergeDb, mergeDb.RequestUser),
            new CaveBulkRevisionRepository(mergeDb, mergeDb.RequestUser));
        var merge = mergeRepository.ExecuteAsync([source.Id], destination.Id);
        await WaitForLockWaitAsync(database, cave.AccountId, mergeBackendPid);

        holdReferenceLock.Resume.TrySetResult();
        await writer;
        await merge;

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Empty(await verify.BiologyTags.ToListAsync());
        var currentRevisionId = await verify.Caves.IgnoreQueryFilters().Where(row => row.Id == cave.CaveId)
            .Select(row => row.CurrentRevisionId).SingleAsync();
        var writerRevision = await verify.CaveRevisions.SingleAsync(revision => revision.Id == currentRevisionId);
        Assert.Equal(beforeRevision, writerRevision.PreviousRevisionId);
        Assert.Equal("Removed source", CaveSnapshotJson.Deserialize(writerRevision.SnapshotJson, 1).Name);
        Assert.False(await verify.CaveRevisions.AnyAsync(revision =>
            revision.PreviousRevisionId == currentRevisionId));
    }

    private static async Task AssertDisappearingReferenceFailsAsync(PostgresTestDatabase database,
        PublishedCaveTestData cave, string requestId, string tagId)
    {
        var pause = new PauseTagReferenceLockInterceptor();
        await using var reviewerDb = database.CreateDbContext("reviewer", cave.AccountId, pause);
        await CavePermissions.EnsureAccountUserAsync(reviewerDb, cave.AccountId);
        await reviewerDb.RequestUser.Initialize(cave.AccountId, reviewerDb.RequestUser.Id);
        var services = IntegrationTestServices.For(reviewerDb);
        var versionId = await CaveChangeRequestTestSupport.CurrentVersionAsync(reviewerDb, requestId);
        var approval = services.CaveChangeRequests.ApproveAsync(requestId, versionId, null, default);
        await pause.LockReached.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using (var competing = database.CreateDbContext("delete-tag", cave.AccountId))
        {
            competing.TagTypes.Remove(await competing.TagTypes.SingleAsync(tag => tag.Id == tagId));
            await competing.SaveChangesAsync();
        }
        pause.Resume.TrySetResult();
        await Assert.ThrowsAsync<ApiException>(() => approval);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var persisted = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        Assert.Equal(cave.CaveName, persisted.Name);
        Assert.Equal(cave.RevisionId, persisted.CurrentRevisionId);
        Assert.Equal(CaveChangeRequestStatus.Pending,
            (await verify.CaveChangeRequests.SingleAsync(request => request.Id == requestId)).Status);
        Assert.False(await verify.CaveRevisions.AnyAsync(revision => revision.PreviousRevisionId == cave.RevisionId));
        Assert.False(await verify.TagTypes.IgnoreQueryFilters().AnyAsync(tag => tag.Id == tagId));
    }

    private static async Task WaitForLockWaitAsync(PostgresTestDatabase database, string accountId, int backendPid)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var observer = database.CreateDbContext("tag-lock-observer", accountId);
            await observer.Database.OpenConnectionAsync();
            await using var command = observer.Database.GetDbConnection().CreateCommand();
            command.CommandText = "select \"wait_event_type\" from pg_stat_activity where pid = @pid";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "pid";
            parameter.Value = backendPid;
            command.Parameters.Add(parameter);
            if (string.Equals(await command.ExecuteScalarAsync() as string, "Lock", StringComparison.Ordinal)) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("Tag merge did not reach the expected PostgreSQL TagType lock wait.");
    }
}

internal sealed class PauseTagReferenceLockInterceptor : DbCommandInterceptor
{
    private int _paused;
    public TaskCompletionSource LockReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("from \"TagTypes\"", StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains("for key share", StringComparison.OrdinalIgnoreCase) &&
            Interlocked.Exchange(ref _paused, 1) == 0)
        {
            LockReached.TrySetResult();
            await Resume.Task.WaitAsync(cancellationToken);
        }
        return result;
    }
}

internal sealed class HoldAcquiredTagReferenceLockInterceptor : DbCommandInterceptor
{
    private int _paused;
    public TaskCompletionSource LockAcquired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public IReadOnlyList<string> LockedIds { get; private set; } = [];

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
        CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("from \"TagTypes\"", StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains("for key share", StringComparison.OrdinalIgnoreCase) &&
            Interlocked.Exchange(ref _paused, 1) == 0)
        {
            LockedIds = command.Parameters.Cast<DbParameter>()
                .Where(parameter => parameter.ParameterName == "tag_type_ids")
                .SelectMany(parameter => parameter.Value as string[] ?? []).ToList();
            LockAcquired.TrySetResult();
            await Resume.Task.WaitAsync(cancellationToken);
        }
        return result;
    }
}
