using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Planarian.Library.Exceptions;
using Planarian.Library.Helpers;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Import.Data;
using Planarian.Modules.Import.Models;
using Planarian.Modules.Import.Planning;
using Planarian.Shared.Services;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

namespace Planarian.Tests;

public sealed class ImportSourceFileRetentionIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ImportUploadCollisionNeverOverwritesOrDeletesExistingObject()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ImportUploadCollisionNeverOverwritesOrDeletesExistingObject));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var originalBytes = "existing immutable object"u8.ToArray();
        var blobs = new TestFileBlobStore();
        blobs.BeforeCreate = address => blobs.Seed(address.Partition, address.Key, originalBytes);
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);

        await Assert.ThrowsAsync<StorageObjectAlreadyExistsException>(() =>
            UploadAsync(actor, CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"), "collision.csv"));

        actor.Db.ChangeTracker.Clear();
        var address = Assert.Single(blobs.Keys);
        Assert.StartsWith(ImportSourceStoragePath.CaveEntranceCsvPrefix, address.Key);
        Assert.Equal(originalBytes, blobs.Read(address.Container, address.Key));
        Assert.Equal(0, blobs.DeleteIfExistsAttempts);
        Assert.False(await actor.Db.CaveImportSources.AnyAsync());
        Assert.False(await actor.Db.StorageCleanupTargets.AnyAsync());
    }

    [Fact]
    public async Task AmbiguousImportSourceUploadCreatesRetryableCleanupDebt()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AmbiguousImportSourceUploadCreatesRetryableCleanupDebt));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore { FailAfterCreate = true, FailDeletes = true };
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);

        await Assert.ThrowsAsync<IOException>(() =>
            UploadAsync(actor, CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"), "uncertain.csv"));

        actor.Db.ChangeTracker.Clear();
        Assert.False(await actor.Db.CaveImportSources.AnyAsync());
        var target = Assert.Single(await actor.Db.StorageCleanupTargets.Where(target =>
            target.Purpose == StorageCleanupPurpose.ImportSourceRemoval).ToListAsync());
        Assert.True(blobs.Contains(target.StoragePartition, target.StorageKey));
        Assert.Equal(1, blobs.DeleteIfExistsAttempts);

        blobs.FailDeletes = false;
        await actor.Services.StorageCleanup.CleanupImportSourceTargetsAsync([target.Id], default);
        actor.Db.ChangeTracker.Clear();
        Assert.False(blobs.Contains(target.StoragePartition, target.StorageKey));
        Assert.False(await actor.Db.StorageCleanupTargets.AnyAsync(row => row.Id == target.Id));
    }

    [Fact]
    public async Task NewImportUploadAllocatesFinalIdBeforeCreatingObject()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(NewImportUploadAllocatesFinalIdBeforeCreatingObject));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);

        var uploaded = await UploadAsync(actor,
            CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"), "identity.csv");

        actor.Db.ChangeTracker.Clear();
        var source = await actor.Db.CaveImportSources.AsNoTracking().SingleAsync(row => row.Id == uploaded.Id);
        Assert.Equal(ImportSourceStoragePath.BuildCaveEntranceCsvKey(source.Id), source.BlobKey);
        Assert.True(blobs.Contains(source.BlobContainer, source.BlobKey));
        Assert.False(await actor.Db.Files.AnyAsync(row => row.Id == uploaded.Id));
    }

    [Fact]
    public async Task ImportStorageIdentityDoesNotDependOnLongFileExtension()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ImportStorageIdentityDoesNotDependOnLongFileExtension));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);
        var fileName = "source." + new string('x', FileValidation.MaximumFileNameLength - "source.".Length);
        Assert.Equal(FileValidation.MaximumFileNameLength, fileName.Length);

        var uploaded = await UploadAsync(actor,
            CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"), fileName);

        actor.Db.ChangeTracker.Clear();
        var source = await actor.Db.CaveImportSources.AsNoTracking().SingleAsync(row => row.Id == uploaded.Id);
        Assert.Equal(fileName, source.FileName);
        Assert.Equal(ImportSourceStoragePath.BuildCaveEntranceCsvKey(source.Id), source.BlobKey);
        Assert.True(source.BlobKey.Length <= PropertyLength.Key);
        Assert.True(blobs.Contains(source.BlobContainer, source.BlobKey));

        await actor.Services.Imports.ImportCavesFileProcess(
            source.Id, isDryRun: false, syncExisting: true, default);
        actor.Db.ChangeTracker.Clear();
        var batch = await actor.Db.CaveImportBatches.SingleAsync(row => row.SourceFileId == source.Id);
        Assert.Equal(fileName, batch.SourceFileName);
    }

    [Fact]
    public async Task ImportSourceIsNotAnAuthoringFile()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(ImportSourceIsNotAnAuthoringFile));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);
        var source = await UploadAsync(actor,
            CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"), "not-authoring.csv");

        var open = await Assert.ThrowsAsync<ApiException>(() =>
            actor.Services.Files.OpenAuthoringStagedFileAsync(source.Id, default));
        Assert.Equal(404, open.StatusCode);
        var availability = await Assert.ThrowsAsync<ApiException>(() =>
            actor.Services.Files.RequireAuthoringStagedObjectsAvailableAsync([source.Id], default));
        Assert.Equal(404, availability.StatusCode);

        await actor.Services.Files.DeleteAuthoringStagedFileAsync(source.Id, default);
        actor.Db.ChangeTracker.Clear();
        Assert.True(await actor.Db.CaveImportSources.AnyAsync(row => row.Id == source.Id));
        Assert.False(await actor.Db.Files.AnyAsync(row => row.Id == source.Id));

        var requests = new Planarian.Modules.Caves.Revisions.CaveChangeRequestRepository(
            actor.Db, actor.Db.RequestUser);
        Assert.Empty(await requests.GetAuthorableStagedFileIdsAsync([source.Id], null, default));

        await using var transaction = await actor.Db.Database.BeginTransactionAsync();
        var attach = await Assert.ThrowsAsync<ApiException>(() =>
            new Planarian.Modules.Caves.Repositories.CaveRepository(actor.Db, actor.Db.RequestUser)
                .AttachAuthoringStagedFilesAsync(cave.CaveId, [source.Id], default));
        Assert.Equal(404, attach.StatusCode);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task ImportSourceIdCannotBeClaimedByChangeRequest()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ImportSourceIdCannotBeClaimedByChangeRequest));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);
        var source = await UploadAsync(actor,
            CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"), "separate-domain.csv");

        const string requestId = "claimreq01";
        actor.Db.CaveChangeRequests.Add(new CaveChangeRequest
        {
            Id = requestId,
            AccountId = cave.AccountId,
            CaveId = cave.CaveId,
            BaseRevisionId = cave.RevisionId,
            Status = CaveChangeRequestStatus.Pending
        });
        await actor.Db.SaveChangesAsync();

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                insert into "CaveChangeRequestStagedFiles"
                    ("Id", "AccountId", "ChangeRequestId", "FileId", "CreatedOn")
                values ('badclaim01', @account, @request, @file, now())
                """, connection);
            command.Parameters.AddWithValue("account", cave.AccountId);
            command.Parameters.AddWithValue("request", requestId);
            command.Parameters.AddWithValue("file", source.Id);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        }

        actor.Db.ChangeTracker.Clear();
        Assert.True(await actor.Db.CaveImportSources.AnyAsync(row => row.Id == source.Id));
        Assert.False(await actor.Db.CaveChangeRequestStagedFiles.AnyAsync(row => row.FileId == source.Id));
    }

    [Fact]
    public async Task LegacyGuidStorageKeyRemainsUsable()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(LegacyGuidStorageKeyRemainsUsable));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);

        const string sourceId = "legacyimp1";
        var key = ImportSourceStoragePath.CaveEntranceCsvPrefix + Guid.NewGuid() + ".csv";
        var csv = CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,");
        actor.Db.CaveImportSources.Add(new CaveImportSource
        {
            Id = sourceId,
            AccountId = cave.AccountId,
            FileName = "legacy-source.csv",
            BlobContainer = actor.Db.RequestUser.AccountContainerName,
            BlobKey = key,
            ExpiresOn = DateTime.UtcNow.AddDays(1)
        });
        await actor.Db.SaveChangesAsync();
        blobs.Seed(actor.Db.RequestUser.AccountContainerName, key, Encoding.UTF8.GetBytes(csv));

        await actor.Services.Imports.ImportCavesFileProcess(
            sourceId, isDryRun: false, syncExisting: true, default);

        actor.Db.ChangeTracker.Clear();
        var retained = await actor.Db.CaveImportSources.SingleAsync(row => row.Id == sourceId);
        Assert.Null(retained.ExpiresOn);
        Assert.Equal(key, retained.BlobKey);
        Assert.True(blobs.Contains(retained.BlobContainer, key));
        Assert.True(await actor.Db.CaveImportBatches.AnyAsync(batch =>
            batch.SourceFileId == sourceId && batch.SourceFileName == "legacy-source.csv"));
    }

    [Fact]
    public async Task ExpiredImportSourceCannotBePreviewedOrApplied()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ExpiredImportSourceCannotBePreviewedOrApplied));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);
        var uploaded = await UploadAsync(actor,
            CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"), "expired.csv");

        actor.Db.ChangeTracker.Clear();
        var source = await actor.Db.CaveImportSources.SingleAsync(row => row.Id == uploaded.Id);
        source.ExpiresOn = DateTime.UtcNow.AddMinutes(-1);
        await actor.Db.SaveChangesAsync();
        var address = (source.BlobContainer, source.BlobKey);

        foreach (var dryRun in new[] { true, false })
        {
            var error = await Assert.ThrowsAsync<ApiException>(() =>
                actor.Services.Imports.ImportCavesFileProcess(
                    uploaded.Id, dryRun, syncExisting: true, default));
            Assert.Equal(400, error.StatusCode);
            Assert.Contains("expired", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        actor.Db.ChangeTracker.Clear();
        var unchanged = await actor.Db.CaveImportSources.AsNoTracking()
            .SingleAsync(row => row.Id == uploaded.Id);
        Assert.NotNull(unchanged.ExpiresOn);
        Assert.True(unchanged.ExpiresOn <= DateTime.UtcNow);
        Assert.True(blobs.Contains(address.BlobContainer, address.BlobKey));
        Assert.False(await actor.Db.CaveImportBatches.AnyAsync(row => row.SourceFileId == uploaded.Id));
    }

    [Fact]
    public async Task DryRunRejectsAuthoringFileEvenWhenBytesAreValidCaveCsv()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DryRunRejectsAuthoringFileEvenWhenBytesAreValidCaveCsv));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.File, "Other", "other0000a");
        var blobs = new TestFileBlobStore();
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(
            CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,")));
        var staged = await actor.Services.Files.StageAuthoringFile(content, "looks-valid.csv", default);

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            actor.Services.Imports.ImportCavesFileProcess(
                staged.Id, isDryRun: true, syncExisting: true, default));

        Assert.Equal(404, error.StatusCode);
        actor.Db.ChangeTracker.Clear();
        var unchanged = await actor.Db.Files.AsNoTracking().SingleAsync(row => row.Id == staged.Id);
        Assert.NotNull(unchanged.ExpiresOn);
        Assert.StartsWith(AuthoringFileStoragePath.Prefix, unchanged.BlobKey);
        Assert.False(await actor.Db.CaveImportSources.AnyAsync(row => row.Id == staged.Id));
        Assert.False(await actor.Db.CaveImportBatches.AnyAsync(row => row.SourceFileId == staged.Id));
    }

    [Fact]
    public async Task ImportSourceInAnotherAccountStorageContainerIsRejectedAtReadAndApply()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ImportSourceInAnotherAccountStorageContainerIsRejectedAtReadAndApply));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        string sourceId;
        string foreignContainer;

        await using (var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs))
        {
            var uploaded = await UploadAsync(actor,
                CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"), "wrong-container.csv");
            sourceId = uploaded.Id;
            actor.Db.ChangeTracker.Clear();
            var source = await actor.Db.CaveImportSources.SingleAsync(row => row.Id == sourceId);
            var bytes = blobs.Read(source.BlobContainer, source.BlobKey);
            foreignContainer = RequestUser.BuildAccountContainerName("foreign001");
            blobs.Seed(foreignContainer, source.BlobKey, bytes);
            source.BlobContainer = foreignContainer;
            await actor.Db.SaveChangesAsync();

            var readError = await Assert.ThrowsAsync<ApiException>(() =>
                actor.Services.Imports.ImportCavesFileProcess(
                    sourceId, isDryRun: true, syncExisting: true, default));
            Assert.Equal(400, readError.StatusCode);
        }

        CaveImportPlan plan;
        await using (var planning = database.CreateDbContext("planner", cave.AccountId))
        {
            var import = new CaveImportTestHarness(planning, planning.RequestUser);
            plan = await import.PlanCsvAsync(
                CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"),
                syncExisting: true);
        }

        await using var execution = database.CreateDbContext("executor", cave.AccountId);
        var executor = new CaveImportTestHarness(execution, execution.RequestUser);
        await Assert.ThrowsAsync<ImportPlanConcurrencyException>(() =>
            executor.ExecuteUploadedAsync(plan, sourceId));

        execution.ChangeTracker.Clear();
        var unchanged = await execution.CaveImportSources.AsNoTracking()
            .SingleAsync(row => row.Id == sourceId);
        Assert.Equal(foreignContainer, unchanged.BlobContainer);
        Assert.NotNull(unchanged.ExpiresOn);
        Assert.False(await execution.CaveImportBatches.AnyAsync(row => row.SourceFileId == sourceId));
    }

    [Fact]
    public async Task CaveDryRunLeavesSourceTemporaryAndApplyRetainsSameObjectWithRealFileName()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CaveDryRunLeavesSourceTemporaryAndApplyRetainsSameObjectWithRealFileName));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);
        var uploaded = await UploadAsync(actor,
            CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"), "real-source-name.csv");

        actor.Db.ChangeTracker.Clear();
        var before = await actor.Db.CaveImportSources.AsNoTracking().SingleAsync(row => row.Id == uploaded.Id);
        var expiration = before.ExpiresOn;
        var address = (before.BlobContainer, before.BlobKey);

        await actor.Services.Imports.ImportCavesFileProcess(
            uploaded.Id, isDryRun: true, syncExisting: true, default);
        actor.Db.ChangeTracker.Clear();
        var afterDryRun = await actor.Db.CaveImportSources.AsNoTracking()
            .SingleAsync(row => row.Id == uploaded.Id);
        Assert.Equal(expiration, afterDryRun.ExpiresOn);
        Assert.Equal(address, (afterDryRun.BlobContainer, afterDryRun.BlobKey));
        Assert.False(await actor.Db.CaveImportBatches.AnyAsync(row => row.SourceFileId == uploaded.Id));

        await actor.Services.Imports.ImportCavesFileProcess(
            uploaded.Id, isDryRun: false, syncExisting: true, default);
        actor.Db.ChangeTracker.Clear();
        var retained = await actor.Db.CaveImportSources.AsNoTracking()
            .SingleAsync(row => row.Id == uploaded.Id);
        Assert.Null(retained.ExpiresOn);
        Assert.Equal(address, (retained.BlobContainer, retained.BlobKey));
        Assert.True(blobs.Contains(retained.BlobContainer, retained.BlobKey));
        var batch = await actor.Db.CaveImportBatches.SingleAsync(row => row.SourceFileId == uploaded.Id);
        Assert.Equal("real-source-name.csv", batch.SourceFileName);
    }

    [Fact]
    public async Task EntranceApplyRetainsSourceAndLinksBatchToExactSource()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(EntranceApplyRetainsSourceAndLinksBatchToExactSource));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);
        var uploaded = await UploadAsync(actor,
            EntranceCsv("Main,A01,1,true,35.1,-86.2,612,Survey Grade,20,Open,Wet,Sink,2026-08-01,Surveyor,Description,Distinctive"),
            "entrances-source.csv");

        await actor.Services.Imports.ImportEntrancesFileProcess(
            uploaded.Id, isDryRun: false, syncExisting: false, default);

        actor.Db.ChangeTracker.Clear();
        var retained = await actor.Db.CaveImportSources.AsNoTracking().SingleAsync(row => row.Id == uploaded.Id);
        Assert.Null(retained.ExpiresOn);
        var batch = await actor.Db.CaveImportBatches.SingleAsync(row => row.SourceFileId == uploaded.Id);
        Assert.Equal(CaveImportKind.EntranceCsv, batch.Kind);
        Assert.Equal("entrances-source.csv", batch.SourceFileName);
        Assert.True(blobs.Contains(retained.BlobContainer, retained.BlobKey));
    }

    [Fact]
    public async Task NoChangeApplyStillRetainsSourceAndSameSourceCanBeAppliedAgain()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(NoChangeApplyStillRetainsSourceAndSameSourceCanBeAppliedAgain));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);
        var uploaded = await UploadAsync(actor,
            CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"), "repeat.csv");

        await actor.Services.Imports.ImportCavesFileProcess(
            uploaded.Id, isDryRun: false, syncExisting: true, default);
        actor.Db.ChangeTracker.Clear();
        var first = await actor.Db.CaveImportSources.AsNoTracking().SingleAsync(row => row.Id == uploaded.Id);
        Assert.Null(first.ExpiresOn);

        await actor.Services.Imports.ImportCavesFileProcess(
            uploaded.Id, isDryRun: false, syncExisting: true, default);
        actor.Db.ChangeTracker.Clear();

        var retained = await actor.Db.CaveImportSources.AsNoTracking().SingleAsync(row => row.Id == uploaded.Id);
        Assert.Null(retained.ExpiresOn);
        Assert.Equal(2, await actor.Db.CaveImportBatches.CountAsync(row => row.SourceFileId == uploaded.Id));
        Assert.True(blobs.Contains(retained.BlobContainer, retained.BlobKey));
    }

    [Fact]
    public async Task RetainedSourceCanBeDownloadedButTemporarySourceCannot()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RetainedSourceCanBeDownloadedButTemporarySourceCannot));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);
        var csv = CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,");
        var uploaded = await UploadAsync(actor, csv, "download-me.csv");

        var temporary = await Assert.ThrowsAsync<ApiException>(() =>
            actor.Services.Imports.OpenCsvImportSourceDownloadAsync(uploaded.Id, default));
        Assert.Equal(404, temporary.StatusCode);

        await actor.Services.Imports.ImportCavesFileProcess(
            uploaded.Id, isDryRun: false, syncExisting: true, default);

        var download = await actor.Services.Imports.OpenCsvImportSourceDownloadAsync(uploaded.Id, default);
        Assert.Equal("download-me.csv", download.FileName);
        await using var stream = download.Stream;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        Assert.Equal(csv, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task RetainedSourceDownloadReturnsNotFoundIfObjectDisappearsBeforeStreamOpens()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RetainedSourceDownloadReturnsNotFoundIfObjectDisappearsBeforeStreamOpens));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);
        var uploaded = await UploadAsync(actor,
            CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"), "vanished.csv");
        await actor.Services.Imports.ImportCavesFileProcess(
            uploaded.Id, isDryRun: false, syncExisting: true, default);

        // Object metadata was readable, but the bytes vanished just before the
        // second provider call opened the stream. This is an expected storage race.
        blobs.BeforeReadStream = address => blobs.Delete(address.Partition, address.Key);
        var error = await Assert.ThrowsAsync<ApiException>(() =>
            actor.Services.Imports.OpenCsvImportSourceDownloadAsync(uploaded.Id, default));
        Assert.Equal(404, error.StatusCode);

        actor.Db.ChangeTracker.Clear();
        Assert.True(await actor.Db.CaveImportSources.AnyAsync(source =>
            source.Id == uploaded.Id && source.ExpiresOn == null));
    }

    [Fact]
    public async Task RetainedSourceDownloadRejectsStorageOutsideActiveImportNamespace()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RetainedSourceDownloadRejectsStorageOutsideActiveImportNamespace));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);
        var csv = CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,");
        var uploaded = await UploadAsync(actor, csv, "wrong-download-container.csv");

        await actor.Services.Imports.ImportCavesFileProcess(
            uploaded.Id, isDryRun: false, syncExisting: true, default);

        actor.Db.ChangeTracker.Clear();
        var retained = await actor.Db.CaveImportSources.SingleAsync(row => row.Id == uploaded.Id);
        var foreignContainer = RequestUser.BuildAccountContainerName("foreign001");
        blobs.Seed(foreignContainer, retained.BlobKey, Encoding.UTF8.GetBytes(csv));
        retained.BlobContainer = foreignContainer;
        await actor.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            actor.Services.Imports.OpenCsvImportSourceDownloadAsync(uploaded.Id, default));

        Assert.Equal(400, error.StatusCode);
        Assert.Contains("import-source namespace", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyRejectsSourceThatExpiresAfterPlanningBeforePromotion()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApplyRejectsSourceThatExpiresAfterPlanningBeforePromotion));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var source = await SeedTemporaryImportSourceAsync(
            database, cave, "expireplan", "expires-after-plan.csv", DateTime.UtcNow.AddMinutes(5));

        CaveImportPlan plan;
        await using (var planning = database.CreateDbContext("planner", cave.AccountId))
        {
            var import = new CaveImportTestHarness(planning, planning.RequestUser);
            plan = await import.PlanCsvAsync(
                CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"),
                syncExisting: true);
        }

        await using (var expire = database.CreateDbContext("expire", cave.AccountId))
            await expire.CaveImportSources.IgnoreQueryFilters()
                .Where(row => row.Id == source.SourceId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.ExpiresOn, DateTime.UtcNow.AddMinutes(-1)));

        await using var execution = database.CreateDbContext("executor", cave.AccountId);
        var importHarness = new CaveImportTestHarness(execution, execution.RequestUser);
        var error = await Assert.ThrowsAsync<ImportPlanConcurrencyException>(() =>
            importHarness.ExecuteUploadedAsync(plan, source.SourceId));
        Assert.Contains("expired", error.Message, StringComparison.OrdinalIgnoreCase);

        execution.ChangeTracker.Clear();
        var unchanged = await execution.CaveImportSources.AsNoTracking()
            .SingleAsync(row => row.Id == source.SourceId);
        Assert.NotNull(unchanged.ExpiresOn);
        Assert.True(unchanged.ExpiresOn <= DateTime.UtcNow);
        Assert.False(await execution.CaveImportBatches.AnyAsync(batch => batch.SourceFileId == source.SourceId));
    }

    [Fact]
    public async Task ApplyRejectsSourceThatExpiresWhilePromotionUpdateIsPaused()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApplyRejectsSourceThatExpiresWhilePromotionUpdateIsPaused));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var source = await SeedTemporaryImportSourceAsync(
            database, cave, "expirelock", "expires-during-promotion.csv", DateTime.UtcNow.AddSeconds(2));

        CaveImportPlan plan;
        await using (var planning = database.CreateDbContext("planner", cave.AccountId))
        {
            var import = new CaveImportTestHarness(planning, planning.RequestUser);
            plan = await import.PlanCsvAsync(
                CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"),
                syncExisting: true);
        }

        var pause = new PauseOnSqlCommandInterceptor("UPDATE \"CaveImportSources\"");
        await using var execution = database.CreateDbContext("executor", cave.AccountId, pause);
        var importTask = new CaveImportTestHarness(execution, execution.RequestUser)
            .ExecuteUploadedAsync(plan, source.SourceId);

        await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var wait = source.ExpiresOn - DateTime.UtcNow + TimeSpan.FromMilliseconds(100);
        if (wait > TimeSpan.Zero)
            await Task.Delay(wait);

        pause.Continue();
        var error = await Assert.ThrowsAsync<ImportPlanConcurrencyException>(() => importTask);
        Assert.Contains("expired", error.Message, StringComparison.OrdinalIgnoreCase);

        execution.ChangeTracker.Clear();
        var unchanged = await execution.CaveImportSources.AsNoTracking()
            .SingleAsync(row => row.Id == source.SourceId);
        Assert.NotNull(unchanged.ExpiresOn);
        Assert.True(unchanged.ExpiresOn <= DateTime.UtcNow);
        Assert.False(await execution.CaveImportBatches.AnyAsync(batch => batch.SourceFileId == source.SourceId));
    }

    [Fact]
    public async Task RetentionCommitWinsRaceAgainstExpiredSourceCleanup()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RetentionCommitWinsRaceAgainstExpiredSourceCleanup));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var source = await SeedTemporaryImportSourceAsync(
            database, cave, "racesrc001", "race.csv", DateTime.UtcNow.AddSeconds(2));

        CaveImportPlan plan;
        await using (var planning = database.CreateDbContext("planner", cave.AccountId))
        {
            var import = new CaveImportTestHarness(planning, planning.RequestUser);
            plan = await import.PlanCsvAsync(
                CaveCsv("Race Winner,County A,A01,1,AA,,,,200,50,12,2,,,,,,2026-08-01,,false,,Race"),
                syncExisting: true);
        }

        var pause = new PauseOnSqlCommandInterceptor("UPDATE \"Caves\"");
        await using var execution = database.CreateDbContext("executor", cave.AccountId, pause);
        var importTask = new CaveImportTestHarness(execution, execution.RequestUser)
            .ExecuteUploadedAsync(plan, source.SourceId);

        await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var wait = source.ExpiresOn - DateTime.UtcNow + TimeSpan.FromMilliseconds(100);
        if (wait > TimeSpan.Zero)
            await Task.Delay(wait);

        await using var cleanup = database.CreateDbContext("cleanup", cave.AccountId);
        await cleanup.Database.OpenConnectionAsync();
        int cleanupBackendPid;
        await using (var command = cleanup.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "select pg_backend_pid()";
            cleanupBackendPid = Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        var cleanupTask = new ImportSourceStore(cleanup, cleanup.RequestUser)
            .RemoveExpiredAsync(16, default);
        await WaitForLockWaitAsync(database, cave.AccountId, cleanupBackendPid);

        pause.Continue();
        await importTask;
        Assert.Empty(await cleanupTask);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var retained = await verify.CaveImportSources.AsNoTracking()
            .SingleAsync(row => row.Id == source.SourceId);
        Assert.Null(retained.ExpiresOn);
        Assert.True(await verify.CaveImportBatches.AnyAsync(row => row.SourceFileId == source.SourceId));
        Assert.False(await verify.StorageCleanupTargets.AnyAsync(row =>
            row.StoragePartition == retained.BlobContainer && row.StorageKey == retained.BlobKey));
    }

    [Fact]
    public async Task CaveExecutionFailureRollsBackSourcePromotionAndBatch()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CaveExecutionFailureRollsBackSourcePromotionAndBatch));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var source = await SeedTemporaryImportSourceAsync(
            database, cave, "srcfailc01", "rollback-cave.csv");

        CaveImportPlan plan;
        await using (var planning = database.CreateDbContext("planner", cave.AccountId))
        {
            var import = new CaveImportTestHarness(planning, planning.RequestUser);
            plan = await import.PlanCsvAsync(
                CaveCsv("Rollback Cave,County A,A01,1,AA,,,,200,50,12,2,,,,,,2026-08-01,,false,,Rollback"),
                syncExisting: true);
        }

        var fail = new ThrowOnSqlCommandInterceptor("UPDATE \"Caves\"");
        await using (var execution = database.CreateDbContext("executor", cave.AccountId, fail))
        {
            var import = new CaveImportTestHarness(execution, execution.RequestUser);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
                import.ExecuteUploadedAsync(plan, source.SourceId));
            Assert.IsType<InjectedImportFailure>(error.InnerException);
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var persisted = await verify.CaveImportSources.AsNoTracking()
            .SingleAsync(row => row.Id == source.SourceId);
        Assert.Equal(source.ExpiresOn, persisted.ExpiresOn);
        Assert.False(await verify.CaveImportBatches.AnyAsync(row => row.SourceFileId == source.SourceId));
        Assert.Equal(cave.RevisionId,
            (await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId)).CurrentRevisionId);
        Assert.True(fail.WasTriggered);
    }

    [Fact]
    public async Task EntranceExecutionFailureRollsBackSourcePromotionAndBatch()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(EntranceExecutionFailureRollsBackSourcePromotionAndBatch));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var source = await SeedTemporaryImportSourceAsync(
            database, cave, "srcfaile01", "rollback-entrance.csv");

        EntranceImportPlan plan;
        await using (var planning = database.CreateDbContext("planner", cave.AccountId))
        {
            var import = new EntranceImportTestHarness(planning, planning.RequestUser);
            plan = await import.PlanCsvAsync(
                EntranceCsv("Main,A01,1,true,35.1,-86.2,612,Survey Grade,20,Open,Wet,Sink,2026-08-01,Surveyor,Description,Distinctive"),
                syncExisting: false);
        }

        var fail = new ThrowOnSqlCommandInterceptor("INSERT INTO \"Entrances\"");
        await using (var execution = database.CreateDbContext("executor", cave.AccountId, fail))
        {
            var import = new EntranceImportTestHarness(execution, execution.RequestUser);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
                import.ExecuteUploadedAsync(plan, source.SourceId));
            Assert.IsType<InjectedImportFailure>(error.InnerException);
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var persisted = await verify.CaveImportSources.AsNoTracking()
            .SingleAsync(row => row.Id == source.SourceId);
        Assert.Equal(source.ExpiresOn, persisted.ExpiresOn);
        Assert.False(await verify.CaveImportBatches.AnyAsync(row => row.SourceFileId == source.SourceId));
        Assert.Empty(await verify.Entrances.IgnoreQueryFilters()
            .Where(row => row.CaveId == cave.CaveId).ToListAsync());
        Assert.True(fail.WasTriggered);
    }

    [Fact]
    public async Task ExpiredCleanupDeletesUnappliedUploadButPreservesAppliedSourceObject()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ExpiredCleanupDeletesUnappliedUploadButPreservesAppliedSourceObject));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);

        var retained = await UploadAsync(actor,
            CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"), "retained.csv");
        await actor.Services.Imports.ImportCavesFileProcess(
            retained.Id, isDryRun: false, syncExisting: true, default);

        var expired = await UploadAsync(actor,
            CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"), "abandoned.csv");
        actor.Db.ChangeTracker.Clear();

        var expiredRow = await actor.Db.CaveImportSources.SingleAsync(row => row.Id == expired.Id);
        expiredRow.ExpiresOn = DateTime.UtcNow.AddMinutes(-1);
        var expiredAddress = (expiredRow.BlobContainer, expiredRow.BlobKey);

        var retainedRow = await actor.Db.CaveImportSources.SingleAsync(row => row.Id == retained.Id);
        var retainedAddress = (retainedRow.BlobContainer, retainedRow.BlobKey);
        Assert.Null(retainedRow.ExpiresOn);
        retainedRow.ExpiresOn = DateTime.UtcNow.AddMinutes(-1); // simulate stale/corrupt legacy deadline
        await actor.Db.SaveChangesAsync();

        // A later source upload opportunistically sweeps one bounded batch.
        await UploadAsync(actor,
            CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"), "cleanup-trigger.csv");

        actor.Db.ChangeTracker.Clear();
        Assert.True(await actor.Db.CaveImportSources.AnyAsync(row => row.Id == retained.Id));
        Assert.True(await actor.Db.CaveImportBatches.AnyAsync(row => row.SourceFileId == retained.Id));
        Assert.False(await actor.Db.CaveImportSources.AnyAsync(row => row.Id == expired.Id));
        Assert.True(blobs.Contains(retainedAddress.BlobContainer, retainedAddress.BlobKey));
        Assert.False(blobs.Contains(expiredAddress.BlobContainer, expiredAddress.BlobKey));
        Assert.False(await actor.Db.StorageCleanupTargets.AnyAsync(row =>
            row.StoragePartition == retainedAddress.BlobContainer &&
            row.StorageKey == retainedAddress.BlobKey));
    }

    [Fact]
    public async Task ExpiredSourceCleanupClaimsOnlyOneBoundedBatch()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ExpiredSourceCleanupClaimsOnlyOneBoundedBatch));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');

        await using (var seed = database.CreateDbContext("seed", cave.AccountId))
        {
            for (var index = 1; index <= 17; index++)
            {
                var id = $"exp{index:0000000}";
                seed.CaveImportSources.Add(new CaveImportSource
                {
                    Id = id,
                    AccountId = cave.AccountId,
                    FileName = $"{id}.csv",
                    BlobContainer = RequestUser.BuildAccountContainerName(cave.AccountId),
                    BlobKey = ImportSourceStoragePath.BuildCaveEntranceCsvKey(id),
                    ExpiresOn = DateTime.UtcNow.AddMinutes(-1)
                });
            }
            await seed.SaveChangesAsync();
        }

        await using var cleanup = database.CreateDbContext("cleanup", cave.AccountId);
        var targetIds = await new ImportSourceStore(cleanup, cleanup.RequestUser)
            .RemoveExpiredAsync(16, default);

        Assert.Equal(16, targetIds.Count);
        cleanup.ChangeTracker.Clear();
        var remaining = await cleanup.CaveImportSources.AsNoTracking()
            .OrderBy(row => row.Id).Select(row => row.Id).ToListAsync();
        Assert.Equal(["exp0000017"], remaining);
    }

    [Fact]
    public async Task CaveSyncDeleteCleansPublishedAttachmentButNeverRetainedSourceCsv()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CaveSyncDeleteCleansPublishedAttachmentButNeverRetainedSourceCsv));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var published = await FileTestDataFactory.AddFileAsync(
            database, cave, associateWithCave: true, fileId: "pubdelete1");
        var blobs = new TestFileBlobStore();
        blobs.Seed("test", "seed-a", [9, 8, 7]);

        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);
        var source = await UploadAsync(actor,
            CaveCsv("Replacement,Replacement County,REP,2,AA,,,,10,2,1,1,,,,,,,,false,,Replacement"),
            "sync-delete-source.csv");
        actor.Db.ChangeTracker.Clear();
        var sourceBefore = await actor.Db.CaveImportSources.AsNoTracking()
            .SingleAsync(row => row.Id == source.Id);
        var sourceAddress = (sourceBefore.BlobContainer, sourceBefore.BlobKey);

        await actor.Services.Imports.ImportCavesFileProcess(
            source.Id, isDryRun: false, syncExisting: true, default);

        actor.Db.ChangeTracker.Clear();
        Assert.False(await actor.Db.Caves.IgnoreQueryFilters().AnyAsync(row => row.Id == cave.CaveId));
        Assert.False(await actor.Db.Files.IgnoreQueryFilters().AnyAsync(row => row.Id == published.FileId));
        Assert.False(blobs.Contains("test", "seed-a"));

        var retained = await actor.Db.CaveImportSources.AsNoTracking()
            .SingleAsync(row => row.Id == source.Id);
        Assert.Null(retained.ExpiresOn);
        Assert.Equal(sourceAddress, (retained.BlobContainer, retained.BlobKey));
        Assert.True(blobs.Contains(sourceAddress.BlobContainer, sourceAddress.BlobKey));
        Assert.True(await actor.Db.CaveImportBatches.AnyAsync(row => row.SourceFileId == source.Id));
        Assert.False(await actor.Db.StorageCleanupTargets.AnyAsync(row =>
            row.StoragePartition == sourceAddress.BlobContainer &&
            row.StorageKey == sourceAddress.BlobKey));
    }

    [Fact]
    public async Task AuthoringStagedDeletionCannotDeleteRetainedImportSource()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AuthoringStagedDeletionCannotDeleteRetainedImportSource));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var blobs = new TestFileBlobStore();
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "importer", blobs);
        var source = await UploadAsync(actor,
            CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"), "retained.csv");
        await actor.Services.Imports.ImportCavesFileProcess(
            source.Id, isDryRun: false, syncExisting: true, default);

        actor.Db.ChangeTracker.Clear();
        var retained = await actor.Db.CaveImportSources.AsNoTracking()
            .SingleAsync(row => row.Id == source.Id);
        var address = (retained.BlobContainer, retained.BlobKey);

        await actor.Services.Files.DeleteAuthoringStagedFileAsync(source.Id, default);

        actor.Db.ChangeTracker.Clear();
        Assert.True(await actor.Db.CaveImportSources.AnyAsync(row => row.Id == source.Id));
        Assert.True(await actor.Db.CaveImportBatches.AnyAsync(row => row.SourceFileId == source.Id));
        Assert.True(blobs.Contains(address.BlobContainer, address.BlobKey));
        Assert.False(await actor.Db.StorageCleanupTargets.AnyAsync(row =>
            row.StoragePartition == address.BlobContainer && row.StorageKey == address.BlobKey));
    }

    [Fact]
    public async Task UploadedSourceMustBelongToActiveAccount()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(UploadedSourceMustBelongToActiveAccount));
        var caveA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var caveB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var sourceB = await SeedTemporaryImportSourceAsync(
            database, caveB, "sourceb001", "foreign.csv");

        CaveImportPlan plan;
        await using (var planning = database.CreateDbContext("planner-a", caveA.AccountId))
        {
            var import = new CaveImportTestHarness(planning, planning.RequestUser);
            plan = await import.PlanCsvAsync(
                CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"),
                syncExisting: true);
        }

        await using var db = database.CreateDbContext("executor-a", caveA.AccountId);
        var executor = new CaveImportTestHarness(db, db.RequestUser);
        await Assert.ThrowsAsync<ImportPlanConcurrencyException>(() =>
            executor.ExecuteUploadedAsync(plan, sourceB.SourceId));

        await using var verify = database.CreateDbContext("verify-b", caveB.AccountId);
        Assert.Equal(sourceB.ExpiresOn,
            (await verify.CaveImportSources.AsNoTracking()
                .SingleAsync(row => row.Id == sourceB.SourceId)).ExpiresOn);
        Assert.False(await verify.CaveImportBatches.AnyAsync(row => row.SourceFileId == sourceB.SourceId));
    }

    [Fact]
    public async Task GenericAuthoringStagedFileCannotBecomeImportSource()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(GenericAuthoringStagedFileCannotBecomeImportSource));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var staged = await FileTestDataFactory.AddFileAsync(
            database, cave, associateWithCave: false, fileId: "authorcsv1",
            name: "authoring", extension: ".csv");

        await using (var seed = database.CreateDbContext("staged-seed", cave.AccountId))
        {
            var file = await seed.Files.SingleAsync(row => row.Id == staged.FileId);
            file.BlobContainer = RequestUser.BuildAccountContainerName(cave.AccountId);
            file.BlobKey = AuthoringFileStoragePath.BuildKey(file.Id);
            file.ExpiresOn = DateTime.UtcNow.AddDays(10);
            await seed.SaveChangesAsync();
        }

        CaveImportPlan plan;
        await using (var planning = database.CreateDbContext("planner", cave.AccountId))
        {
            var import = new CaveImportTestHarness(planning, planning.RequestUser);
            plan = await import.PlanCsvAsync(
                CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"),
                syncExisting: true);
        }

        await using var db = database.CreateDbContext("executor", cave.AccountId);
        var executor = new CaveImportTestHarness(db, db.RequestUser);
        await Assert.ThrowsAsync<ImportPlanConcurrencyException>(() =>
            executor.ExecuteUploadedAsync(plan, staged.FileId));

        db.ChangeTracker.Clear();
        var unchanged = await db.Files.AsNoTracking().SingleAsync(row => row.Id == staged.FileId);
        Assert.NotNull(unchanged.ExpiresOn);
        Assert.Equal(AuthoringFileStoragePath.BuildKey(staged.FileId), unchanged.BlobKey);
        Assert.False(await db.CaveImportSources.AnyAsync(row => row.Id == staged.FileId));
        Assert.False(await db.CaveImportBatches.AnyAsync(row => row.SourceFileId == staged.FileId));
    }

    [Fact]
    public async Task CaveAssociatedFileCannotBecomeImportSource()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CaveAssociatedFileCannotBecomeImportSource));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var attached = await FileTestDataFactory.AddFileAsync(
            database, cave, associateWithCave: true, fileId: "attached01",
            name: "attached", extension: ".csv");

        CaveImportPlan plan;
        await using (var planning = database.CreateDbContext("planner", cave.AccountId))
        {
            var import = new CaveImportTestHarness(planning, planning.RequestUser);
            plan = await import.PlanCsvAsync(
                CaveCsv("Cave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,"),
                syncExisting: true);
        }

        await using var db = database.CreateDbContext("executor", cave.AccountId);
        var executor = new CaveImportTestHarness(db, db.RequestUser);
        await Assert.ThrowsAsync<ImportPlanConcurrencyException>(() =>
            executor.ExecuteUploadedAsync(plan, attached.FileId));

        Assert.False(await db.CaveImportSources.AnyAsync(row => row.Id == attached.FileId));
        Assert.False(await db.CaveImportBatches.AnyAsync(row => row.SourceFileId == attached.FileId));
        Assert.True(await db.Files.AnyAsync(row =>
            row.Id == attached.FileId && row.CaveId == cave.CaveId));
    }

    private static Task<ImportSourceUploadVm> UploadAsync(
        CaveTestActor actor,
        string csv,
        string fileName) =>
        actor.Services.Imports.UploadCsvImportSourceAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(csv)), fileName, uuid: null, default);

    private static string CaveCsv(string row) =>
        ImportDryRunIntegrationTests.CaveHeader + "\n" + row + "\n";

    private static string EntranceCsv(string row) =>
        ImportDryRunIntegrationTests.EntranceHeader + "\n" + row + "\n";

    private static async Task<TemporarySource> SeedTemporaryImportSourceAsync(
        PostgresTestDatabase database,
        PublishedCaveTestData cave,
        string sourceId,
        string fileName,
        DateTime? expiresOn = null)
    {
        var effectiveExpiration = expiresOn ?? DateTime.UtcNow.AddDays(7);
        await using var db = database.CreateDbContext("source-seed", cave.AccountId);
        db.CaveImportSources.Add(new CaveImportSource
        {
            Id = sourceId,
            AccountId = cave.AccountId,
            FileName = fileName,
            BlobContainer = RequestUser.BuildAccountContainerName(cave.AccountId),
            BlobKey = ImportSourceStoragePath.BuildCaveEntranceCsvKey(sourceId),
            ExpiresOn = effectiveExpiration
        });
        await db.SaveChangesAsync();
        return new TemporarySource(sourceId, effectiveExpiration);
    }

    private static async Task WaitForLockWaitAsync(
        PostgresTestDatabase database,
        string accountId,
        int backendPid)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var observer = database.CreateDbContext("lock-observer", accountId);
            await observer.Database.OpenConnectionAsync();
            await using var command = observer.Database.GetDbConnection().CreateCommand();
            command.CommandText = """select "wait_event_type" from pg_stat_activity where pid = @pid""";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "pid";
            parameter.Value = backendPid;
            command.Parameters.Add(parameter);
            if (string.Equals(await command.ExecuteScalarAsync() as string, "Lock", StringComparison.Ordinal))
                return;
            await Task.Delay(50);
        }

        throw new TimeoutException("Expired import-source cleanup did not wait on the source row lock.");
    }

    private sealed record TemporarySource(string SourceId, DateTime ExpiresOn);

    private sealed class PauseOnSqlCommandInterceptor(string fragment) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _continue =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Reached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Continue() => _continue.TrySetResult();

        private async Task PauseIfMatchAsync(DbCommand command, CancellationToken cancellationToken)
        {
            if (!command.CommandText.Contains(fragment, StringComparison.Ordinal)) return;
            Reached.TrySetResult();
            await _continue.Task.WaitAsync(cancellationToken);
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await PauseIfMatchAsync(command, cancellationToken);
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await PauseIfMatchAsync(command, cancellationToken);
            return result;
        }
    }

    private sealed class InjectedImportFailure : Exception;

    private sealed class ThrowOnSqlCommandInterceptor(string fragment) : DbCommandInterceptor
    {
        public bool WasTriggered { get; private set; }

        private void ThrowIfMatch(DbCommand command)
        {
            if (!command.CommandText.Contains(fragment, StringComparison.Ordinal)) return;
            WasTriggered = true;
            throw new InjectedImportFailure();
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            ThrowIfMatch(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfMatch(command);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            ThrowIfMatch(command);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfMatch(command);
            return ValueTask.FromResult(result);
        }
    }
}
