using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Import.Models;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

namespace Planarian.Tests;

public sealed class FileImportIntegrationTests(
    PostgresTestServer postgres,
    AzuriteTestServer azurite)
    : IClassFixture<PostgresTestServer>, IClassFixture<AzuriteTestServer>
{
    [Fact]
    public async Task DeletingMissingLegacyBlobDoesNotRecreateContainer()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(DeletingMissingLegacyBlobDoesNotRecreateContainer));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await SeedFileImportRequirementsAsync(database, cave, "manager");
        await using var db = database.CreateDbContext("manager", cave.AccountId);
        await db.RequestUser.Initialize(cave.AccountId, db.RequestUser.Id);
        var services = IntegrationTestServices.For(
            db, new TestFileBlobStore(), fileConnectionString: azurite.ConnectionString);
        var containerName = $"delete-{Guid.NewGuid():N}";
        var container = new BlobServiceClient(azurite.ConnectionString)
            .GetBlobContainerClient(containerName);

        Assert.False((await container.ExistsAsync()).Value);

        await services.BlobTransport.DeleteFile("already-gone.bin", containerName);

        Assert.False((await container.ExistsAsync()).Value);
    }

    [Fact]
    public async Task ChunkedFileImportMatchesOrdinaryUploadPublicationSemantics()
    {
        await using var directDatabase = await postgres.CreateDatabaseAsync(
            nameof(ChunkedFileImportMatchesOrdinaryUploadPublicationSemantics) + "_direct");
        await using var importDatabase = await postgres.CreateDatabaseAsync(
            nameof(ChunkedFileImportMatchesOrdinaryUploadPublicationSemantics) + "_import");
        var directCave = await CaveTestDataFactory.CreatePublishedCaveAsync(directDatabase, 'a');
        var importCave = await CaveTestDataFactory.CreatePublishedCaveAsync(importDatabase, 'a');
        await SeedFileImportRequirementsAsync(directDatabase, directCave, "manager");
        await SeedFileImportRequirementsAsync(importDatabase, importCave, "manager");
        const string surveyTagId = "filesurv0a";
        await ReferenceTestData.AddTagAsync(
            directDatabase, directCave.AccountId, TagTypeKeyConstant.File, "Survey", surveyTagId);
        await ReferenceTestData.AddTagAsync(
            importDatabase, importCave.AccountId, TagTypeKeyConstant.File, "Survey", surveyTagId);
        var payload = System.Text.Encoding.UTF8.GetBytes("same file bytes");
        const string fileName = "A01-1-survey.pdf";

        var directBlobs = new TestFileBlobStore();
        await using (var db = directDatabase.CreateDbContext("manager", directCave.AccountId))
        {
            await db.RequestUser.Initialize(directCave.AccountId, db.RequestUser.Id);
            var services = IntegrationTestServices.For(db, directBlobs);
            await services.Files.UploadCaveFile(new MemoryStream(payload), directCave.CaveId, fileName, default);
        }

        var importBlobs = new TestFileBlobStore();
        await using (var db = importDatabase.CreateDbContext("manager", importCave.AccountId))
        {
            await db.RequestUser.Initialize(importCave.AccountId, db.RequestUser.Id);
            var services = IntegrationTestServices.For(
                db, importBlobs, fileConnectionString: azurite.ConnectionString);
            var session = await services.Imports.CreateFileUploadSession(
                Request(fileName, payload.Length, "file-import-parity"), default);
            await services.Imports.UploadFileChunk(
                session.SessionId, new MemoryStream(payload), 0, 0, payload.Length, default);
            var result = await services.Imports.FinalizeFileUploadSession(session.SessionId, default);
            Assert.True(result.IsSuccessful);
        }

        await using var directVerify = directDatabase.CreateDbContext("verify", directCave.AccountId);
        await using var importVerify = importDatabase.CreateDbContext("verify", importCave.AccountId);
        var directFile = await directVerify.Files.AsNoTracking().SingleAsync(row => row.CaveId == directCave.CaveId);
        var importFile = await importVerify.Files.AsNoTracking().SingleAsync(row => row.CaveId == importCave.CaveId);

        Assert.Equal(
            (directFile.AccountId, directFile.Name, directFile.Extension, directFile.FileTypeTagId, directFile.ExpiresOn),
            (importFile.AccountId, importFile.Name, importFile.Extension, importFile.FileTypeTagId, importFile.ExpiresOn));
        Assert.Equal(surveyTagId, directFile.FileTypeTagId);
        Assert.Equal($"objects/files/{directFile.Id}", directFile.BlobKey);
        Assert.Equal($"objects/files/{importFile.Id}", importFile.BlobKey);
        Assert.Equal(directFile.BlobContainer, importFile.BlobContainer);
        Assert.Equal(payload, directBlobs.Read(directFile.BlobContainer!, directFile.BlobKey!));
        Assert.Equal(payload, importBlobs.Read(importFile.BlobContainer!, importFile.BlobKey!));

        var directCaveRow = await directVerify.Caves.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.Id == directCave.CaveId);
        var importCaveRow = await importVerify.Caves.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.Id == importCave.CaveId);
        var directRevision = await directVerify.CaveRevisions.SingleAsync(row => row.Id == directCaveRow.CurrentRevisionId);
        var importRevision = await importVerify.CaveRevisions.SingleAsync(row => row.Id == importCaveRow.CurrentRevisionId);

        Assert.Equal((directCave.RevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, (string?)null),
            (directRevision.PreviousRevisionId, directRevision.Source, directRevision.Operation, directRevision.ImportBatchId));
        Assert.Equal((importCave.RevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, (string?)null),
            (importRevision.PreviousRevisionId, importRevision.Source, importRevision.Operation, importRevision.ImportBatchId));
        Assert.Empty(await importVerify.CaveImportBatches.ToListAsync());

        ImportParityAssertions.AssertPublishedSnapshotsEqual(
            CaveSnapshotJson.Deserialize(directRevision.SnapshotJson, directRevision.SnapshotSchemaVersion),
            CaveSnapshotJson.Deserialize(importRevision.SnapshotJson, importRevision.SnapshotSchemaVersion),
            normalizeFileIds: true);
    }

    [Fact]
    public async Task ChunkedFileImportPublishesNormalFileRevisionCleansStagingAndIsIdempotent()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(ChunkedFileImportPublishesNormalFileRevisionCleansStagingAndIsIdempotent));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await SeedFileImportRequirementsAsync(database, cave, "manager");
        await ReferenceTestData.AddTagAsync(
            database, cave.AccountId, TagTypeKeyConstant.File, "Survey");
        var blobs = new TestFileBlobStore();
        var payload = System.Text.Encoding.UTF8.GetBytes("survey file bytes for chunked import");

        await using var db = database.CreateDbContext("manager", cave.AccountId);
        await db.RequestUser.Initialize(cave.AccountId, db.RequestUser.Id);
        var services = IntegrationTestServices.For(
            db, blobs, fileConnectionString: azurite.ConnectionString);
        var revisionsBefore = await db.CaveRevisions.CountAsync(row => row.CaveId == cave.CaveId);
        var request = Request("A01-1-survey.pdf", payload.Length, "file-import-request-1");

        var session = await services.Imports.CreateFileUploadSession(request, default);
        var split = 11;
        await services.Imports.UploadFileChunk(
            session.SessionId, new MemoryStream(payload[..split]), 0, 0, split, default);
        var uploaded = await services.Imports.UploadFileChunk(
            session.SessionId, new MemoryStream(payload[split..]), split, 1, payload.Length - split, default);
        Assert.Equal(payload.Length, uploaded.UploadedBytes);
        Assert.Equal("uploaded", uploaded.Status);

        var result = await services.Imports.FinalizeFileUploadSession(session.SessionId, default);

        Assert.True(result.IsSuccessful);
        Assert.Equal($"{cave.CountyDisplayId}-{cave.CountyNumber} {cave.CaveName}", result.AssociatedCave);
        Assert.Equal(request.FileName, result.FileName);
        Assert.Equal(request.RequestId, result.RequestId);

        db.ChangeTracker.Clear();
        var file = await db.Files.AsNoTracking().SingleAsync(row => row.CaveId == cave.CaveId);
        Assert.Equal(("A01-1-survey", ".pdf"), (file.Name, file.Extension));
        Assert.Equal(cave.AccountId, file.AccountId);
        Assert.NotNull(file.BlobContainer);
        Assert.Equal($"objects/files/{file.Id}", file.BlobKey);
        Assert.Null(file.ExpiresOn);
        Assert.Equal("Survey", await db.TagTypes.IgnoreQueryFilters()
            .Where(tag => tag.Id == file.FileTypeTagId)
            .Select(tag => tag.Name)
            .SingleAsync());
        Assert.True(blobs.Contains(file.BlobContainer!, file.BlobKey!));
        Assert.Equal(payload, blobs.Read(file.BlobContainer!, file.BlobKey!));

        var caveRow = await db.Caves.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.Id == cave.CaveId);
        var revision = await db.CaveRevisions.SingleAsync(row => row.Id == caveRow.CurrentRevisionId);
        Assert.Equal((cave.RevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, null),
            (revision.PreviousRevisionId, revision.Source, revision.Operation, revision.ImportBatchId));
        Assert.Equal(revisionsBefore + 1,
            await db.CaveRevisions.CountAsync(row => row.CaveId == cave.CaveId));
        var snapshot = CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);
        var snapshotFile = Assert.Single(snapshot.Files);
        Assert.Equal((file.Id, file.Name, file.Extension), (snapshotFile.Id, snapshotFile.Name, snapshotFile.Extension));

        var stagedBlob = new BlobServiceClient(azurite.ConnectionString)
            .GetBlobContainerClient(db.RequestUser.AccountContainerName)
            .GetBlobClient($"temp/import/file-upload-sessions/{session.SessionId}");
        Assert.False((await stagedBlob.ExistsAsync()).Value);

        var repeated = await services.Imports.FinalizeFileUploadSession(session.SessionId, default);
        Assert.Equal((result.FileName, result.AssociatedCave, result.RequestId, result.IsSuccessful),
            (repeated.FileName, repeated.AssociatedCave, repeated.RequestId, repeated.IsSuccessful));
        db.ChangeTracker.Clear();
        Assert.Single(await db.Files.Where(row => row.CaveId == cave.CaveId).ToListAsync());
        Assert.Equal(revisionsBefore + 1,
            await db.CaveRevisions.CountAsync(row => row.CaveId == cave.CaveId));
    }

    [Fact]
    public async Task CancelAllSkipsSessionThatBeginsFinalizingAfterSelection()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(CancelAllSkipsSessionThatBeginsFinalizingAfterSelection));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await SeedFileImportRequirementsAsync(database, cave, "manager");
        await using var db = database.CreateDbContext("manager", cave.AccountId);
        await db.RequestUser.Initialize(cave.AccountId, db.RequestUser.Id);
        var services = IntegrationTestServices.For(
            db, new TestFileBlobStore(), fileConnectionString: azurite.ConnectionString);
        var chunked = new Planarian.Shared.Services.ChunkedUploadService(
            db.RequestUser, services.BlobTransport, new Planarian.Shared.Options.RequestThrottleOptions());
        var session = await chunked.CreateSession(
            new Planarian.Shared.Services.ChunkedUploadSessionCreateRequest
            {
                FileName = "cancel-race.pdf", FileSize = 10,
                BlobKeyPrefix = "temp/import/file-upload-sessions"
            }, default);

        var other = await chunked.CreateSession(
            new Planarian.Shared.Services.ChunkedUploadSessionCreateRequest
            {
                FileName = "other-cancel.pdf", FileSize = 10,
                BlobKeyPrefix = "temp/import/file-upload-sessions"
            }, default);

        await session.Gate.WaitAsync();
        var gateHeld = true;
        try
        {
            // Cancel-all selects the Created session and then blocks acquiring Gate.
            var cancelTask = chunked.CancelActiveSessionsForCurrentUser(default);
            Assert.False(cancelTask.IsCompleted,
                "Cancel-all must have selected the session before finalization begins.");

            // Another operation acquires the same upload gate first and advances
            // the state to Finalizing before cancellation can enter CloseSession.
            session.Status = Planarian.Shared.Services.ChunkedUploadSessionStatus.Finalizing;
            session.Gate.Release();
            gateHeld = false;
            await cancelTask.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(Planarian.Shared.Services.ChunkedUploadSessionStatus.Finalizing, session.Status);
            Assert.Same(session, chunked.GetRequiredSession(session.SessionId));
            Assert.Equal(Planarian.Shared.Services.ChunkedUploadSessionStatus.Canceled, other.Status);
            Assert.Throws<ApiException>(() => chunked.GetRequiredSession(other.SessionId));
            session.Status = Planarian.Shared.Services.ChunkedUploadSessionStatus.Uploaded;
            session.ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(-1);
            Assert.Same(session, Assert.Single(await chunked.CleanupExpiredSessions(default)));
        }
        finally
        {
            if (gateHeld) session.Gate.Release();
        }
    }

    [Fact]
    public async Task ExpiredSessionSweepCannotRemoveSessionDuringActiveUploadOrFinalization()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(ExpiredSessionSweepCannotRemoveSessionDuringActiveUploadOrFinalization));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await SeedFileImportRequirementsAsync(database, cave, "manager");
        await using var db = database.CreateDbContext("manager", cave.AccountId);
        await db.RequestUser.Initialize(cave.AccountId, db.RequestUser.Id);
        var services = IntegrationTestServices.For(
            db, new TestFileBlobStore(), fileConnectionString: azurite.ConnectionString);
        var chunked = new Planarian.Shared.Services.ChunkedUploadService(
            db.RequestUser, services.BlobTransport, new Planarian.Shared.Options.RequestThrottleOptions());
        var session = await chunked.CreateSession(
            new Planarian.Shared.Services.ChunkedUploadSessionCreateRequest
            {
                FileName = "sweep.pdf", FileSize = 10,
                BlobKeyPrefix = "temp/import/file-upload-sessions"
            }, default);

        await session.Gate.WaitAsync();
        var gateHeld = true;
        try
        {
            session.ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(-1);
            var sweep = chunked.CleanupExpiredSessions(default);
            await Task.Delay(250);
            Assert.False(sweep.IsCompleted,
                "An expiration sweep must not delete a session while an upload holds its gate.");

            // A completed chunk refreshes expiration under the same gate.
            session.ExpiresOn = DateTimeOffset.UtcNow.AddHours(1);
            session.Gate.Release();
            gateHeld = false;
            Assert.Empty(await sweep.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Same(session, chunked.GetRequiredSession(session.SessionId));
        }
        finally
        {
            if (gateHeld)
                session.Gate.Release();
        }

        // Finalization may legitimately extend past its original upload TTL.
        session.Status = Planarian.Shared.Services.ChunkedUploadSessionStatus.Finalizing;
        session.ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(-1);
        Assert.Empty(await chunked.CleanupExpiredSessions(default));
        Assert.Same(session, chunked.GetRequiredSession(session.SessionId));

        // A stale operation can still hold a reference obtained before a sweep.
        // Retiring that session must mark it canceled as well as remove its ID.
        session.Status = Planarian.Shared.Services.ChunkedUploadSessionStatus.Uploaded;
        var retired = await chunked.CleanupExpiredSessions(default);
        Assert.Same(session, Assert.Single(retired));
        Assert.Equal(Planarian.Shared.Services.ChunkedUploadSessionStatus.Canceled, session.Status);
        Assert.Throws<ApiException>(() => chunked.GetRequiredSession(session.SessionId));
    }

    [Fact]
    public async Task ConcurrentFinalizationOfSameSessionPublishesOnlyOnce()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(ConcurrentFinalizationOfSameSessionPublishesOnlyOnce));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await SeedFileImportRequirementsAsync(database, cave, "manager");
        var payload = System.Text.Encoding.UTF8.GetBytes("one publication only");
        var blobs = new TestFileBlobStore();
        using var firstWriteEntered = new ManualResetEventSlim();
        using var allowFirstWrite = new ManualResetEventSlim();
        var createAttempts = 0;
        blobs.BeforeCreate = _ =>
        {
            if (Interlocked.Increment(ref createAttempts) != 1) return;
            firstWriteEntered.Set();
            if (!allowFirstWrite.Wait(TimeSpan.FromSeconds(20)))
                throw new TimeoutException("Concurrent finalization test did not release the first writer.");
        };

        await using var firstDb = database.CreateDbContext("manager", cave.AccountId);
        await firstDb.RequestUser.Initialize(cave.AccountId, firstDb.RequestUser.Id);
        await using var secondDb = database.CreateDbContext("manager", cave.AccountId);
        await secondDb.RequestUser.Initialize(cave.AccountId, secondDb.RequestUser.Id);
        var first = IntegrationTestServices.For(firstDb, blobs, fileConnectionString: azurite.ConnectionString);
        var second = IntegrationTestServices.For(secondDb, blobs, fileConnectionString: azurite.ConnectionString);
        var session = await first.Imports.CreateFileUploadSession(
            Request("A01-1-concurrent.pdf", payload.Length, "concurrent-file-import"), default);
        await first.Imports.UploadFileChunk(session.SessionId, new MemoryStream(payload),
            0, 0, payload.Length, default);

        var firstTask = Task.Run(() => first.Imports.FinalizeFileUploadSession(session.SessionId, default));
        try
        {
            Assert.True(await Task.Run(() => firstWriteEntered.Wait(TimeSpan.FromSeconds(15))),
                "First finalization never reached the durable object write.");

            // Cancel must not delete the committed staging blob while publication
            // is already in progress; that can make an otherwise valid File fail.
            var cancellation = await Assert.ThrowsAsync<ApiException>(() =>
                second.Imports.CancelFileUploadSession(session.SessionId, default));
            Assert.Equal(ApiExceptionType.Conflict, cancellation.ErrorCode);

            var secondTask = Task.Run(() => second.Imports.FinalizeFileUploadSession(session.SessionId, default));
            await Task.Delay(450); // The first writer is held; the second can reach the same boundary.
            allowFirstWrite.Set();

            var results = await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(25));
            Assert.All(results, result => Assert.True(result.IsSuccessful));
            Assert.Equal(1, Volatile.Read(ref createAttempts));
            Assert.Equal(results[0].FileName, results[1].FileName);
            Assert.Equal(results[0].RequestId, results[1].RequestId);
            // The API overwrites RequestId with each HTTP trace identifier. The
            // cached publication result must not be shared mutable response state.
            Assert.NotSame(results[0], results[1]);
            var originalSecondRequestId = results[1].RequestId;
            results[0].RequestId = "first-http-trace";
            Assert.Equal(originalSecondRequestId, results[1].RequestId);
        }
        finally
        {
            allowFirstWrite.Set();
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Single(await verify.Files.Where(file => file.CaveId == cave.CaveId).ToListAsync());
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(revision => revision.CaveId == cave.CaveId));
    }

    [Fact]
    public async Task ChunkedFileImportRetriesOrdinaryCleanupDebt()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(ChunkedFileImportRetriesOrdinaryCleanupDebt));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await SeedFileImportRequirementsAsync(database, cave, "manager");
        var blobs = new TestFileBlobStore();
        var payload = "published upload bytes"u8.ToArray();

        await using var db = database.CreateDbContext("manager", cave.AccountId);
        await db.RequestUser.Initialize(cave.AccountId, db.RequestUser.Id);
        var container = db.RequestUser.AccountContainerName;
        const string orphanKey = "temp/import/caves/abandoned-source";
        blobs.Seed(container, orphanKey, "unreferenced source"u8.ToArray());
        db.StorageCleanupTargets.Add(new StorageCleanupTarget
        {
            AccountId = cave.AccountId,
            StoragePartition = container,
            StorageKey = orphanKey,
            Purpose = StorageCleanupPurpose.ImportSourceRemoval
        });
        await db.SaveChangesAsync();

        var services = IntegrationTestServices.For(
            db, blobs, fileConnectionString: azurite.ConnectionString);
        var session = await services.Imports.CreateFileUploadSession(
            Request("A01-1-cleanup.pdf", payload.Length, "file-import-cleanup"), default);
        await services.Imports.UploadFileChunk(
            session.SessionId, new MemoryStream(payload), 0, 0, payload.Length, default);

        var result = await services.Imports.FinalizeFileUploadSession(session.SessionId, default);

        Assert.True(result.IsSuccessful);
        Assert.False(blobs.Contains(container, orphanKey));
        Assert.False(await db.StorageCleanupTargets.AnyAsync(target =>
            target.AccountId == cave.AccountId && target.StorageKey == orphanKey));
        db.ChangeTracker.Clear();
        var published = Assert.Single(await db.Files.Where(row => row.CaveId == cave.CaveId).ToListAsync());
        Assert.Equal(payload, blobs.Read(container, published.BlobKey!));
    }

    [Fact]
    public async Task ChunkOrderIncompleteFinalizeAndForeignTenantAccessFailWithoutPublication()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(ChunkOrderIncompleteFinalizeAndForeignTenantAccessFailWithoutPublication));
        var caveA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var caveB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        await SeedFileImportRequirementsAsync(database, caveA, "manager-a");
        await SeedFileImportRequirementsAsync(database, caveB, "manager-b");
        var payload = System.Text.Encoding.UTF8.GetBytes("ordered chunks");

        await using var dbA = database.CreateDbContext("manager-a", caveA.AccountId);
        await dbA.RequestUser.Initialize(caveA.AccountId, dbA.RequestUser.Id);
        var servicesA = IntegrationTestServices.For(
            dbA, new TestFileBlobStore(), fileConnectionString: azurite.ConnectionString);
        var session = await servicesA.Imports.CreateFileUploadSession(
            Request("A01-1-order.pdf", payload.Length, "file-import-order"), default);

        await Assert.ThrowsAsync<ApiException>(() => servicesA.Imports.UploadFileChunk(
            session.SessionId, new MemoryStream(payload[..4]), 0, 1, 4, default));

        await servicesA.Imports.UploadFileChunk(
            session.SessionId, new MemoryStream(payload[..4]), 0, 0, 4, default);
        await Assert.ThrowsAsync<ApiException>(() =>
            servicesA.Imports.FinalizeFileUploadSession(session.SessionId, default));

        await using var dbB = database.CreateDbContext("manager-b", caveB.AccountId);
        await dbB.RequestUser.Initialize(caveB.AccountId, dbB.RequestUser.Id);
        var servicesB = IntegrationTestServices.For(
            dbB, new TestFileBlobStore(), fileConnectionString: azurite.ConnectionString);
        await Assert.ThrowsAsync<ApiException>(() =>
            servicesB.Imports.CancelFileUploadSession(session.SessionId, default));

        await servicesA.Imports.CancelFileUploadSession(session.SessionId, default);
        var cancelledBlob = new BlobServiceClient(azurite.ConnectionString)
            .GetBlobContainerClient(dbA.RequestUser.AccountContainerName)
            .GetBlobClient($"temp/import/file-upload-sessions/{session.SessionId}");
        Assert.False((await cancelledBlob.ExistsAsync()).Value);
        await Assert.ThrowsAsync<ApiException>(() => servicesA.Imports.UploadFileChunk(
            session.SessionId, new MemoryStream(payload[4..]), 4, 1, payload.Length - 4, default));

        Assert.Empty(await dbA.Files.Where(row => row.CaveId == caveA.CaveId).ToListAsync());
        Assert.Equal(caveA.RevisionId,
            (await dbA.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == caveA.CaveId)).CurrentRevisionId);
    }

    [Fact]
    public async Task DuplicateAndMissingCaveValidationRejectBeforeCreatingNewPublication()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(DuplicateAndMissingCaveValidationRejectBeforeCreatingNewPublication));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await SeedFileImportRequirementsAsync(database, cave, "manager");
        var blobs = new TestFileBlobStore();
        var payload = System.Text.Encoding.UTF8.GetBytes("duplicate file");

        await using var db = database.CreateDbContext("manager", cave.AccountId);
        await db.RequestUser.Initialize(cave.AccountId, db.RequestUser.Id);
        var services = IntegrationTestServices.For(
            db, blobs, fileConnectionString: azurite.ConnectionString);
        var request = Request("A01-1-duplicate.pdf", payload.Length, "file-import-duplicate");
        var session = await services.Imports.CreateFileUploadSession(request, default);
        await services.Imports.UploadFileChunk(
            session.SessionId, new MemoryStream(payload), 0, 0, payload.Length, default);
        await services.Imports.FinalizeFileUploadSession(session.SessionId, default);

        db.ChangeTracker.Clear();
        var fileCount = await db.Files.CountAsync(row => row.CaveId == cave.CaveId);
        var revisionCount = await db.CaveRevisions.CountAsync(row => row.CaveId == cave.CaveId);

        var duplicate = Request(request.FileName, payload.Length, "file-import-duplicate-2");
        duplicate.IgnoreDuplicates = true;
        var duplicateError = await Assert.ThrowsAsync<ApiException>(() =>
            services.Imports.CreateFileUploadSession(duplicate, default));
        Assert.Equal(ApiExceptionType.Conflict, duplicateError.ErrorCode);

        var missing = Request("ZZZ-99-missing.pdf", payload.Length, "file-import-missing");
        var missingError = await Assert.ThrowsAsync<ApiException>(() =>
            services.Imports.CreateFileUploadSession(missing, default));
        Assert.Equal(ApiExceptionType.NotFound, missingError.ErrorCode);

        db.ChangeTracker.Clear();
        Assert.Equal(fileCount, await db.Files.CountAsync(row => row.CaveId == cave.CaveId));
        Assert.Equal(revisionCount, await db.CaveRevisions.CountAsync(row => row.CaveId == cave.CaveId));
    }

    [Fact]
    public async Task InvalidRegexMalformedFilenameAndForeignOnlyCaveAreRejectedBeforeSessionCreation()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(InvalidRegexMalformedFilenameAndForeignOnlyCaveAreRejectedBeforeSessionCreation));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var foreignCave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        await SeedFileImportRequirementsAsync(database, cave, "manager");

        await using var db = database.CreateDbContext("manager", cave.AccountId);
        await db.RequestUser.Initialize(cave.AccountId, db.RequestUser.Id);
        var services = IntegrationTestServices.For(
            db, new TestFileBlobStore(), fileConnectionString: azurite.ConnectionString);

        var invalidRegex = Request("A01-1-invalid.pdf", 10, "file-import-invalid-regex");
        invalidRegex.IdRegex = "[";
        var invalidRegexError = await Assert.ThrowsAsync<ApiException>(() =>
            services.Imports.CreateFileUploadSession(invalidRegex, default));
        Assert.Equal(ApiExceptionType.BadRequest, invalidRegexError.ErrorCode);

        var malformed = Request("no-cave-id.pdf", 10, "file-import-malformed");
        var malformedError = await Assert.ThrowsAsync<ApiException>(() =>
            services.Imports.CreateFileUploadSession(malformed, default));
        Assert.Equal(ApiExceptionType.BadRequest, malformedError.ErrorCode);

        var foreignOnly = Request(
            $"{foreignCave.CountyDisplayId}-{foreignCave.CountyNumber}-foreign.pdf",
            10,
            "file-import-foreign-cave");
        var foreignError = await Assert.ThrowsAsync<ApiException>(() =>
            services.Imports.CreateFileUploadSession(foreignOnly, default));
        Assert.Equal(ApiExceptionType.NotFound, foreignError.ErrorCode);

        Assert.Empty(await db.Files.Where(row => row.CaveId == cave.CaveId).ToListAsync());
        Assert.Equal(cave.RevisionId,
            (await db.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId)).CurrentRevisionId);
    }

    [Fact]
    public async Task DurableObjectWriteFailureLeavesNoFileOrRevisionAndCleansCommittedStagingBlob()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(DurableObjectWriteFailureLeavesNoFileOrRevisionAndCleansCommittedStagingBlob));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await SeedFileImportRequirementsAsync(database, cave, "manager");
        var blobs = new TestFileBlobStore { FailWrites = true };
        var payload = System.Text.Encoding.UTF8.GetBytes("publication failure");

        await using var db = database.CreateDbContext("manager", cave.AccountId);
        await db.RequestUser.Initialize(cave.AccountId, db.RequestUser.Id);
        var services = IntegrationTestServices.For(
            db, blobs, fileConnectionString: azurite.ConnectionString);
        var session = await services.Imports.CreateFileUploadSession(
            Request("A01-1-failure.pdf", payload.Length, "file-import-failure"), default);
        await services.Imports.UploadFileChunk(
            session.SessionId, new MemoryStream(payload), 0, 0, payload.Length, default);

        await Assert.ThrowsAsync<IOException>(() =>
            services.Imports.FinalizeFileUploadSession(session.SessionId, default));

        db.ChangeTracker.Clear();
        Assert.Empty(await db.Files.Where(row => row.CaveId == cave.CaveId).ToListAsync());
        Assert.Equal(cave.RevisionId,
            (await db.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId)).CurrentRevisionId);

        var stagedBlob = new BlobServiceClient(azurite.ConnectionString)
            .GetBlobContainerClient(db.RequestUser.AccountContainerName)
            .GetBlobClient($"temp/import/file-upload-sessions/{session.SessionId}");
        Assert.False((await stagedBlob.ExistsAsync()).Value);
    }

    private static ImportFileRequest Request(string fileName, int length, string requestId) => new()
    {
        FileName = fileName,
        FileSize = length,
        RequestId = requestId,
        IdRegex = "[A-Z0-9]+-[0-9]+",
        DelimiterRegex = "-",
        IgnoreDuplicates = true
    };

    private static async Task SeedFileImportRequirementsAsync(
        PostgresTestDatabase database, PublishedCaveTestData cave, string managerId)
    {
        await ReferenceTestData.AddTagAsync(
            database, cave.AccountId, TagTypeKeyConstant.File, "Other", $"fileother{cave.AccountId[^1]}");
        await CavePermissions.GrantManagerAsync(database, cave, managerId);
    }
}
