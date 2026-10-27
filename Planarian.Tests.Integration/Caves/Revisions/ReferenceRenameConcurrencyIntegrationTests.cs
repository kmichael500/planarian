using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Caves.Revisions;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Concurrency;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

using Planarian.Tests;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class ReferenceRenameConcurrencyIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task TagRenameWinsThenWaitingCaveWriterRejectsStaleRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TagRenameWinsThenWaitingCaveWriterRejectsStaleRevision));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var biology = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Cricket", "biology00a");
        cave = cave with { RevisionId = await AttachBiologyAndPublishAsync(database, cave, biology.Id) };
        await CavePermissions.GrantManagerAsync(database, cave, "writer");

        var pause = new HoldDestructiveTagLockInterceptor();
        await using var admin = database.CreateDbContext("manager", cave.AccountId, pause);
        var rename = IntegrationTestServices.For(admin).Account.CreateOrUpdateTagType(
            new CreateEditTagTypeVm { Name = "Cave Cricket", Key = TagTypeKeyConstant.Biology },
            biology.Id, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var writer = database.CreateDbContext("writer", cave.AccountId);
        await CavePermissions.AuthenticateAsync(writer, cave.AccountId);
        await writer.Database.OpenConnectionAsync();
        var writerPid = await PostgresLockAssertions.BackendPidAsync(writer);
        var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Stale writer");
        values.BiologyTagIds = [biology.Id];
        var write = IntegrationTestServices.For(writer).Caves.AddCave(values, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, writerPid, write);

        pause.Resume.TrySetResult();
        await rename;
        await Assert.ThrowsAsync<CaveRevisionConflictException>(() => write);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal("Cave Cricket",
            await verify.TagTypes.Where(row => row.Id == biology.Id).Select(row => row.Name).SingleAsync());
        var persisted = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        Assert.Equal(cave.CaveName, persisted.Name);
        var renameRevision = await verify.CaveRevisions.SingleAsync(row => row.Id == persisted.CurrentRevisionId);
        Assert.Equal(cave.RevisionId, renameRevision.PreviousRevisionId);
        Assert.Equal(CaveRevisionSource.ManagerEdit, renameRevision.Source);
    }

    [Fact]
    public async Task CaveWriterWinsThenWaitingTagRenamePublishesNextRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CaveWriterWinsThenWaitingTagRenamePublishesNextRevision));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var biology = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Cricket", "biology00a");
        await CavePermissions.GrantManagerAsync(database, cave, "writer");

        var pause = new HoldAcquiredTagReferenceLockInterceptor();
        await using var writer = database.CreateDbContext("writer", cave.AccountId, pause);
        await CavePermissions.AuthenticateAsync(writer, cave.AccountId);
        var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Writer committed first");
        values.BiologyTagIds = [biology.Id];
        var write = IntegrationTestServices.For(writer).Caves.AddCave(values, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var admin = database.CreateDbContext("manager", cave.AccountId);
        await admin.Database.OpenConnectionAsync();
        var adminPid = await PostgresLockAssertions.BackendPidAsync(admin);
        var rename = IntegrationTestServices.For(admin).Account.CreateOrUpdateTagType(
            new CreateEditTagTypeVm { Name = "Cave Cricket", Key = TagTypeKeyConstant.Biology },
            biology.Id, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, adminPid, rename);

        pause.Resume.TrySetResult();
        await write;
        string writerRevisionId;
        await using (var afterWriter = database.CreateDbContext("after-writer", cave.AccountId))
            writerRevisionId = await afterWriter.Caves.IgnoreQueryFilters()
                .Where(row => row.Id == cave.CaveId).Select(row => row.CurrentRevisionId!).SingleAsync();

        await rename;

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var persisted = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        Assert.Equal("Writer committed first", persisted.Name);
        Assert.NotEqual(writerRevisionId, persisted.CurrentRevisionId);
        var renameRevision = await verify.CaveRevisions.SingleAsync(row => row.Id == persisted.CurrentRevisionId);
        Assert.Equal(writerRevisionId, renameRevision.PreviousRevisionId);
        var snapshot = CaveSnapshotJson.Deserialize(renameRevision.SnapshotJson, renameRevision.SnapshotSchemaVersion);
        Assert.Contains(snapshot.Tags,
            tag => tag.TagTypeId == biology.Id && tag.NameAtRevision == "Cave Cricket");
    }

    [Fact]
    public async Task CountyRenameWinsThenWaitingCaveWriterRejectsStaleRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CountyRenameWinsThenWaitingCaveWriterRejectsStaleRevision));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantManagerAsync(database, cave, "writer");

        var pause = new HoldCountyLockInterceptor("for update");
        await using var admin = database.CreateDbContext("manager", cave.AccountId, pause);
        var rename = IntegrationTestServices.For(admin).Account.CreateOrUpdateCounty(
            cave.StateId,
            new CreateCountyVm { Name = "Renamed County", CountyDisplayId = "R99" },
            cave.CountyId, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var writer = database.CreateDbContext("writer", cave.AccountId);
        await CavePermissions.AuthenticateAsync(writer, cave.AccountId);
        await writer.Database.OpenConnectionAsync();
        var writerPid = await PostgresLockAssertions.BackendPidAsync(writer);
        var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Stale writer");
        var write = IntegrationTestServices.For(writer).Caves.AddCave(values, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, writerPid, write);

        pause.Resume.TrySetResult();
        await rename;
        await Assert.ThrowsAsync<CaveRevisionConflictException>(() => write);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var county = await verify.Counties.SingleAsync(row => row.Id == cave.CountyId);
        Assert.Equal("Renamed County", county.Name);
        Assert.Equal("R99", county.DisplayId);
        var persisted = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        Assert.Equal(cave.CaveName, persisted.Name);
        var renameRevision = await verify.CaveRevisions.SingleAsync(row => row.Id == persisted.CurrentRevisionId);
        Assert.Equal(cave.RevisionId, renameRevision.PreviousRevisionId);
        Assert.Equal(CaveRevisionSource.ManagerEdit, renameRevision.Source);
    }

    private static async Task<string> AttachBiologyAndPublishAsync(
        PostgresTestDatabase database, PublishedCaveTestData cave, string tagId)
    {
        await using var db = database.CreateDbContext("attach-biology", cave.AccountId);
        db.BiologyTags.Add(new BiologyTag
            { Id = IdGenerator.Generate(), CaveId = cave.CaveId, TagTypeId = tagId });
        await db.SaveChangesAsync();
        return (await new CaveMutationRepository(db, db.RequestUser,
                new CavePublishedSnapshotRepository(db, db.RequestUser))
            .PublishExistingAsync(cave.CaveId, cave.RevisionId, CaveRevisionSource.ManagerEdit,
                CaveRevisionOperation.Update, _ => { })).RevisionId!;
    }
}
