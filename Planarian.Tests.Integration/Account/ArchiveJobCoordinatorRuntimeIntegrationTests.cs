using System.Formats.Tar;
using System.IO.Compression;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Planarian.Library.Exceptions;
using Planarian.Library.Options;
using Planarian.Model.Database;
using Planarian.Modules.Account.Archive.Models;
using Planarian.Modules.Account.Archive.Services;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Notifications.Hubs;
using Planarian.Modules.Notifications.Services;
using Planarian.Shared.Models;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

namespace Planarian.Tests.Integration.AccountArchive;

public sealed class ArchiveJobCoordinatorRuntimeIntegrationTests(
    PostgresTestServer postgres,
    AzuriteTestServer azurite)
    : IClassFixture<PostgresTestServer>, IClassFixture<AzuriteTestServer>
{
    [Fact]
    public async Task SuccessfulJobPublishesReadableArchiveAndRemovesTemporaryObject()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(SuccessfulJobPublishesReadableArchiveAndRemovesTemporaryObject));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await using var db = database.CreateDbContext("archive-success", cave.AccountId);
        await EnsureContainerAsync(db.RequestUser.AccountContainerName);
        await using var harness = CreateHarness(database, db);

        Assert.True(await harness.Coordinator.StartArchiveJobAsync(
            cave.AccountId, db.RequestUser.AccountContainerName));

        var terminal = await harness.Notifications.WaitForTerminalAsync();
        Assert.True(terminal.State == ProgressState.Completed, $"Expected Completed, got {terminal.State}: {terminal.StatusMessage}");
        Assert.Equal("Archive ready.", terminal.StatusMessage);

        var container = new BlobServiceClient(azurite.ConnectionString)
            .GetBlobContainerClient(db.RequestUser.AccountContainerName);
        var finalBlobs = await ListBlobsAsync(container, ArchiveJobCoordinator.ArchivePrefix, includeTemp: false);
        var tempBlobs = await ListBlobsAsync(container, ArchiveJobCoordinator.TempArchivePrefix, includeTemp: true);

        var final = Assert.Single(finalBlobs);
        Assert.Empty(tempBlobs);
        Assert.Contains("/archive", "/" + final.Name, StringComparison.OrdinalIgnoreCase);
        var content = await container.GetBlobClient(final.Name).DownloadContentAsync();
        AssertArchiveHasCoreCsvEntries(content.Value.Content.ToArray());
    }

    [Fact]
    public async Task CancellationAtArchiveCreationLeavesNoPublishedOrTemporaryArchiveAndCanRetry()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(CancellationAtArchiveCreationLeavesNoPublishedOrTemporaryArchiveAndCanRetry));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');

        await using (var db = database.CreateDbContext("archive-cancel", cave.AccountId))
        {
            await EnsureContainerAsync(db.RequestUser.AccountContainerName);
            await using var harness = CreateHarness(database, db, blockAtCreatingArchive: true);
            Assert.True(await harness.Coordinator.StartArchiveJobAsync(
                cave.AccountId, db.RequestUser.AccountContainerName));

            await harness.Notifications.WaitUntilCreatingArchiveAsync();
            harness.Coordinator.CancelArchiveJob(cave.AccountId);
            harness.Notifications.ReleaseCreatingArchive();

            var terminal = await harness.Notifications.WaitForTerminalAsync();
            Assert.Equal(ProgressState.Canceled, terminal.State);

            var container = new BlobServiceClient(azurite.ConnectionString)
                .GetBlobContainerClient(db.RequestUser.AccountContainerName);
            Assert.Empty(await ListBlobsAsync(container, ArchiveJobCoordinator.ArchivePrefix, includeTemp: false));
            Assert.Empty(await ListBlobsAsync(container, ArchiveJobCoordinator.TempArchivePrefix, includeTemp: true));
        }

        // Retry after cancellation must acquire a fresh cross-replica lease and publish normally.
        await using (var retryDb = database.CreateDbContext("archive-retry", cave.AccountId))
        {
            await EnsureContainerAsync(retryDb.RequestUser.AccountContainerName);
            await using var retry = CreateHarness(database, retryDb);
            Assert.True(await retry.Coordinator.StartArchiveJobAsync(
                cave.AccountId, retryDb.RequestUser.AccountContainerName));
            var terminal = await retry.Notifications.WaitForTerminalAsync();
            Assert.Equal(ProgressState.Completed, terminal.State);
        }
    }

    [Fact]
    public async Task FinalPublicationConflictDoesNotOverwriteAndRetryPublishesFreshArchive()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(FinalPublicationConflictDoesNotOverwriteAndRetryPublishesFreshArchive));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'p');
        string failedFinalKey;

        await using (var db = database.CreateDbContext("archive-publication-failure", cave.AccountId))
        {
            await EnsureContainerAsync(db.RequestUser.AccountContainerName);
            await using var harness = CreateHarness(database, db, blockAtCreatingArchive: true);

            Assert.True(await harness.Coordinator.StartArchiveJobAsync(
                cave.AccountId, db.RequestUser.AccountContainerName));
            await harness.Notifications.WaitUntilCreatingArchiveAsync();

            var finalKey = Assert.IsType<string>(harness.Coordinator.GetActiveArchiveBlobKey(cave.AccountId));
            failedFinalKey = finalKey;
            var tempKey = ArchiveJobCoordinator.TempArchivePrefix +
                          finalKey[ArchiveJobCoordinator.ArchivePrefix.Length..];
            var container = new BlobServiceClient(azurite.ConnectionString)
                .GetBlobContainerClient(db.RequestUser.AccountContainerName);
            var sentinel = new byte[] { 0xde, 0xad, 0xbe, 0xef };
            await container.GetBlobClient(finalKey).UploadAsync(new BinaryData(sentinel), overwrite: false);

            harness.Notifications.ReleaseCreatingArchive();
            var terminal = await harness.Notifications.WaitForTerminalAsync();
            Assert.Equal(ProgressState.Failed, terminal.State);
            Assert.Contains("different content", terminal.StatusMessage, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(sentinel, (await container.GetBlobClient(finalKey).DownloadContentAsync())
                .Value.Content.ToArray());
            Assert.False((await container.GetBlobClient(tempKey).ExistsAsync()).Value);
        }

        await using (var retryDb = database.CreateDbContext("archive-publication-retry", cave.AccountId))
        {
            await using var retry = CreateHarness(database, retryDb);
            Assert.True(await retry.Coordinator.StartArchiveJobAsync(
                cave.AccountId, retryDb.RequestUser.AccountContainerName));
            Assert.Equal(ProgressState.Completed, (await retry.Notifications.WaitForTerminalAsync()).State);

            var container = new BlobServiceClient(azurite.ConnectionString)
                .GetBlobContainerClient(retryDb.RequestUser.AccountContainerName);
            var finalBlobs = await ListBlobsAsync(
                container, ArchiveJobCoordinator.ArchivePrefix, includeTemp: false);
            Assert.Equal(2, finalBlobs.Count);
            var retryFinal = Assert.Single(finalBlobs.Where(blob =>
                !string.Equals(blob.Name, failedFinalKey, StringComparison.Ordinal)));
            var retryContent = await container.GetBlobClient(retryFinal.Name).DownloadContentAsync();
            AssertArchiveHasCoreCsvEntries(retryContent.Value.Content.ToArray());
        }
    }

    [Fact]
    public async Task SameAccountIsSerializedAcrossCoordinatorInstancesWhileFirstJobIsActive()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(SameAccountIsSerializedAcrossCoordinatorInstancesWhileFirstJobIsActive));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'c');

        await using var firstDb = database.CreateDbContext("archive-replica-1", cave.AccountId);
        await using var secondDb = database.CreateDbContext("archive-replica-2", cave.AccountId);
        await EnsureContainerAsync(firstDb.RequestUser.AccountContainerName);
        await using var first = CreateHarness(database, firstDb, blockAtCreatingArchive: true);
        await using var second = CreateHarness(database, secondDb);

        Assert.True(await first.Coordinator.StartArchiveJobAsync(
            cave.AccountId, firstDb.RequestUser.AccountContainerName));
        await first.Notifications.WaitUntilCreatingArchiveAsync();

        Assert.False(await second.Coordinator.StartArchiveJobAsync(
            cave.AccountId, secondDb.RequestUser.AccountContainerName));

        first.Coordinator.CancelArchiveJob(cave.AccountId);
        first.Notifications.ReleaseCreatingArchive();
        Assert.Equal(ProgressState.Canceled, (await first.Notifications.WaitForTerminalAsync()).State);

        Assert.True(await second.Coordinator.StartArchiveJobAsync(
            cave.AccountId, secondDb.RequestUser.AccountContainerName));
        Assert.Equal(ProgressState.Completed, (await second.Notifications.WaitForTerminalAsync()).State);
    }


    [Fact]
    public async Task ActiveArchiveBlocksResetFromAnotherCoordinatorUntilArchiveLeaseReleases()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(ActiveArchiveBlocksResetFromAnotherCoordinatorUntilArchiveLeaseReleases));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'r');

        await using var archiveDb = database.CreateDbContext("archive-reset-race", cave.AccountId);
        await EnsureContainerAsync(archiveDb.RequestUser.AccountContainerName);
        await using var archive = CreateHarness(database, archiveDb, blockAtCreatingArchive: true);

        Assert.True(await archive.Coordinator.StartArchiveJobAsync(
            cave.AccountId, archiveDb.RequestUser.AccountContainerName));
        await archive.Notifications.WaitUntilCreatingArchiveAsync();

        await using var resetDb = database.CreateDbContext("reset-other-replica", cave.AccountId);
        var resetServices = IntegrationTestServices.For(
            resetDb,
            maintenanceConnectionString: database.ConnectionString);

        var conflict = await Assert.ThrowsAsync<ApiException>(() =>
            resetServices.Account.ResetAccount(CancellationToken.None));
        Assert.Equal(409, conflict.StatusCode);

        await using (var verify = database.CreateDbContext("reset-race-verify", cave.AccountId))
        {
            Assert.True(await verify.Caves.IgnoreQueryFilters()
                .AnyAsync(candidate => candidate.Id == cave.CaveId));
        }

        archive.Coordinator.CancelArchiveJob(cave.AccountId);
        archive.Notifications.ReleaseCreatingArchive();
        Assert.Equal(ProgressState.Canceled, (await archive.Notifications.WaitForTerminalAsync()).State);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var completed = await resetServices.Account.ResetAccount(CancellationToken.None);
                Assert.True(completed.RelationalResetCompleted);
                break;
            }
            catch (ApiException ex) when (ex.StatusCode == 409 && attempt < 60)
            {
                // The terminal notification is emitted just before the archive task's
                // PostgreSQL advisory lease is disposed. Wait for that cross-replica
                // lease boundary instead of introducing a timing-dependent assertion.
                await Task.Delay(50);
            }
        }
    }

    [Fact]
    public async Task ActiveArchiveBlocksArchiveDeleteFromAnotherCoordinatorUntilLeaseReleases()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(ActiveArchiveBlocksArchiveDeleteFromAnotherCoordinatorUntilLeaseReleases));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'x');

        await using var archiveDb = database.CreateDbContext("archive-delete-race", cave.AccountId);
        await EnsureContainerAsync(archiveDb.RequestUser.AccountContainerName);
        var container = new BlobServiceClient(azurite.ConnectionString)
            .GetBlobContainerClient(archiveDb.RequestUser.AccountContainerName);
        const string existingArchiveKey = "archives/existing/archive.tar.gz";
        await container.GetBlobClient(existingArchiveKey)
            .UploadAsync(new BinaryData(new byte[] { 1, 2, 3, 4 }), overwrite: false);

        await using var archive = CreateHarness(database, archiveDb, blockAtCreatingArchive: true);
        Assert.True(await archive.Coordinator.StartArchiveJobAsync(
            cave.AccountId, archiveDb.RequestUser.AccountContainerName));
        await archive.Notifications.WaitUntilCreatingArchiveAsync();

        await using var deleteDb = database.CreateDbContext("archive-delete-other-replica", cave.AccountId);
        var deleteServices = IntegrationTestServices.For(
            deleteDb,
            maintenanceConnectionString: database.ConnectionString,
            fileConnectionString: azurite.ConnectionString);

        var conflict = await Assert.ThrowsAsync<ApiException>(() =>
            deleteServices.Account.DeleteArchive(existingArchiveKey, CancellationToken.None));
        Assert.Equal(409, conflict.StatusCode);
        Assert.True((await container.GetBlobClient(existingArchiveKey).ExistsAsync()).Value);

        archive.Coordinator.CancelArchiveJob(cave.AccountId);
        archive.Notifications.ReleaseCreatingArchive();
        Assert.Equal(ProgressState.Canceled, (await archive.Notifications.WaitForTerminalAsync()).State);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await deleteServices.Account.DeleteArchive(existingArchiveKey, CancellationToken.None);
                break;
            }
            catch (ApiException ex) when (ex.StatusCode == 409 && attempt < 60)
            {
                await Task.Delay(50);
            }
        }

        Assert.False((await container.GetBlobClient(existingArchiveKey).ExistsAsync()).Value);
    }

    [Fact]
    public async Task SeparateAccountsCanGenerateArchivesConcurrently()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(SeparateAccountsCanGenerateArchivesConcurrently));
        var caveA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'd');
        var caveB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'e');

        await using var dbA = database.CreateDbContext("archive-concurrent-a", caveA.AccountId);
        await using var dbB = database.CreateDbContext("archive-concurrent-b", caveB.AccountId);
        await EnsureContainerAsync(dbA.RequestUser.AccountContainerName);
        await EnsureContainerAsync(dbB.RequestUser.AccountContainerName);
        await using var harnessA = CreateHarness(database, dbA);
        await using var harnessB = CreateHarness(database, dbB);

        var starts = await Task.WhenAll(
            harnessA.Coordinator.StartArchiveJobAsync(caveA.AccountId, dbA.RequestUser.AccountContainerName),
            harnessB.Coordinator.StartArchiveJobAsync(caveB.AccountId, dbB.RequestUser.AccountContainerName));
        Assert.All(starts, Assert.True);

        var terminals = await Task.WhenAll(
            harnessA.Notifications.WaitForTerminalAsync(),
            harnessB.Notifications.WaitForTerminalAsync());
        Assert.All(terminals, terminal =>
            Assert.True(terminal.State == ProgressState.Completed,
                $"Expected Completed, got {terminal.State}: {terminal.StatusMessage}"));

        foreach (var containerName in new[]
                 {
                     dbA.RequestUser.AccountContainerName,
                     dbB.RequestUser.AccountContainerName
                 })
        {
            var container = new BlobServiceClient(azurite.ConnectionString)
                .GetBlobContainerClient(containerName);
            Assert.Single(await ListBlobsAsync(container, ArchiveJobCoordinator.ArchivePrefix, includeTemp: false));
            Assert.Empty(await ListBlobsAsync(container, ArchiveJobCoordinator.TempArchivePrefix, includeTemp: true));
        }
    }

    [Fact]
    public async Task PruneFailureDoesNotInvalidateNewlyPublishedArchive()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(PruneFailureDoesNotInvalidateNewlyPublishedArchive));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'f');
        await using var db = database.CreateDbContext("archive-prune-failure", cave.AccountId);

        var container = new BlobServiceClient(azurite.ConnectionString)
            .GetBlobContainerClient(db.RequestUser.AccountContainerName);
        await container.CreateIfNotExistsAsync();

        var prior = new List<string>();
        for (var index = 0; index < 5; index++)
        {
            var key = $"archives/prior-{index:D2}.tar.gz";
            await container.GetBlobClient(key)
                .UploadAsync(new BinaryData(new byte[] { (byte)index }), overwrite: true);
            prior.Add(key);
            await Task.Delay(15);
        }

        var oldest = prior[0];
        var lease = container.GetBlobClient(oldest).GetBlobLeaseClient();
        await lease.AcquireAsync(BlobLeaseClient.InfiniteLeaseDuration);
        try
        {
            await using var harness = CreateHarness(database, db);
            Assert.True(await harness.Coordinator.StartArchiveJobAsync(
                cave.AccountId, db.RequestUser.AccountContainerName));

            var terminal = await harness.Notifications.WaitForTerminalAsync();
            Assert.True(terminal.State == ProgressState.Completed,
                $"Expected Completed, got {terminal.State}: {terminal.StatusMessage}");

            var finals = await ListBlobsAsync(
                container, ArchiveJobCoordinator.ArchivePrefix, includeTemp: false);
            Assert.Equal(6, finals.Count);
            Assert.True((await container.GetBlobClient(oldest).ExistsAsync()).Value);

            var newlyPublished = finals.Single(blob =>
                !prior.Contains(blob.Name, StringComparer.Ordinal));
            var content = await container.GetBlobClient(newlyPublished.Name).DownloadContentAsync();
            AssertArchiveHasCoreCsvEntries(content.Value.Content.ToArray());
        }
        finally
        {
            await lease.ReleaseAsync();
        }
    }

    private async Task EnsureContainerAsync(string containerName)
    {
        await new BlobServiceClient(azurite.ConnectionString)
            .GetBlobContainerClient(containerName)
            .CreateIfNotExistsAsync();
    }

    private CoordinatorHarness CreateHarness(
        PostgresTestDatabase database,
        PlanarianDbContext db,
        bool blockAtCreatingArchive = false)
    {
        var testServices = IntegrationTestServices.For(
            db,
            maintenanceConnectionString: database.ConnectionString,
            fileConnectionString: azurite.ConnectionString);
        var repository = new AccountRepository(db, db.RequestUser);
        var notifications = new CapturingNotificationHubContext(blockAtCreatingArchive);

        var services = new ServiceCollection();
        services.AddSingleton(repository);
        services.AddSingleton<FileService>(testServices.Files);
        services.AddSingleton(new ExportService(repository, testServices.Files));
        services.AddSingleton(new NotificationService(notifications));
        var provider = services.BuildServiceProvider();

        var coordinator = new ArchiveJobCoordinator(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new AccountMaintenanceLockRepository(new ServerOptions
            {
                SqlConnectionString = database.ConnectionString
            }));

        return new CoordinatorHarness(coordinator, notifications, provider);
    }

    private static async Task<List<Azure.Storage.Blobs.Models.BlobItem>> ListBlobsAsync(
        BlobContainerClient container,
        string prefix,
        bool includeTemp)
    {
        var blobs = new List<Azure.Storage.Blobs.Models.BlobItem>();
        if (!(await container.ExistsAsync()).Value)
            return blobs;

        await foreach (var blob in container.GetBlobsAsync(prefix: prefix))
        {
            if (includeTemp ||
                !blob.Name.StartsWith(ArchiveJobCoordinator.TempArchivePrefix, StringComparison.OrdinalIgnoreCase))
                blobs.Add(blob);
        }

        return blobs;
    }

    private static void AssertArchiveHasCoreCsvEntries(byte[] content)
    {
        using var source = new MemoryStream(content, writable: false);
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        var names = new List<string>();
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
            names.Add(entry.Name);

        Assert.Contains("caves.csv", names);
        Assert.Contains("entrances.csv", names);
    }

    private sealed class CoordinatorHarness(
        ArchiveJobCoordinator coordinator,
        CapturingNotificationHubContext notifications,
        ServiceProvider provider) : IAsyncDisposable
    {
        public ArchiveJobCoordinator Coordinator { get; } = coordinator;
        public CapturingNotificationHubContext Notifications { get; } = notifications;

        public async ValueTask DisposeAsync()
        {
            Notifications.ReleaseCreatingArchive();
            await provider.DisposeAsync();
        }
    }

    private sealed class CapturingNotificationHubContext(bool blockAtCreatingArchive)
        : IHubContext<NotificationHub>
    {
        private readonly CaptureClientProxy _proxy = new(blockAtCreatingArchive);
        public IHubClients Clients => new CaptureHubClients(_proxy);
        public IGroupManager Groups { get; } = new NoOpGroupManager();

        public Task WaitUntilCreatingArchiveAsync() =>
            _proxy.CreatingArchive.Task.WaitAsync(TimeSpan.FromSeconds(20));

        public void ReleaseCreatingArchive() => _proxy.ReleaseCreating.TrySetResult();

        public Task<ArchiveProgressVm> WaitForTerminalAsync() =>
            _proxy.Terminal.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }

    private sealed class CaptureClientProxy(bool blockAtCreatingArchive) : IClientProxy
    {
        private readonly bool _blockAtCreatingArchive = blockAtCreatingArchive;
        public TaskCompletionSource CreatingArchive { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseCreating { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ArchiveProgressVm> Terminal { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default)
        {
            if (args.FirstOrDefault() is not ArchiveProgressVm progress)
                return;

            if (_blockAtCreatingArchive && progress.StatusMessage == "Creating archive...")
            {
                CreatingArchive.TrySetResult();
                await ReleaseCreating.Task.WaitAsync(cancellationToken);
            }

            if (progress.State is ProgressState.Completed or ProgressState.Canceled or ProgressState.Failed)
                Terminal.TrySetResult(progress);
        }
    }

    private sealed class CaptureHubClients(IClientProxy proxy) : IHubClients
    {
        public IClientProxy All => proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => proxy;
        public IClientProxy Client(string connectionId) => proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => proxy;
        public IClientProxy Group(string groupName) => proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => proxy;
        public IClientProxy User(string userId) => proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => proxy;
    }

    private sealed class NoOpGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
