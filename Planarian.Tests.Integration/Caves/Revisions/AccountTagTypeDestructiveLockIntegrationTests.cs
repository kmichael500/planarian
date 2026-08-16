using Microsoft.EntityFrameworkCore;
using Npgsql;
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

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class AccountTagTypeDestructiveLockIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task DeleteNormalizesIdsAndDeletesOnlyAccountOwnedTags()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DeleteNormalizesIdsAndDeletesOnlyAccountOwnedTags));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var first = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "First", "biology00b");
        var second = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Second", "biology00a");
        var global = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Global", "biology00c", isDefault: true);

        await using (var db = database.CreateDbContext("manager", cave.AccountId))
        {
            var deleted = await new TagTypeDeleteExecutionRepository(db, db.RequestUser)
                .ExecuteAsync([first.Id, $" {second.Id} ", first.Id, global.Id, "missing"]);
            Assert.Equal(2, deleted);
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.False(await verify.TagTypes.IgnoreQueryFilters().AnyAsync(tag => tag.Id == first.Id));
        Assert.False(await verify.TagTypes.IgnoreQueryFilters().AnyAsync(tag => tag.Id == second.Id));
        Assert.True(await verify.TagTypes.IgnoreQueryFilters().AnyAsync(tag => tag.Id == global.Id));
    }

    [Fact]
    public async Task DeleteUsesDestructiveLockAndWaitsForPublishedReferenceWriter()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DeleteUsesDestructiveLockAndWaitsForPublishedReferenceWriter));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var tag = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Waiting delete", "biology00a");

        await using var referenceDb = database.CreateDbContext("writer", cave.AccountId);
        await using var referenceTransaction = await referenceDb.Database.BeginTransactionAsync();
        Assert.Single(await new TagReferenceLockRepository(referenceDb, referenceDb.RequestUser)
            .LockForReferenceAsync([tag.Id]));

        await using var deleteDb = database.CreateDbContext("manager", cave.AccountId);
        await deleteDb.Database.OpenConnectionAsync();
        await using var pidCommand = deleteDb.Database.GetDbConnection().CreateCommand();
        pidCommand.CommandText = "select pg_backend_pid()";
        var deletePid = Convert.ToInt32(await pidCommand.ExecuteScalarAsync());
        var deletion = new TagTypeDeleteExecutionRepository(deleteDb, deleteDb.RequestUser)
            .ExecuteAsync([tag.Id]);
        await WaitForLockWaitAsync(database, cave.AccountId, deletePid);

        await referenceTransaction.CommitAsync();
        Assert.Equal(1, await deletion);
        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.False(await verify.TagTypes.IgnoreQueryFilters().AnyAsync(row => row.Id == tag.Id));
    }

    [Fact]
    public async Task DeletingAlreadyReferencedTagReturnsControlledInUseErrorAndRollsBack()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DeletingAlreadyReferencedTagReturnsControlledInUseErrorAndRollsBack));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var tag = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Referenced", "biology00a");
        var referencedRevisionId = await AttachBiologyAndPublishAsync(database, cave, tag.Id);

        await using (var db = database.CreateDbContext("manager", cave.AccountId))
        {
            var exception = await Assert.ThrowsAsync<ApiException>(() =>
                new TagTypeDeleteExecutionRepository(db, db.RequestUser).ExecuteAsync([tag.Id]));
            Assert.Equal(400, exception.StatusCode);
            Assert.Equal("Cannot delete tag type because it is in use.", exception.Message);
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.True(await verify.TagTypes.IgnoreQueryFilters().AnyAsync(row => row.Id == tag.Id));
        Assert.True(await verify.BiologyTags.AnyAsync(row => row.CaveId == cave.CaveId && row.TagTypeId == tag.Id));
        Assert.Equal(referencedRevisionId,
            (await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId)).CurrentRevisionId);
    }

    [Fact]
    public async Task RealCaveWriterBlocksDeleteThenCommittedReferenceMakesDeleteFailCleanly()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RealCaveWriterBlocksDeleteThenCommittedReferenceMakesDeleteFailCleanly));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var tag = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Writer target", "biology00a");
        await CavePermissions.GrantManagerAsync(database, cave, "writer");

        var pause = new HoldAcquiredTagReferenceLockInterceptor();
        await using var writerDb = database.CreateDbContext("writer", cave.AccountId, pause);
        await CavePermissions.EnsureAccountUserAsync(writerDb, cave.AccountId);
        await writerDb.RequestUser.Initialize(cave.AccountId, writerDb.RequestUser.Id);
        var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Writer committed");
        values.BiologyTagIds = [tag.Id];
        var writer = IntegrationTestServices.For(writerDb).Caves.AddCave(values, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var deleteDb = database.CreateDbContext("manager", cave.AccountId);
        await deleteDb.Database.OpenConnectionAsync();
        await using var pidCommand = deleteDb.Database.GetDbConnection().CreateCommand();
        pidCommand.CommandText = "select pg_backend_pid()";
        var deletePid = Convert.ToInt32(await pidCommand.ExecuteScalarAsync());
        var deletion = new TagTypeDeleteExecutionRepository(deleteDb, deleteDb.RequestUser).ExecuteAsync([tag.Id]);
        await WaitForLockWaitAsync(database, cave.AccountId, deletePid);

        pause.Resume.TrySetResult();
        await writer;
        var exception = await Assert.ThrowsAsync<ApiException>(() => deletion);
        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("Cannot delete tag type because it is in use.", exception.Message);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.True(await verify.TagTypes.IgnoreQueryFilters().AnyAsync(row => row.Id == tag.Id));
        Assert.True(await verify.BiologyTags.AnyAsync(row => row.CaveId == cave.CaveId && row.TagTypeId == tag.Id));
        var persisted = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        Assert.NotEqual(cave.RevisionId, persisted.CurrentRevisionId);
        var writerRevision = await verify.CaveRevisions.SingleAsync(row => row.Id == persisted.CurrentRevisionId);
        Assert.Equal(cave.RevisionId, writerRevision.PreviousRevisionId);
        Assert.Equal(CaveRevisionSource.ManagerEdit, writerRevision.Source);
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(row => row.CaveId == cave.CaveId));
    }

    private static async Task<string> AttachBiologyAndPublishAsync(PostgresTestDatabase database,
        PublishedCaveTestData cave, string tagId)
    {
        await using var db = database.CreateDbContext("attach-biology", cave.AccountId);
        db.BiologyTags.Add(new BiologyTag
            { Id = IdGenerator.Generate(), CaveId = cave.CaveId, TagTypeId = tagId });
        await db.SaveChangesAsync();
        var mutation = await new CaveMutationRepository(db, db.RequestUser,
                new CavePublishedSnapshotRepository(db, db.RequestUser))
            .PublishExistingAsync(cave.CaveId, cave.RevisionId, CaveRevisionSource.ManagerEdit,
                CaveRevisionOperation.Update, _ => { });
        return mutation.RevisionId!;
    }

    private static async Task WaitForLockWaitAsync(PostgresTestDatabase database, string accountId, int backendPid)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var observer = database.CreateDbContext("delete-lock-observer", accountId);
            await observer.Database.OpenConnectionAsync();
            await using var command = observer.Database.GetDbConnection().CreateCommand();
            command.CommandText = "select \"wait_event_type\" from pg_stat_activity where pid = @pid";
            command.Parameters.Add(new NpgsqlParameter<int>("pid", backendPid));
            if (string.Equals(await command.ExecuteScalarAsync() as string, "Lock", StringComparison.Ordinal)) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("TagType deletion did not wait on the expected reference lock.");
    }
}
