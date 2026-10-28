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
