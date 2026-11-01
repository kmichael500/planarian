using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Repositories;
using Planarian.Shared.Services;
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
    public async Task CreateOnlyCollisionNeverDeletesOtherBytesOrMutatesCave(bool directPublication)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(CreateOnlyCollisionNeverDeletesOtherBytesOrMutatesCave)}_{directPublication}");
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(
            database, cave.AccountId, TagTypeKeyConstant.File, "Other", "fileothera");
        if (directPublication) await CavePermissions.GrantManagerAsync(database, cave, "manager");

        var original = "already-owned immutable bytes"u8.ToArray();
        var blobs = new TestFileBlobStore();
        blobs.BeforeCreate = address => blobs.Seed(address.Partition, address.Key, original);
        await using var actor = await CaveTestActor.CreateAsync(
            database, cave.AccountId, directPublication ? "manager" : "owner", blobs);

        await Assert.ThrowsAsync<StorageObjectAlreadyExistsException>(async () =>
        {
            using var stream = new MemoryStream("should never replace bytes"u8.ToArray());
            if (directPublication)
                await actor.Services.Files.UploadCaveFile(stream, cave.CaveId, "collision.pdf", default);
            else
                await actor.Services.Files.StageAuthoringFile(stream, "collision.pdf", default);
        });

        actor.Db.ChangeTracker.Clear();
        var address = Assert.Single(blobs.Keys);
        Assert.Equal(original, blobs.Read(address.Container, address.Key));
        Assert.Equal(0, blobs.DeleteIfExistsAttempts);
        Assert.Empty(await actor.Db.Files.Where(row => row.AccountId == cave.AccountId).ToListAsync());
        Assert.Empty(await actor.Db.StorageCleanupTargets
            .Where(row => row.AccountId == cave.AccountId).ToListAsync());
        var unchanged = await actor.Db.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        Assert.Equal(cave.RevisionId, unchanged.CurrentRevisionId);
    }

    [Fact]
    public async Task DirectFileUploadRetriesOrdinaryCleanupDebt()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DirectFileUploadRetriesOrdinaryCleanupDebt));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.File, "Other", "other0000a");
        await CavePermissions.GrantManagerAsync(database, cave, "manager");
        var blobs = new TestFileBlobStore();
        var partition = RequestUser.BuildAccountContainerName(cave.AccountId);
        const string orphanKey = "objects/files/abandoned-object";
        blobs.Seed(partition, orphanKey, "unreferenced bytes"u8.ToArray());

        await using var manager = await CaveTestActor.CreateAsync(
            database, cave.AccountId, "manager", blobs);
        manager.Db.StorageCleanupTargets.Add(new StorageCleanupTarget
        {
            AccountId = cave.AccountId,
            StoragePartition = partition,
            StorageKey = orphanKey,
            Purpose = StorageCleanupPurpose.UnpublishedFileRemoval
        });
        await manager.Db.SaveChangesAsync();

        await using var upload = new MemoryStream("new cave file"u8.ToArray());
        var published = await manager.Services.Files.UploadCaveFile(
            upload, cave.CaveId, "current.pdf", default);

        manager.Db.ChangeTracker.Clear();
        Assert.False(blobs.Contains(partition, orphanKey));
        Assert.False(await manager.Db.StorageCleanupTargets.AnyAsync(target =>
            target.AccountId == cave.AccountId && target.StorageKey == orphanKey));
        Assert.True(await manager.Db.Files.AnyAsync(file =>
            file.Id == published.Id && file.CaveId == cave.CaveId));
    }

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
            await retry.Services.StorageCleanup.CleanupUnpublishedFileTargetsAsync([target.Id], default);
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
        await retry.Services.StorageCleanup.CleanupUnpublishedFileTargetsAsync([target.Id], default);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleCleanupDebtCannotDeleteLiveFileOrImportSource(bool isImportSource)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(StaleCleanupDebtCannotDeleteLiveFileOrImportSource)}_{isImportSource}");
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        string partition;
        string key;

        if (isImportSource)
        {
            partition = RequestUser.BuildAccountContainerName(tenant.AccountId);
            key = ImportSourceStoragePath.BuildCaveEntranceCsvKey("source0001");
            await using var seed = database.CreateDbContext("seed", tenant.AccountId);
            seed.CaveImportSources.Add(new CaveImportSource
            {
                Id = "source0001",
                AccountId = tenant.AccountId,
                FileName = "preserved.csv",
                BlobContainer = partition,
                BlobKey = key,
                ExpiresOn = null
            });
            await seed.SaveChangesAsync();
        }
        else
        {
            var file = await FileTestDataFactory.AddFileAsync(
                database, tenant, associateWithCave: true);
            partition = file.BlobContainer;
            key = file.BlobKey;
        }

        var blobs = new TestFileBlobStore();
        var liveBytes = "must stay intact"u8.ToArray();
        var orphanKey = "objects/files/unreferenced-object";
        blobs.Seed(partition, key, liveBytes);
        blobs.Seed(partition, orphanKey, "should be removed"u8.ToArray());

        await using var actor = await CaveTestActor.CreateAsync(
            database, tenant.AccountId, "owner", blobs);
        var liveDebt = new StorageCleanupTarget
        {
            AccountId = tenant.AccountId,
            StoragePartition = partition,
            StorageKey = key,
            Purpose = StorageCleanupPurpose.UnpublishedFileRemoval
        };
        var orphanDebt = new StorageCleanupTarget
        {
            AccountId = tenant.AccountId,
            StoragePartition = partition,
            StorageKey = orphanKey,
            Purpose = StorageCleanupPurpose.UnpublishedFileRemoval
        };
        actor.Db.StorageCleanupTargets.AddRange(liveDebt, orphanDebt);
        await actor.Db.SaveChangesAsync();

        await actor.Services.StorageCleanup.CleanupOrdinaryTargetsAsync(
            [liveDebt.Id, orphanDebt.Id], default);

        Assert.Equal(liveBytes, blobs.Read(partition, key));
        Assert.False(blobs.Contains(partition, orphanKey));
        Assert.Equal(1, blobs.DeleteIfExistsAttempts);
        actor.Db.ChangeTracker.Clear();
        Assert.False(await actor.Db.StorageCleanupTargets
            .AnyAsync(row => row.Id == liveDebt.Id || row.Id == orphanDebt.Id));
        if (isImportSource)
            Assert.True(await actor.Db.CaveImportSources.AnyAsync(row => row.Id == "source0001"));
        else
            Assert.True(await actor.Db.Files.AnyAsync(row => row.CaveId == tenant.CaveId));
    }

    [Fact]
    public async Task PersistentlyFailingCleanupTargetsDoNotStarveLaterTargets()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PersistentlyFailingCleanupTargetsDoNotStarveLaterTargets));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        var partition = RequestUser.BuildAccountContainerName(tenant.AccountId);
        await using var actor = await CaveTestActor.CreateAsync(
            database, tenant.AccountId, "owner", blobs);

        var targets = Enumerable.Range(0, 17).Select(index => new StorageCleanupTarget
        {
            AccountId = tenant.AccountId,
            StoragePartition = partition,
            StorageKey = $"objects/files/orphan-{index:D3}",
            Purpose = StorageCleanupPurpose.UnpublishedFileRemoval
        }).ToList();
        actor.Db.StorageCleanupTargets.AddRange(targets);
        await actor.Db.SaveChangesAsync();
        foreach (var target in targets)
            blobs.Seed(partition, target.StorageKey, "orphan bytes"u8.ToArray());

        // Match the actual ordering used by the bounded cleanup query rather
        // than relying on client-side insertion order or wall-clock granularity.
        var ordered = await actor.Db.StorageCleanupTargets.AsNoTracking()
            .OrderBy(target => target.CreatedOn).ThenBy(target => target.Id)
            .ToListAsync();
        foreach (var target in ordered.Take(16))
            blobs.FailDelete(partition, target.StorageKey);
        var later = ordered[16];

        await actor.Services.StorageCleanup.CleanupOrdinaryTargetsAsync();
        Assert.True(blobs.Contains(partition, later.StorageKey));

        await actor.Services.StorageCleanup.CleanupOrdinaryTargetsAsync();
        Assert.False(blobs.Contains(partition, later.StorageKey));
        actor.Db.ChangeTracker.Clear();
        Assert.False(await actor.Db.StorageCleanupTargets.AnyAsync(target => target.Id == later.Id));
        Assert.Equal(16, await actor.Db.StorageCleanupTargets.CountAsync());
        Assert.All(ordered.Take(16), target => Assert.True(blobs.Contains(partition, target.StorageKey)));
    }

    [Fact]
    public async Task CleanupCannotDeleteObjectOwnedByAnotherAccount()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CleanupCannotDeleteObjectOwnedByAnotherAccount));
        var firstAccount = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var otherAccount = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var otherFile = await FileTestDataFactory.AddFileAsync(
            database, otherAccount, associateWithCave: true);
        var blobs = new TestFileBlobStore();
        var originalBytes = "foreign tenant asset"u8.ToArray();
        blobs.Seed(otherFile.BlobContainer, otherFile.BlobKey, originalBytes);

        await using var actor = await CaveTestActor.CreateAsync(
            database, firstAccount.AccountId, "owner", blobs);
        var debt = new StorageCleanupTarget
        {
            AccountId = firstAccount.AccountId,
            StoragePartition = otherFile.BlobContainer,
            StorageKey = otherFile.BlobKey,
            Purpose = StorageCleanupPurpose.CaveAssetRemoval
        };
        actor.Db.StorageCleanupTargets.Add(debt);
        await actor.Db.SaveChangesAsync();

        await actor.Services.StorageCleanup.CleanupAssetRemovalTargetsAsync([debt.Id], default);

        Assert.Equal(originalBytes, blobs.Read(otherFile.BlobContainer, otherFile.BlobKey));
        Assert.Equal(0, blobs.DeleteIfExistsAttempts);
        actor.Db.ChangeTracker.Clear();
        Assert.False(await actor.Db.StorageCleanupTargets.AnyAsync(row => row.Id == debt.Id));
        Assert.True(await actor.Db.Files.IgnoreQueryFilters()
            .AnyAsync(row => row.AccountId == otherAccount.AccountId && row.Id == otherFile.FileId));
    }

    [Fact]
    public async Task ConcurrentCleanupQueueIsIdempotentWithinPurposeAndAccount()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ConcurrentCleanupQueueIsIdempotentWithinPurposeAndAccount));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var other = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var address = new StorageCleanupAddress("portable-store", "objects/files/missing");

        await using var first = await CaveTestActor.CreateAsync(database, tenant.AccountId, "owner");
        await using var second = await CaveTestActor.CreateAsync(database, tenant.AccountId, "owner");
        var firstRepository = new StorageCleanupRepository(first.Db, first.Db.RequestUser);
        var secondRepository = new StorageCleanupRepository(second.Db, second.Db.RequestUser);

        var concurrent = await Task.WhenAll(
            firstRepository.QueueAsync(StorageCleanupPurpose.UnpublishedFileRemoval, [address], default),
            secondRepository.QueueAsync(StorageCleanupPurpose.UnpublishedFileRemoval, [address], default));
        var firstTarget = Assert.Single(concurrent[0]);
        Assert.Equal(firstTarget.Id, Assert.Single(concurrent[1]).Id);
        Assert.Equal(firstTarget.Id, Assert.Single(await firstRepository.QueueAsync(
            StorageCleanupPurpose.UnpublishedFileRemoval, [address, address], default)).Id);

        var differentPurpose = Assert.Single(await firstRepository.QueueAsync(
            StorageCleanupPurpose.CaveAssetRemoval, [address], default));
        Assert.NotEqual(firstTarget.Id, differentPurpose.Id);

        await using var otherActor = await CaveTestActor.CreateAsync(
            database, other.AccountId, "owner");
        var otherTarget = Assert.Single(await new StorageCleanupRepository(
            otherActor.Db, otherActor.Db.RequestUser).QueueAsync(
                StorageCleanupPurpose.UnpublishedFileRemoval, [address], default));
        Assert.NotEqual(firstTarget.Id, otherTarget.Id);

        first.Db.ChangeTracker.Clear();
        var allTargets = await first.Db.StorageCleanupTargets.IgnoreQueryFilters()
            .Where(row => row.StoragePartition == address.StoragePartition &&
                          row.StorageKey == address.StorageKey)
            .ToListAsync();
        Assert.Equal(3, allTargets.Count);
        Assert.Equal(2, allTargets.Count(row => row.AccountId == tenant.AccountId));
        Assert.Single(allTargets.Where(row => row.AccountId == other.AccountId));
    }

    [Fact]
    public async Task CleanupAcknowledgementDetachesPersistedButNotUnrelatedTrackedDebt()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CleanupAcknowledgementDetachesPersistedButNotUnrelatedTrackedDebt));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await using var actor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "owner");

        var completed = new StorageCleanupTarget
        {
            AccountId = tenant.AccountId,
            StoragePartition = "test",
            StorageKey = "objects/files/completed",
            Purpose = StorageCleanupPurpose.UnpublishedFileRemoval
        };
        var unrelated = new StorageCleanupTarget
        {
            AccountId = tenant.AccountId,
            StoragePartition = "test",
            StorageKey = "objects/files/unrelated",
            Purpose = StorageCleanupPurpose.CaveAssetRemoval
        };
        actor.Db.StorageCleanupTargets.AddRange(completed, unrelated);
        await actor.Db.SaveChangesAsync();

        var repo = new StorageCleanupRepository(actor.Db, actor.Db.RequestUser);
        Assert.Equal(1, await repo.CompleteAsync(
            [StorageCleanupPurpose.UnpublishedFileRemoval], [completed.Id], default));

        Assert.Equal(EntityState.Detached, actor.Db.Entry(completed).State);
        Assert.Equal(EntityState.Unchanged, actor.Db.Entry(unrelated).State);
        Assert.False(await actor.Db.StorageCleanupTargets.AsNoTracking()
            .AnyAsync(row => row.Id == completed.Id));
        Assert.True(await actor.Db.StorageCleanupTargets.AsNoTracking()
            .AnyAsync(row => row.Id == unrelated.Id));
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

        var repository = new StorageCleanupRepository(actor.Db, actor.Db.RequestUser);
        var deleted = await repository.CompleteAsync(
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

        await IntegrationTestServices.For(cleanup, blobs).StorageCleanup
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
        await owner.Services.Files.DeleteAuthoringStagedFileAsync(staged.Id, default);

        owner.Db.ChangeTracker.Clear();
        Assert.False(await owner.Db.Files.AnyAsync(file => file.Id == staged.Id));
        var target = Assert.Single(await owner.Db.StorageCleanupTargets.AsNoTracking()
            .Where(item => item.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval)
            .ToListAsync());
        Assert.Equal((partition, key), (target.StoragePartition, target.StorageKey));
        Assert.True(blobs.Contains(partition, key));

        blobs.FailDeletes = false;
        await owner.Services.StorageCleanup.CleanupUnpublishedFileTargetsAsync([target.Id], default);

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
            await owner.Services.StorageCleanup.CleanupUnpublishedFileTargetsAsync([target.Id], default);

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
        await manager.Services.StorageCleanup.CleanupAssetRemovalTargetsAsync([target.Id], default);

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
