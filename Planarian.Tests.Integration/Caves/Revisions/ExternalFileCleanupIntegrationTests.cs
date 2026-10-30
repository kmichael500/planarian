using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Repositories;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class ExternalFileCleanupIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AmbiguousStagedObjectCreateDoesNotLeaveUntrackedBytes(bool deletionFails)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(AmbiguousStagedObjectCreateDoesNotLeaveUntrackedBytes)}_{deletionFails}");
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Other", "other0000a");
        var blobs = new TestFileBlobStore { FailAfterCreate = true, FailDeletes = deletionFails };

        await using (var author = await CaveTestActor.CreateAsync(database, tenant.AccountId, "owner", blobs))
        {
            await using var content = new MemoryStream("persisted despite lost response"u8.ToArray());
            await Assert.ThrowsAsync<IOException>(() => author.Services.Files.StageAuthoringFile(
                content, "ambiguous.pdf", default));
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Empty(await verify.Files.Where(file => file.AccountId == tenant.AccountId).ToListAsync());
        var debt = await verify.StorageCleanupTargets
            .Where(target => target.AccountId == tenant.AccountId &&
                             target.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval)
            .ToListAsync();
        if (deletionFails)
        {
            var target = Assert.Single(debt);
            Assert.True(blobs.Contains(target.StoragePartition, target.StorageKey));
            Assert.Equal("persisted despite lost response"u8.ToArray(),
                blobs.Read(target.StoragePartition, target.StorageKey));

            blobs.FailDeletes = false;
            await using var retry = await CaveTestActor.CreateAsync(database, tenant.AccountId, "owner", blobs);
            await retry.Services.Files.CleanupUnpublishedFileRemovalTargetsAsync([target.Id], default);
        }
        else
        {
            Assert.Empty(debt);
        }
        Assert.Empty(blobs.Keys);
        verify.ChangeTracker.Clear();
        Assert.False(await verify.StorageCleanupTargets.AnyAsync(target =>
            target.AccountId == tenant.AccountId && target.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval));
    }

    [Fact]
    public async Task AmbiguousDirectCaveUploadCannotPublishOrLoseCleanupResponsibility()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AmbiguousDirectCaveUploadCannotPublishOrLoseCleanupResponsibility));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Other", "other0000a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");
        var blobs = new TestFileBlobStore { FailAfterCreate = true, FailDeletes = true };

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager", blobs))
        {
            await using var content = new MemoryStream("direct upload payload"u8.ToArray());
            await Assert.ThrowsAsync<IOException>(() =>
                manager.Services.Files.UploadCaveFile(content, tenant.CaveId, "uncertain.pdf", default));
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.False(await verify.Files.AnyAsync(row => row.AccountId == tenant.AccountId &&
                                                     row.CaveId == tenant.CaveId));
        Assert.Equal(tenant.RevisionId, await verify.Caves.IgnoreQueryFilters()
            .Where(row => row.AccountId == tenant.AccountId && row.Id == tenant.CaveId)
            .Select(row => row.CurrentRevisionId).SingleAsync());
        Assert.Single(await verify.CaveRevisions.IgnoreQueryFilters()
            .Where(row => row.AccountId == tenant.AccountId && row.CaveId == tenant.CaveId).ToListAsync());

        var target = Assert.Single(await verify.StorageCleanupTargets.Where(row =>
            row.AccountId == tenant.AccountId &&
            row.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval).ToListAsync());
        Assert.Equal("direct upload payload"u8.ToArray(),
            blobs.Read(target.StoragePartition, target.StorageKey));

        blobs.FailDeletes = false;
        await using var retry = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager", blobs);
        await retry.Services.Files.CleanupUnpublishedFileRemovalTargetsAsync([target.Id], default);
        Assert.Empty(blobs.Keys);
        verify.ChangeTracker.Clear();
        Assert.False(await verify.StorageCleanupTargets.AnyAsync(row => row.Id == target.Id));
    }

    [Fact]
    public async Task LostCommitAcknowledgementDoesNotDeletePublishedFileOrReplayTrackedMutation()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(LostCommitAcknowledgementDoesNotDeletePublishedFileOrReplayTrackedMutation));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Other", "othercommit");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");
        var blobs = new TestFileBlobStore();
        var lostCommit = new LoseFirstCommitAcknowledgementInterceptor();
        var bytes = "published even without acknowledgement"u8.ToArray();

        await using (var db = database.CreateDbContext("manager", tenant.AccountId, lostCommit))
        {
            await CavePermissions.AuthenticateAsync(db, tenant.AccountId);
            await using var content = new MemoryStream(bytes);
            await Assert.ThrowsAnyAsync<Exception>(() =>
                IntegrationTestServices.For(db, blobs).Files.UploadCaveFile(
                    content, tenant.CaveId, "lost-commit.pdf", default));
        }

        Assert.True(lostCommit.Fired);
        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var file = Assert.Single(await verify.Files.IgnoreQueryFilters()
            .Where(row => row.AccountId == tenant.AccountId && row.CaveId == tenant.CaveId)
            .ToListAsync());
        Assert.Equal(bytes, blobs.Read(file.BlobContainer!, file.BlobKey!));
        Assert.Single(blobs.Keys);
        Assert.Equal(0, blobs.DeleteIfExistsAttempts);
        Assert.Equal(2, await verify.CaveRevisions.IgnoreQueryFilters()
            .CountAsync(row => row.AccountId == tenant.AccountId && row.CaveId == tenant.CaveId));
        Assert.False(await verify.StorageCleanupTargets.AnyAsync(row =>
            row.AccountId == tenant.AccountId && row.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval));
    }

    [Fact]
    public async Task LostStagingSaveAcknowledgementPreservesCommittedFileAndObject()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(LostStagingSaveAcknowledgementPreservesCommittedFileAndObject));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Other", "otherstage");
        var blobs = new TestFileBlobStore();
        var lostSave = new LoseFirstFileSaveAcknowledgementInterceptor();
        var bytes = "stage survived acknowledgement loss"u8.ToArray();

        await using (var db = database.CreateDbContext("owner", tenant.AccountId, lostSave))
        {
            await CavePermissions.EnsureAccountUserAsync(db, tenant.AccountId);
            await CavePermissions.AuthenticateAsync(db, tenant.AccountId);
            await using var content = new MemoryStream(bytes);
            await Assert.ThrowsAsync<IOException>(() =>
                IntegrationTestServices.For(db, blobs).Files.StageAuthoringFile(
                    content, "lost-stage.pdf", default));
        }

        Assert.True(lostSave.Fired);
        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var file = Assert.Single(await verify.Files.IgnoreQueryFilters()
            .Where(row => row.AccountId == tenant.AccountId && row.CaveId == null)
            .ToListAsync());
        Assert.NotNull(file.ExpiresOn);
        Assert.Equal(bytes, blobs.Read(file.BlobContainer!, file.BlobKey!));
        Assert.Single(blobs.Keys);
        Assert.Equal(0, blobs.DeleteIfExistsAttempts);
        Assert.False(await verify.StorageCleanupTargets.AnyAsync(row =>
            row.AccountId == tenant.AccountId && row.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval));
    }

    [Fact]
    public async Task CleanupAcknowledgementDoesNotDetachUnsavedCleanupDebt()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CleanupAcknowledgementDoesNotDetachUnsavedCleanupDebt));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await using var actor = await CaveTestActor.CreateAsync(
            database, tenant.AccountId, "owner");

        var unsaved = new StorageCleanupTarget
        {
            AccountId = tenant.AccountId,
            StoragePartition = "portable-store",
            StorageKey = "objects/files/unsaved-cleanup",
            Purpose = StorageCleanupPurpose.UnpublishedFileRemoval
        };
        actor.Db.StorageCleanupTargets.Add(unsaved);

        var repository = new FileRepository(actor.Db, actor.Db.RequestUser);
        var deleted = await repository.CompleteStorageCleanupTargetsAsync(
            [StorageCleanupPurpose.UnpublishedFileRemoval], [unsaved.Id], default);

        Assert.Equal(0, deleted);
        Assert.Equal(EntityState.Added, actor.Db.Entry(unsaved).State);

        await actor.Db.SaveChangesAsync();
        var persistedId = unsaved.Id;
        actor.Db.ChangeTracker.Clear();
        Assert.True(await actor.Db.StorageCleanupTargets.AnyAsync(row => row.Id == persistedId));
    }

    [Fact]
    public async Task ExplicitCleanupProcessesEveryTargetInBoundedDatabaseBatches()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ExplicitCleanupProcessesEveryTargetInBoundedDatabaseBatches));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");
        var blobs = new TestFileBlobStore();
        const int targetCount = 501;
        const string partition = "bulk-cleanup";
        var targetIds = Enumerable.Range(0, targetCount)
            .Select(index => $"cl{index:D8}")
            .ToArray();

        await using (var seed = database.CreateDbContext("manager", tenant.AccountId))
        {
            for (var index = 0; index < targetCount; index++)
            {
                var key = $"objects/files/cleanup-{index:D4}";
                seed.StorageCleanupTargets.Add(new StorageCleanupTarget
                {
                    Id = targetIds[index],
                    AccountId = tenant.AccountId,
                    StoragePartition = partition,
                    StorageKey = key,
                    Purpose = StorageCleanupPurpose.CaveAssetRemoval
                });
                blobs.Seed(partition, key, [1]);
            }

            await seed.SaveChangesAsync();
        }

        var counter = new DbCommandCounter();
        await using var cleanup = database.CreateDbContext("manager", tenant.AccountId, counter);
        await CavePermissions.AuthenticateAsync(cleanup, tenant.AccountId);
        counter.Reset();

        await IntegrationTestServices.For(cleanup, blobs).Files
            .CleanupAssetRemovalTargetsAsync(targetIds, default);

        var cleanupCommandCount = counter.Commands.Count;
        Assert.Equal(targetCount, blobs.DeleteIfExistsAttempts);
        Assert.Empty(blobs.Keys);
        Assert.InRange(cleanupCommandCount, 1, 12);

        cleanup.ChangeTracker.Clear();
        Assert.False(await cleanup.StorageCleanupTargets.AsNoTracking()
            .AnyAsync(target => target.Purpose == StorageCleanupPurpose.CaveAssetRemoval));
    }

    [Fact]
    public async Task ManualUnpublishedFileDeleteKeepsDurableDebtWhenObjectDeletionFails()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ManualUnpublishedFileDeleteKeepsDurableDebtWhenObjectDeletionFails));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Other", "other0000a");
        var blobs = new TestFileBlobStore();

        await using var owner = await CaveTestActor.CreateAsync(
            database, tenant.AccountId, "owner", blobs);
        await using var content = new MemoryStream("manual staged bytes"u8.ToArray());
        var staged = await owner.Services.Files.StageAuthoringFile(
            content, "manual-delete.pdf", default);
        var row = await owner.Db.Files.AsNoTracking().SingleAsync(file => file.Id == staged.Id);
        var partition = row.BlobContainer!;
        var key = row.BlobKey!;

        blobs.FailDeletes = true;
        await owner.Services.Files.DeleteUnpublishedFileAsync(staged.Id, default);

        owner.Db.ChangeTracker.Clear();
        Assert.False(await owner.Db.Files.AnyAsync(file => file.Id == staged.Id));
        var target = Assert.Single(await owner.Db.StorageCleanupTargets.AsNoTracking()
            .Where(item => item.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval)
            .ToListAsync());
        Assert.Equal((partition, key), (target.StoragePartition, target.StorageKey));
        Assert.True(blobs.Contains(partition, key));

        blobs.FailDeletes = false;
        await owner.Services.Files.CleanupUnpublishedFileRemovalTargetsAsync([target.Id], default);

        Assert.DoesNotContain(owner.Db.ChangeTracker.Entries<StorageCleanupTarget>(),
            entry => entry.Entity.Id == target.Id);
        owner.Db.ChangeTracker.Clear();
        Assert.False(await owner.Db.StorageCleanupTargets.AnyAsync(item => item.Id == target.Id));
        Assert.False(blobs.Contains(partition, key));
    }

    [Fact]
    public async Task ExpiredUnpublishedFileCleanupKeepsDurableDebtWhenObjectDeletionFails()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ExpiredUnpublishedFileCleanupKeepsDurableDebtWhenObjectDeletionFails));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Other", "other0000a");
        var blobs = new TestFileBlobStore();

        await using var owner = await CaveTestActor.CreateAsync(
            database, tenant.AccountId, "owner", blobs);
        await using (var expiredContent = new MemoryStream("expired staged bytes"u8.ToArray()))
        {
            var staged = await owner.Services.Files.StageAuthoringFile(
                expiredContent, "expired.pdf", default);
            var row = await owner.Db.Files.SingleAsync(file => file.Id == staged.Id);
            row.ExpiresOn = DateTime.UtcNow.AddMinutes(-1);
            await owner.Db.SaveChangesAsync();

            var partition = row.BlobContainer!;
            var key = row.BlobKey!;
            blobs.FailDeletes = true;

            await using var nextContent = new MemoryStream("next staged bytes"u8.ToArray());
            await owner.Services.Files.StageAuthoringFile(nextContent, "next.pdf", default);

            owner.Db.ChangeTracker.Clear();
            Assert.False(await owner.Db.Files.AnyAsync(file => file.Id == staged.Id));
            var target = Assert.Single(await owner.Db.StorageCleanupTargets.AsNoTracking()
                .Where(item => item.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval)
                .ToListAsync());
            Assert.Equal((partition, key), (target.StoragePartition, target.StorageKey));
            Assert.True(blobs.Contains(partition, key));

            blobs.FailDeletes = false;
            await owner.Services.Files.CleanupUnpublishedFileRemovalTargetsAsync([target.Id], default);

            owner.Db.ChangeTracker.Clear();
            Assert.False(await owner.Db.StorageCleanupTargets.AnyAsync(item => item.Id == target.Id));
            Assert.False(blobs.Contains(partition, key));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HardCaveDeleteCommitsCleanupDebtBeforeObjectDeletionAndCanRetry(bool failObjectDelete)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(HardCaveDeleteCommitsCleanupDebtBeforeObjectDeletionAndCanRetry)}_{failObjectDelete}");
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Other", "other0000a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");
        var blobs = new TestFileBlobStore();

        await using var manager = await CaveTestActor.CreateAsync(
            database, tenant.AccountId, "manager", blobs);
        await using (var content = new MemoryStream("published cave bytes"u8.ToArray()))
            await manager.Services.Files.UploadCaveFile(
                content, tenant.CaveId, "published.pdf", default);

        var file = await manager.Db.Files.AsNoTracking()
            .SingleAsync(row => row.CaveId == tenant.CaveId);
        var partition = file.BlobContainer!;
        var key = file.BlobKey!;

        blobs.FailDeletes = failObjectDelete;
        await manager.Services.Caves.DeleteCave(tenant.CaveId, default);

        manager.Db.ChangeTracker.Clear();
        Assert.False(await manager.Db.Caves.IgnoreQueryFilters()
            .AnyAsync(cave => cave.Id == tenant.CaveId));
        Assert.False(await manager.Db.Files.IgnoreQueryFilters()
            .AnyAsync(row => row.Id == file.Id));
        var targets = await manager.Db.StorageCleanupTargets.AsNoTracking()
            .Where(item => item.Purpose == StorageCleanupPurpose.CaveAssetRemoval &&
                           item.StoragePartition == partition && item.StorageKey == key)
            .ToListAsync();
        var tombstone = await manager.Db.CaveRevisions.AsNoTracking()
            .Where(revision => revision.CaveId == tenant.CaveId)
            .OrderByDescending(revision => revision.CreatedOn)
            .ThenByDescending(revision => revision.Id)
            .FirstAsync();
        Assert.Equal(CaveRevisionOperation.Delete, tombstone.Operation);

        if (!failObjectDelete)
        {
            Assert.Empty(targets);
            Assert.False(blobs.Contains(partition, key));
            return;
        }

        var target = Assert.Single(targets);
        Assert.True(blobs.Contains(partition, key));
        blobs.FailDeletes = false;
        await manager.Services.Files.CleanupAssetRemovalTargetsAsync([target.Id], default);

        manager.Db.ChangeTracker.Clear();
        Assert.False(await manager.Db.StorageCleanupTargets.AnyAsync(item => item.Id == target.Id));
        Assert.False(blobs.Contains(partition, key));
    }
}

internal sealed class LoseFirstCommitAcknowledgementInterceptor : DbTransactionInterceptor
{
    public bool Fired { get; private set; }

    public override Task TransactionCommittedAsync(DbTransaction transaction,
        TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (!Fired)
        {
            Fired = true;
            throw new IOException("PostgreSQL committed, but the application lost the acknowledgement.");
        }
        return Task.CompletedTask;
    }
}

internal sealed class LoseFirstFileSaveAcknowledgementInterceptor : SaveChangesInterceptor
{
    public bool Fired { get; private set; }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        if (!Fired && result > 0 && eventData.Context?.ChangeTracker.Entries<Planarian.Model.Database.Entities.RidgeWalker.File>()
                .Any(entry => entry.Entity.BlobKey is not null) == true)
        {
            Fired = true;
            throw new IOException("PostgreSQL saved the File, but the application lost the acknowledgement.");
        }
        return ValueTask.FromResult(result);
    }
}
