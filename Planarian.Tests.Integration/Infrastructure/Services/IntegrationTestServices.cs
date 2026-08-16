using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Planarian.Library.Options;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Account.Archive.Services;
using Planarian.Modules.Account.Services;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Caves.Services;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Notifications.Services;
using Planarian.Modules.Settings.Repositories;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Options;
using Planarian.Shared.Repositories;
using Planarian.Shared.Services;
using FileOptions = Planarian.Shared.Options.FileOptions;

namespace Planarian.Tests.Integration.Infrastructure.Services;

/// <summary>Correctly wired production services used by database integration tests.</summary>
internal sealed class IntegrationTestServices
{
    private readonly PlanarianDbContext _db;
    private readonly TagRepository _tags;
    private readonly CavePublishedSnapshotRepository _snapshots;
    private readonly TagReferenceLockRepository _tagLocks;
    private AccountService? _account;

    private IntegrationTestServices(PlanarianDbContext db, TestFileBlobStore blobs)
    {
        _db = db;
        var user = db.RequestUser;
        var caves = new CaveRepository(db, user);
        var tags = new TagRepository(db, user);
        var snapshots = new CavePublishedSnapshotRepository(db, user);
        var mutations = new CaveMutationCoordinator(new CaveMutationRepository(db, user, snapshots));
        var tagLocks = new TagReferenceLockRepository(db, user);
        var countyLocks = new CountyReferenceLockRepository(db, user);
        var files = new IntegrationTestFileService(new FileRepository(db, user), user, tags,
            new FileOptions { ConnectionString = "UseDevelopmentStorage=true" },
            new SettingsRepository(db, user), caves, CreateThrottle(db), mutations, tagLocks, blobs);

        Files = files;
        Caves = new CaveService(caves, user, files, tags, new FeatureSettingRepository(db, user),
            new ClientUrlBuilder(new FixedClientRequestOrigin()), mutations, tagLocks, countyLocks);
        CaveChangeRequests = new CaveChangeRequestService(new CaveChangeRequestRepository(db, user), caves, Caves,
            new CaveRevisionQueryRepository(db, user), mutations, files, user);
        _tags = tags;
        _snapshots = snapshots;
        _tagLocks = tagLocks;
    }

    public AccountService Account => _account ??= CreateAccountService();
    public CaveChangeRequestService CaveChangeRequests { get; }
    public CaveService Caves { get; }
    public FileService Files { get; }

    public static IntegrationTestServices For(PlanarianDbContext db, TestFileBlobStore? blobs = null) =>
        new(db, blobs ?? new TestFileBlobStore());

    public async Task<FileVm> StageRequestFileForTestAsync(string requestId, Stream stream, string fileName,
        string? uuid, CancellationToken cancellationToken)
    {
        var file = await Files.StageAuthoringFile(stream, fileName, cancellationToken, uuid);
        try
        {
            await new CaveChangeRequestRepository(_db, _db.RequestUser).StageFileAsync(
                requestId, file.Id, file.FileTypeTagId, file.DisplayName, reviewer: false, cancellationToken);
            return file;
        }
        catch
        {
            await Files.DeleteUnpublishedFileAsync(file.Id, CancellationToken.None);
            throw;
        }
    }

    private AccountService CreateAccountService()
    {
        var user = _db.RequestUser;
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        return new AccountService(new AccountRepository(_db, user), user, Files, new FileRepository(_db, user),
            new NotificationService(new NoOpNotificationHubContext()), _tags,
            new FeatureSettingRepository(_db, user), Caves,
            new ArchiveJobCoordinator(serviceProvider.GetRequiredService<IServiceScopeFactory>()), CreateThrottle(_db),
            new TagTypeMergeExecutionRepository(_db, user, _tagLocks, _snapshots,
                new CaveBulkRevisionRepository(_db, user)),
            new TagTypeDeleteExecutionRepository(_db, user),
            new CountyReferenceLockRepository(_db, user),
            new CountyDeleteExecutionRepository(_db, user, new CountyReferenceLockRepository(_db, user)));
    }

    private static RequestThrottleService CreateThrottle(PlanarianDbContext db)
    {
        var memory = new MemoryCache(new MemoryCacheOptions());
        var accessor = new HttpContextAccessor();
        var log = new ThrottleEventLogService(
            new ThrottleEventLogRepository(new IntegrationDbContextFactory(db)), accessor,
            NullLogger<ThrottleEventLogService>.Instance, memory, db.RequestUser, new ServerOptions());
        return new RequestThrottleService(memory, new RequestThrottleOptions(), accessor, db.RequestUser, log);
    }

    private sealed class FixedClientRequestOrigin : IClientRequestOrigin
    {
        public string GetOrigin() => "https://planarian.test";
    }

    private sealed class NoOpNotificationHubContext : IHubContext<Planarian.Modules.Notifications.Hubs.NotificationHub>
    {
        public IHubClients Clients { get; } = new NoOpHubClients();
        public IGroupManager Groups { get; } = new NoOpGroupManager();
    }

    private sealed class NoOpHubClients : IHubClients
    {
        private static readonly IClientProxy Proxy = new NoOpClientProxy();
        public IClientProxy All => Proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Client(string connectionId) => Proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;
        public IClientProxy Group(string groupName) => Proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
        public IClientProxy User(string userId) => Proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class NoOpClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveFromGroupAsync(string connectionId, string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class IntegrationDbContextFactory(PlanarianDbContext source)
        : IDbContextFactory<PlanarianDbContext>
    {
        public PlanarianDbContext CreateDbContext() => PostgresTestServer.CreateDbContext(
            source.Database.GetConnectionString()
            ?? throw new InvalidOperationException("The integration database has no connection string."),
            source.RequestUser.Id, source.RequestUser.AccountId);
    }
}

internal sealed class TestFileBlobStore : IObjectStorage
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Container, string Key), byte[]> _content = new();
    private readonly HashSet<(string Container, string Key)> _deleteFailures = [];

    public bool FailWrites { get; set; }
    public bool FailDeletes { get; set; }
    public bool FailContainerDeletes { get; set; }
    public Func<string, Task>? BeforeContainerDeleteAsync { get; set; }
    public IReadOnlyCollection<(string Container, string Key)> Keys
    {
        get
        {
            lock (_gate) return _content.Keys.ToList();
        }
    }

    public bool Contains(string container, string key)
    {
        lock (_gate) return _content.ContainsKey((container, key));
    }

    public byte[] Read(string container, string key)
    {
        lock (_gate) return _content[(container, key)].ToArray();
    }

    public void Seed(string container, string key, byte[] content)
    {
        lock (_gate) _content[(container, key)] = content.ToArray();
    }

    public void FailDelete(string container, string key)
    {
        lock (_gate) _deleteFailures.Add((container, key));
    }

    public async Task WriteAsync(Stream stream, string container, string key, CancellationToken cancellationToken)
    {
        if (FailWrites) throw new IOException("The test blob boundary rejected the write.");
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellationToken);
        lock (_gate) _content[(container, key)] = copy.ToArray();
    }

    public Stream OpenRead(string container, string key)
    {
        byte[] content;
        lock (_gate)
        {
            if (!_content.TryGetValue((container, key), out var stored))
                throw new FileNotFoundException("The staged test blob does not exist.", key);
            content = stored.ToArray();
        }
        return new MemoryStream(content, writable: false);
    }

    public void Delete(string container, string key)
    {
        lock (_gate)
        {
            if (FailDeletes || _deleteFailures.Contains((container, key)))
                throw new IOException("The test blob boundary rejected the delete.");
            _content.Remove((container, key));
        }
    }

    public void DeleteContainer(string container)
    {
        lock (_gate)
        {
            foreach (var key in _content.Keys.Where(key => key.Container == container).ToList())
                _content.Remove(key);
        }
    }

    public async Task PutAsync(StorageObjectAddress address, Stream content, string? contentType,
        CancellationToken cancellationToken) =>
        await WriteAsync(content, address.Partition, address.Key, cancellationToken);

    public Task<StoredObjectReadResult?> OpenReadAsync(StorageObjectAddress address,
        CancellationToken cancellationToken)
    {
        if (!Contains(address.Partition, address.Key))
            return Task.FromResult<StoredObjectReadResult?>(null);
        return Task.FromResult<StoredObjectReadResult?>(new StoredObjectReadResult(
            _ => Task.FromResult(OpenRead(address.Partition, address.Key)),
            "application/octet-stream", null, null));
    }

    public Task<bool> DeleteIfExistsAsync(StorageObjectAddress address, CancellationToken cancellationToken)
    {
        var existed = Contains(address.Partition, address.Key);
        Delete(address.Partition, address.Key);
        return Task.FromResult(existed);
    }
}
