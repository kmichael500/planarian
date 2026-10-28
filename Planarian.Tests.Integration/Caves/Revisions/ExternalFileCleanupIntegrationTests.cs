using Microsoft.EntityFrameworkCore;
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
