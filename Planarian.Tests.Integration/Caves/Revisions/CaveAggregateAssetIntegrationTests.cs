using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Controllers;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Concurrency;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveAggregateAssetIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    private const string EmptyFeatureCollection = "{\"type\":\"FeatureCollection\",\"features\":[]}";

    [Fact]
    public async Task StaleDirectManagerEditCannotOverwriteANewerRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(StaleDirectManagerEditCannotOverwriteANewerRevision));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager-a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager-b");

        var first = PublishableValues(tenant, quality.Id, "First manager wins");
        var stale = PublishableValues(tenant, quality.Id, "Stale manager loses");

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager-a"))
            await manager.Services.Caves.AddCave(first, default);

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager-b"))
        {
            var conflict = await Assert.ThrowsAsync<CaveRevisionConflictException>(() =>
                manager.Services.Caves.AddCave(stale, default));
            Assert.Equal(tenant.RevisionId, conflict.ExpectedRevisionId);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.Equal("First manager wins", cave.Name);
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
    }

    [Fact]
    public async Task DirectManagerEditRequiresOwningItsTransaction()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(DirectManagerEditRequiresOwningItsTransaction));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager"))
        await using (var outer = await manager.Db.Database.BeginTransactionAsync())
        {
            var values = PublishableValues(tenant, quality.Id, "Must not publish inside caller transaction");
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                manager.Services.Caves.AddCave(values, default));
            Assert.Contains("requires ownership", exception.Message, StringComparison.OrdinalIgnoreCase);
            await outer.RollbackAsync();
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.Equal(tenant.CaveName, cave.Name);
        Assert.Equal(tenant.RevisionId, cave.CurrentRevisionId);
        Assert.Single(await verify.CaveRevisions.Where(row => row.CaveId == tenant.CaveId).ToListAsync());
    }

    [Fact]
    public async Task RacingDirectManagerEditsReturnRevisionConflictAndCommitOnlyWinner()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RacingDirectManagerEditsReturnRevisionConflictAndCommitOnlyWinner));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager-a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager-b");

        var pausedValues = PublishableValues(tenant, quality.Id, "Paused manager loses");
        var winningValues = PublishableValues(tenant, quality.Id, "Concurrent manager wins");
        pausedValues.IsCountyNumberManuallySet = false;
        winningValues.IsCountyNumberManuallySet = false;

        var pause = new PauseCaveUpdateInterceptor();
        await using var pausedDb = database.CreateDbContext("manager-a", tenant.AccountId, pause);
        await CavePermissions.AuthenticateAsync(pausedDb, tenant.AccountId);
        var pausedSave = IntegrationTestServices.For(pausedDb).Caves.AddCave(pausedValues, default);
        await pause.CaveUpdateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await using (var winnerDb = database.CreateDbContext("manager-b", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(winnerDb, tenant.AccountId);
            await IntegrationTestServices.For(winnerDb).Caves.AddCave(winningValues, default);
        }

        pause.ContinueUpdate.TrySetResult();
        var conflict = await Assert.ThrowsAsync<CaveRevisionConflictException>(() => pausedSave);
        Assert.Equal(tenant.RevisionId, conflict.ExpectedRevisionId);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.Equal("Concurrent manager wins", cave.Name);
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
    }

    [Fact]
    public async Task DirectLinePlotSaveUsesStableIdentityAndCanonicalNoOpDoesNotAddARevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DirectLinePlotSaveUsesStableIdentityAndCanonicalNoOpDoesNotAddARevision));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");

        string publishedRevisionId;
        string linePlotId;
        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager"))
        {
            var context = await manager.Services.Caves.GetEditAuthoringContextAsync(tenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            values.LinePlots = [new() { Name = "Survey line", GeoJson = EmptyFeatureCollection }];
            await manager.Services.Caves.AddCave(values, default);

            var savedContext = await manager.Services.Caves.GetEditAuthoringContextAsync(tenant.CaveId, default);
            publishedRevisionId = savedContext.Cave.CurrentRevisionId!;
            var savedLinePlot = Assert.Single(savedContext.LinePlots);
            linePlotId = savedLinePlot.Id!;

            var noOp = ValuesFromCave(savedContext.Cave);
            noOp.LinePlots = [new()
            {
                Id = linePlotId,
                Name = "Survey line",
                GeoJson = "{ \"features\" : [ ], \"type\" : \"FeatureCollection\" }"
            }];
            await manager.Services.Caves.AddCave(noOp, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.Equal(publishedRevisionId, cave.CurrentRevisionId);
        var linePlot = await verify.CaveGeoJsons.SingleAsync(row => row.CaveId == tenant.CaveId);
        Assert.Equal(linePlotId, linePlot.Id);
        Assert.Equal("{\"features\":[],\"type\":\"FeatureCollection\"}", CaveJsonContent.Normalize(linePlot.GeoJson));
        var revision = await verify.CaveRevisions.SingleAsync(row => row.Id == publishedRevisionId);
        var snapshotLinePlot = Assert.Single(CaveSnapshotJson.Deserialize(revision.SnapshotJson, 1).LinePlots);
        Assert.Equal(linePlotId, snapshotLinePlot.Id);
    }

    [Fact]
    public async Task PublishedFileRemovalDeletesBytesAfterCommitButPreservesHistoricalMetadata()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PublishedFileRemovalDeletesBytesAfterCommitButPreservesHistoricalMetadata));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true, fileId: "remove000a");
        var blobs = new TestFileBlobStore();
        blobs.Seed("test", "seed-a", System.Text.Encoding.UTF8.GetBytes("published file bytes"));
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");

        string revisionWithFile;
        await using (var seed = database.CreateDbContext("file-revision-seed", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(seed, seed.RequestUser,
                new CavePublishedSnapshotRepository(seed, seed.RequestUser));
            revisionWithFile = (await mutations.PublishExistingAsync(tenant.CaveId, tenant.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, _ => { })).RevisionId!;
        }

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager", blobs))
        {
            var cave = await new CaveRepository(manager.Db, manager.Db.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(cave!);
            Assert.Equal(revisionWithFile, values.ExpectedRevisionId);
            values.Files = [];
            await manager.Services.Caves.AddCave(values, default);
        }

        await using var verify = database.CreateDbContext("verify-removal", tenant.AccountId);
        Assert.False(await verify.Files.AnyAsync(row => row.Id == file.FileId));
        Assert.False(blobs.Contains("test", "seed-a"));
        Assert.False(await verify.StorageCleanupTargets.AnyAsync(row =>
            row.Purpose == StorageCleanupPurpose.CaveAssetRemoval));

        var historical = await verify.CaveRevisions.SingleAsync(row => row.Id == revisionWithFile);
        var historicalFile = Assert.Single(CaveSnapshotJson.Deserialize(
            historical.SnapshotJson, historical.SnapshotSchemaVersion).Files);
        Assert.Equal(file.FileId, historicalFile.Id);
        Assert.Equal("seed-a", historicalFile.Name);

        var currentRevisionId = await verify.Caves.IgnoreQueryFilters()
            .Where(row => row.Id == tenant.CaveId).Select(row => row.CurrentRevisionId).SingleAsync();
        var current = await verify.CaveRevisions.SingleAsync(row => row.Id == currentRevisionId);
        Assert.Empty(CaveSnapshotJson.Deserialize(current.SnapshotJson, current.SnapshotSchemaVersion).Files);
    }

    [Fact]
    public async Task FailedPublishedFileCleanupRemainsDurableAndCanBeRetried()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(FailedPublishedFileCleanupRemainsDurableAndCanBeRetried));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true, fileId: "cleanup00a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");
        var blobs = new TestFileBlobStore { FailDeletes = true };
        blobs.Seed("test", "seed-a", [1, 2, 3]);

        await using (var seed = database.CreateDbContext("cleanup-revision-seed", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(seed, seed.RequestUser,
                new CavePublishedSnapshotRepository(seed, seed.RequestUser));
            tenant = tenant with { RevisionId = (await mutations.PublishExistingAsync(tenant.CaveId, tenant.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, _ => { })).RevisionId! };
        }

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager", blobs))
        {
            var cave = await new CaveRepository(manager.Db, manager.Db.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(cave!);
            values.Files = [];
            await manager.Services.Caves.AddCave(values, default);
        }

        string cleanupId;
        string revisionAfterRemoval;
        await using (var pending = database.CreateDbContext("verify-pending-cleanup", tenant.AccountId))
        {
            Assert.False(await pending.Files.AnyAsync(row => row.Id == file.FileId));
            var target = Assert.Single(await pending.StorageCleanupTargets.AsNoTracking()
                .Where(row => row.Purpose == StorageCleanupPurpose.CaveAssetRemoval).ToListAsync());
            Assert.Equal((tenant.AccountId, "test", "seed-a"),
                (target.AccountId, target.StoragePartition, target.StorageKey));
            cleanupId = target.Id;
            revisionAfterRemoval = (await pending.Caves.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(row => row.AccountId == tenant.AccountId && row.Id == tenant.CaveId)).CurrentRevisionId!;
        }
        Assert.True(blobs.Contains("test", "seed-a"));

        blobs.FailDeletes = false;
        await using (var retry = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager", blobs))
        {
            var cave = await new CaveRepository(retry.Db, retry.Db.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(cave!);
            Assert.Equal(revisionAfterRemoval, values.ExpectedRevisionId);
            await retry.Services.Caves.AddCave(values, default);
        }

        await using var verify = database.CreateDbContext("verify-cleaned", tenant.AccountId);
        Assert.False(await verify.StorageCleanupTargets.AnyAsync(row => row.Id == cleanupId));
        Assert.False(blobs.Contains("test", "seed-a"));
        Assert.Equal(revisionAfterRemoval, await verify.Caves.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.AccountId == tenant.AccountId && row.Id == tenant.CaveId)
            .Select(row => row.CurrentRevisionId).SingleAsync());
    }

    [Fact]
    public async Task PublishedFileRemovalKeepsOnlyFailedDebtWhenCleanupBatchIsPartiallySuccessful()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PublishedFileRemovalKeepsOnlyFailedDebtWhenCleanupBatchIsPartiallySuccessful));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");
        var blobs = new TestFileBlobStore();

        var fileIds = new List<string>();
        var addresses = new List<(string Partition, string Key)>();
        await using (var uploader = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager", blobs))
        {
            foreach (var fileName in new[] { "delete-success.pdf", "already-missing.pdf", "delete-fails.pdf" })
            {
                await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(fileName));
                var uploaded = await uploader.Services.Files.UploadCaveFile(
                    content, tenant.CaveId, fileName, default);
                var file = await uploader.Db.Files.AsNoTracking().SingleAsync(row => row.Id == uploaded.Id);
                fileIds.Add(file.Id);
                addresses.Add((file.BlobContainer!, file.BlobKey!));
            }
        }

        // Exercise three materially different outcomes in the same cleanup batch.
        blobs.Delete(addresses[1].Partition, addresses[1].Key);
        blobs.FailDelete(addresses[2].Partition, addresses[2].Key);

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager", blobs))
        {
            var cave = await new CaveRepository(manager.Db, manager.Db.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(cave!);
            Assert.NotNull(values.Files);
            Assert.Equal(fileIds.Order(StringComparer.Ordinal),
                values.Files!.Select(file => file.Id).Order(StringComparer.Ordinal));
            values.Files = [];
            await manager.Services.Caves.AddCave(values, default);
        }

        await using var verify = database.CreateDbContext("verify-partial-cleanup", tenant.AccountId);
        Assert.False(await verify.Files.AnyAsync(row => fileIds.Contains(row.Id)));
        Assert.False(blobs.Contains(addresses[0].Partition, addresses[0].Key));
        Assert.False(blobs.Contains(addresses[1].Partition, addresses[1].Key));
        Assert.True(blobs.Contains(addresses[2].Partition, addresses[2].Key));

        var pending = Assert.Single(await verify.StorageCleanupTargets.AsNoTracking()
            .Where(row => row.Purpose == StorageCleanupPurpose.CaveAssetRemoval)
            .ToListAsync());
        Assert.Equal((addresses[2].Partition, addresses[2].Key),
            (pending.StoragePartition, pending.StorageKey));

        var currentRevisionId = await verify.Caves.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.AccountId == tenant.AccountId && row.Id == tenant.CaveId)
            .Select(row => row.CurrentRevisionId).SingleAsync();
        var currentRevision = await verify.CaveRevisions.SingleAsync(row => row.Id == currentRevisionId);
        Assert.Empty(CaveSnapshotJson.Deserialize(
            currentRevision.SnapshotJson, currentRevision.SnapshotSchemaVersion).Files);
    }

    [Fact]
    public async Task CleanupAcknowledgementFailureLeavesDebtUntilIdempotentRetry()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CleanupAcknowledgementFailureLeavesDebtUntilIdempotentRetry));
        var tenant = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        const string partition = "cleanup-ack";
        const string key = "objects/files/already-deleted-on-ack-failure";
        blobs.Seed(partition, key, [1, 2, 3]);

        string cleanupId;
        await using (var seed = database.CreateDbContext("seed-cleanup-ack", tenant.AccountId))
        {
            var target = new StorageCleanupTarget
            {
                AccountId = tenant.AccountId,
                StoragePartition = partition,
                StorageKey = key,
                Purpose = StorageCleanupPurpose.CaveAssetRemoval
            };
            seed.StorageCleanupTargets.Add(target);
            await seed.SaveChangesAsync();
            cleanupId = target.Id;
        }

        var failAcknowledgement = new FailFirstStorageCleanupAcknowledgementInterceptor();
        await using (var cleanup = database.CreateDbContext(
                         "cleanup-ack", tenant.AccountId, failAcknowledgement))
        {
            await IntegrationTestServices.For(cleanup, blobs).Files
                .CleanupAssetRemovalTargetsAsync([cleanupId], default);
        }

        Assert.True(failAcknowledgement.Failed);
        Assert.False(blobs.Contains(partition, key));
        await using (var pending = database.CreateDbContext("verify-pending-ack", tenant.AccountId))
            Assert.True(await pending.StorageCleanupTargets.AnyAsync(row => row.Id == cleanupId));

        await using (var retry = database.CreateDbContext("retry-cleanup-ack", tenant.AccountId))
            await IntegrationTestServices.For(retry, blobs).Files
                .CleanupAssetRemovalTargetsAsync([cleanupId], default);

        await using var verify = database.CreateDbContext("verify-cleanup-ack", tenant.AccountId);
        Assert.False(await verify.StorageCleanupTargets.AnyAsync(row => row.Id == cleanupId));
        Assert.False(blobs.Contains(partition, key));
    }

    [Fact]
    public async Task StaleCleanupTargetCannotDeleteBytesReferencedByLiveFile()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StaleCleanupTargetCannotDeleteBytesReferencedByLiveFile));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "livefile0a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");
        var blobs = new TestFileBlobStore();
        blobs.Seed("test", "seed-a", [4, 5, 6]);

        string cleanupId;
        await using (var seed = database.CreateDbContext("seed-stale-cleanup", tenant.AccountId))
        {
            var target = new StorageCleanupTarget
            {
                AccountId = tenant.AccountId,
                StoragePartition = "test",
                StorageKey = "seed-a",
                Purpose = StorageCleanupPurpose.CaveAssetRemoval
            };
            seed.StorageCleanupTargets.Add(target);
            await seed.SaveChangesAsync();
            cleanupId = target.Id;
        }

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager", blobs))
            await manager.Services.Files.CleanupAssetRemovalTargetsAsync([cleanupId], default);

        await using var verify = database.CreateDbContext("verify-stale-cleanup", tenant.AccountId);
        Assert.True(await verify.Files.AnyAsync(row => row.Id == file.FileId));
        Assert.False(await verify.StorageCleanupTargets.AnyAsync(row => row.Id == cleanupId));
        Assert.True(blobs.Contains("test", "seed-a"));
        Assert.Equal(0, blobs.DeleteIfExistsAttempts);
    }

    [Fact]
    public async Task CleanupTargetCannotDeleteBytesReferencedByForeignAccountLiveFile()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CleanupTargetCannotDeleteBytesReferencedByForeignAccountLiveFile));
        var (tenantA, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var (tenantB, _) = await CreateMeasuredPublishedCaveAsync(database, 'b', null, null, null, null);
        var foreignFile = await FileTestDataFactory.AddFileAsync(database, tenantB, associateWithCave: true,
            fileId: "foreign00a");
        await CavePermissions.GrantManagerAsync(database, tenantA, "manager-a");
        var blobs = new TestFileBlobStore();
        blobs.Seed("shared-storage", "objects/files/shared", [7, 8, 9]);

        await using (var foreignSeed = database.CreateDbContext("seed-foreign-file", tenantB.AccountId))
        {
            var persistedForeign = await foreignSeed.Files
                .SingleAsync(row => row.Id == foreignFile.FileId && row.AccountId == tenantB.AccountId);
            persistedForeign.BlobContainer = "shared-storage";
            persistedForeign.BlobKey = "objects/files/shared";
            await foreignSeed.SaveChangesAsync();
        }

        string cleanupId;
        await using (var seed = database.CreateDbContext("seed-cross-account-cleanup", tenantA.AccountId))
        {
            var target = new StorageCleanupTarget
            {
                AccountId = tenantA.AccountId,
                StoragePartition = "shared-storage",
                StorageKey = "objects/files/shared",
                Purpose = StorageCleanupPurpose.CaveAssetRemoval
            };
            seed.StorageCleanupTargets.Add(target);
            await seed.SaveChangesAsync();
            cleanupId = target.Id;
        }

        await using (var actor = await CaveTestActor.CreateAsync(database, tenantA.AccountId, "manager-a", blobs))
            await actor.Services.Files.CleanupAssetRemovalTargetsAsync([cleanupId], default);

        await using var verify = database.CreateDbContext("verify-cross-account-cleanup", tenantA.AccountId);
        Assert.True(await verify.Files.IgnoreQueryFilters().AnyAsync(row =>
            row.Id == foreignFile.FileId && row.AccountId == tenantB.AccountId));
        Assert.False(await verify.StorageCleanupTargets.AnyAsync(row => row.Id == cleanupId));
        Assert.True(blobs.Contains("shared-storage", "objects/files/shared"));
        Assert.Equal(0, blobs.DeleteIfExistsAttempts);
    }

    [Fact]
    public async Task GenericAuthoringFilePublishesWithoutCopyingItsObject()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(GenericAuthoringFilePublishesWithoutCopyingItsObject));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");
        var blobs = new TestFileBlobStore();

        await using var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager", blobs);
        await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("one upload"));
        var staged = await manager.Services.Files.StageAuthoringFile(content, "survey.pdf", default);
        var before = await manager.Db.Files.AsNoTracking().SingleAsync(row => row.Id == staged.Id);
        Assert.Equal(("survey", ".pdf"), (before.Name, before.Extension));
        Assert.Null(before.CaveId);
        Assert.NotNull(before.ExpiresOn);
        var objectAddress = (before.BlobContainer!, before.BlobKey!);
        Assert.Equal($"objects/files/{staged.Id}", before.BlobKey);
        Assert.Single(blobs.Keys);

        var cave = await new CaveRepository(manager.Db, manager.Db.RequestUser).GetCave(tenant.CaveId);
        var values = ValuesFromCave(cave!);
        values.Files = [new EditFileMetadataVm
        {
            Id = staged.Id,
            FileTypeTagId = staged.FileTypeTagId,
            Name = staged.Name!
        }];
        await manager.Services.Caves.AddCave(values, default);
        manager.Db.ChangeTracker.Clear();

        var published = await manager.Db.Files.AsNoTracking().SingleAsync(row => row.Id == staged.Id);
        Assert.Equal(tenant.CaveId, published.CaveId);
        Assert.Null(published.ExpiresOn);
        Assert.Equal(objectAddress, (published.BlobContainer!, published.BlobKey!));
        Assert.Single(blobs.Keys);
        Assert.True(blobs.Contains(objectAddress.Item1, objectAddress.Item2));
    }

    [Fact]
    public async Task DirectFileRenameUpdatesNameAndPreservesExtensionFileTypeAndStorageIdentity()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DirectFileRenameUpdatesNameAndPreservesExtensionFileTypeAndStorageIdentity));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "rename000a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");

        await using (var seed = database.CreateDbContext("file-rename-baseline", tenant.AccountId))
        {
            var mutation = await new CaveMutationRepository(seed, seed.RequestUser,
                    new CavePublishedSnapshotRepository(seed, seed.RequestUser))
                .PublishExistingAsync(tenant.CaveId, tenant.RevisionId, CaveRevisionSource.ManagerEdit,
                    CaveRevisionOperation.Update, _ => { });
            tenant = tenant with { RevisionId = mutation.RevisionId! };
        }

        string originalPartition;
        string originalKey;
        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager"))
        {
            var original = await manager.Db.Files.AsNoTracking().SingleAsync(row => row.Id == file.FileId);
            originalPartition = original.BlobContainer!;
            originalKey = original.BlobKey!;
            var cave = await new CaveRepository(manager.Db, manager.Db.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(cave!);
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId,
                Name = "Entrance Survey",
                FileTypeTagId = original.FileTypeTagId
            }];
            await manager.Services.Caves.AddCave(values, default);
        }

        await using var verify = database.CreateDbContext("file-rename-verify", tenant.AccountId);
        var currentCave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        var currentFile = await verify.Files.AsNoTracking().SingleAsync(row => row.Id == file.FileId);
        Assert.Equal("Entrance Survey", currentFile.Name);
        Assert.Equal(".pdf", currentFile.Extension);
        Assert.Equal(file.FileId, currentFile.Id);
        Assert.Equal((originalPartition, originalKey), (currentFile.BlobContainer, currentFile.BlobKey));
        Assert.Equal(file.FileTypeId, currentFile.FileTypeTagId);

        var previous = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == tenant.RevisionId)).SnapshotJson, 1);
        var current = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == currentCave.CurrentRevisionId)).SnapshotJson, 1);
        var fileChange = Assert.Single(new CaveRevisionDiffService().Compare(previous, current).FileChanges);
        var scalar = Assert.Single(fileChange.Scalars);
        Assert.Equal(nameof(CaveFileSnapshotV1.Name), scalar.Key);
        Assert.Equal(("seed-a", "Entrance Survey"), (scalar.Value.Previous, scalar.Value.Current));
    }

    [Fact]
    public async Task DirectFileRenameRejectsANameThatNewlyIncludesItsFixedExtension()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DirectFileRenameRejectsANameThatNewlyIncludesItsFixedExtension));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "badname00a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");

        await using (var seed = database.CreateDbContext("file-name-validation-baseline", tenant.AccountId))
        {
            var mutation = await new CaveMutationRepository(seed, seed.RequestUser,
                    new CavePublishedSnapshotRepository(seed, seed.RequestUser))
                .PublishExistingAsync(tenant.CaveId, tenant.RevisionId, CaveRevisionSource.ManagerEdit,
                    CaveRevisionOperation.Update, _ => { });
            tenant = tenant with { RevisionId = mutation.RevisionId! };
        }

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager"))
        {
            var cave = await new CaveRepository(manager.Db, manager.Db.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(cave!);
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId,
                Name = "seed-a.pdf",
                FileTypeTagId = file.FileTypeId
            }];

            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                manager.Services.Caves.AddCave(values, default));
            Assert.Equal(400, failure.StatusCode);
            Assert.Contains("must not include its fixed extension", failure.Message, StringComparison.Ordinal);
        }

        await using var verify = database.CreateDbContext("file-name-validation-verify", tenant.AccountId);
        var unchangedFile = await verify.Files.AsNoTracking().SingleAsync(row => row.Id == file.FileId);
        Assert.Equal(("seed-a", ".pdf"), (unchangedFile.Name, unchangedFile.Extension));
        Assert.Equal(tenant.RevisionId, (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == tenant.CaveId)).CurrentRevisionId);
    }

    [Fact]
    public async Task DirectPublicationRejectsMissingStagedObjectBeforeMutatingCave()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DirectPublicationRejectsMissingStagedObjectBeforeMutatingCave));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");
        var blobs = new TestFileBlobStore();

        await using var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager", blobs);
        await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("missing after staging"));
        var staged = await manager.Services.Files.StageAuthoringFile(content, "missing.pdf", default);
        var stagedRow = await manager.Db.Files.AsNoTracking().SingleAsync(row => row.Id == staged.Id);
        blobs.Delete(stagedRow.BlobContainer!, stagedRow.BlobKey!);
        var revisionCountBefore = await manager.Db.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId);

        var cave = await new CaveRepository(manager.Db, manager.Db.RequestUser).GetCave(tenant.CaveId);
        var values = ValuesFromCave(cave!);
        values.Name = "Must not publish";
        values.Files = [new EditFileMetadataVm
        {
            Id = staged.Id,
            FileTypeTagId = staged.FileTypeTagId,
            Name = staged.Name!
        }];

        var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            manager.Services.Caves.AddCave(values, default));
        Assert.Contains("no longer available", failure.Message);

        manager.Db.ChangeTracker.Clear();
        var unchanged = await manager.Db.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.Equal(tenant.CaveName, unchanged.Name);
        Assert.Equal(tenant.RevisionId, unchanged.CurrentRevisionId);
        var stillStaged = await manager.Db.Files.AsNoTracking().SingleAsync(row => row.Id == staged.Id);
        Assert.Null(stillStaged.CaveId);
        Assert.NotNull(stillStaged.ExpiresOn);
        Assert.Equal(revisionCountBefore,
            await manager.Db.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
    }

    [Fact]
    public async Task InitialProposalCannotClaimExpiredGenericStagedFile()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(InitialProposalCannotClaimExpiredGenericStagedFile));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var location = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        var blobs = new TestFileBlobStore();

        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId,
            "contributor", blobs);
        await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("expired staging"));
        var staged = await contributor.Services.Files.StageAuthoringFile(content, "expired.pdf", default);
        var stagedRow = await contributor.Db.Files.SingleAsync(row => row.Id == staged.Id);
        stagedRow.ExpiresOn = DateTime.UtcNow.AddMinutes(-1);
        await contributor.Db.SaveChangesAsync();

        var values = PublishableValues(tenant, location.Id, "Expired generic staging");
        values.Files = [new EditFileMetadataVm
        {
            Id = staged.Id,
            FileTypeTagId = staged.FileTypeTagId,
            Name = staged.Name!
        }];

        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributor.ChangeRequests.CreateAsync(tenant.CaveId, values, tenant.RevisionId, default));

        contributor.Db.ChangeTracker.Clear();
        Assert.False(await contributor.Db.CaveChangeRequests.AnyAsync(row => row.CaveId == tenant.CaveId));
        Assert.False(await contributor.Db.CaveChangeRequestStagedFiles.AnyAsync(row => row.FileId == staged.Id));
        Assert.NotNull((await contributor.Db.Files.SingleAsync(row => row.Id == staged.Id)).ExpiresOn);
    }

    [Fact]
    public async Task InitialProposalClaimsGenericStagedFileOnlyWhenProposalIsCreated()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(InitialProposalClaimsGenericStagedFileOnlyWhenProposalIsCreated));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        var blobs = new TestFileBlobStore();

        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor", blobs);
        await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("proposal bytes"));
        var staged = await contributor.Services.Files.StageAuthoringFile(content, "proposal.pdf", default);
        Assert.False(await contributor.Db.CaveChangeRequestStagedFiles.AnyAsync(row => row.FileId == staged.Id));

        var context = await contributor.ChangeRequests.GetAuthoringContextAsync(tenant.CaveId, default);
        var values = ValuesFromCave(context.Cave);
        values.Name = "Proposal with staged file";
        values.Files = [new EditFileMetadataVm
        {
            Id = staged.Id,
            FileTypeTagId = staged.FileTypeTagId,
            Name = staged.Name!
        }];
        var requestId = await contributor.ChangeRequests.CreateAsync(
            tenant.CaveId, values, context.ExpectedBaseRevisionId, default);

        Assert.True(await contributor.Db.CaveChangeRequestStagedFiles.AnyAsync(row =>
            row.ChangeRequestId == requestId && row.FileId == staged.Id));
        var file = await contributor.Db.Files.AsNoTracking().SingleAsync(row => row.Id == staged.Id);
        Assert.Null(file.CaveId);
        Assert.NotNull(file.ExpiresOn);
        Assert.Single(blobs.Keys);
    }
    [Fact]
    public async Task GenericStagedFileCannotBeReadOrDeletedByAnotherAccountUser()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(GenericStagedFileCannotBeReadOrDeletedByAnotherAccountUser));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var fileType = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Other", "other0000a");
        var blobs = new TestFileBlobStore();
        string fileId;
        string partition;
        string key;

        await using (var owner = await CaveTestActor.CreateAsync(database, tenant.AccountId, "owner", blobs))
        {
            await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("private staged bytes"));
            var staged = await owner.Services.Files.StageAuthoringFile(content, "private.pdf", default);
            Assert.Equal(fileType.Id, staged.FileTypeTagId);
            var row = await owner.Db.Files.AsNoTracking().SingleAsync(file => file.Id == staged.Id);
            fileId = row.Id;
            partition = row.BlobContainer!;
            key = row.BlobKey!;
        }

        await using (var other = await CaveTestActor.CreateAsync(database, tenant.AccountId, "other", blobs))
        {
            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                other.Services.Files.OpenAuthoringStagedFileAsync(fileId, default));
            Assert.Equal(404, failure.StatusCode);
            Assert.Contains("Staged file", failure.Message, StringComparison.Ordinal);
            await other.Services.Files.DeleteUnpublishedFileAsync(fileId, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.True(await verify.Files.AnyAsync(file => file.Id == fileId && file.CaveId == null));
        Assert.True(blobs.Contains(partition, key));
    }

    [Fact]
    public async Task RequestBoundStagedFileRequiresRequestScopedAccess()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RequestBoundStagedFileRequiresRequestScopedAccess));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'b', null, null, null, null);
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000b");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        var blobs = new TestFileBlobStore();

        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor", blobs);
        await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("request-bound bytes"));
        var staged = await contributor.Services.Files.StageAuthoringFile(content, "evidence.pdf", default);
        var context = await contributor.ChangeRequests.GetAuthoringContextAsync(tenant.CaveId, default);
        var values = ValuesFromCave(context.Cave);
        values.Name = "Proposal with request-bound staged file";
        values.Files = [new EditFileMetadataVm
        {
            Id = staged.Id,
            FileTypeTagId = staged.FileTypeTagId,
            Name = staged.Name!
        }];
        var requestId = await contributor.ChangeRequests.CreateAsync(
            tenant.CaveId, values, context.ExpectedBaseRevisionId, default);

        var genericFailure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributor.Services.Files.OpenAuthoringStagedFileAsync(staged.Id, default));
        Assert.Equal(404, genericFailure.StatusCode);

        var requestScoped = await contributor.ChangeRequests.OpenStagedFileAsync(requestId, staged.Id, default);
        Assert.Equal("evidence.pdf", requestScoped.FileName);
        using var reader = new StreamReader(requestScoped.Stream);
        Assert.Equal("request-bound bytes", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task ProposalVersionKeepsExactLinePlotPayloadAfterPublishedCaveChanges()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalVersionKeepsExactLinePlotPayloadAfterPublishedCaveChanges));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");
        const string proposedGeoJson =
            "{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"properties\":{\"source\":\"proposal\"},\"geometry\":null}]}";

        string requestId;
        string versionId;
        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var context = await contributor.ChangeRequests.GetAuthoringContextAsync(tenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            values.Name = "Proposal owns exact line plot";
            values.LinePlots = [new() { Name = "Proposal line", GeoJson = proposedGeoJson }];
            requestId = await contributor.ChangeRequests.CreateAsync(
                tenant.CaveId, values, context.ExpectedBaseRevisionId, default);
            versionId = await CurrentVersionAsync(contributor.Db, requestId);
        }

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager"))
        {
            var context = await manager.Services.Caves.GetEditAuthoringContextAsync(tenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            values.LinePlots = [new()
            {
                Name = "Different published line",
                GeoJson = "{\"type\":\"FeatureCollection\",\"features\":[]}"
            }];
            await manager.Services.Caves.AddCave(values, default);
        }

        await using var reader = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor");
        var version = await reader.ChangeRequests.GetVersionAsync(requestId, versionId, default);
        var linePlot = Assert.Single(version.LinePlots);
        Assert.Equal("Proposal line", linePlot.Name);
        Assert.Equal(CaveJsonContent.NormalizeFeatureCollection(proposedGeoJson), linePlot.GeoJson);
    }

    private sealed class FailFirstStorageCleanupAcknowledgementInterceptor : DbCommandInterceptor
    {
        public bool Failed { get; private set; }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!Failed && command.CommandText.Contains(
                    "DELETE FROM \"StorageCleanupTargets\"", StringComparison.OrdinalIgnoreCase))
            {
                Failed = true;
                throw new InvalidOperationException("Simulated cleanup acknowledgement failure.");
            }

            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

}
