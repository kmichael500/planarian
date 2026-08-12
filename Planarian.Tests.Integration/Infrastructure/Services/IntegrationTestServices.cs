using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Planarian.Library.Options;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Caves.Services;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
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
    private IntegrationTestServices(PlanarianDbContext db, TestFileBlobStore blobs)
    {
        var user = db.RequestUser;
        var caves = new CaveRepository(db, user);
        var tags = new TagRepository(db, user);
        var snapshots = new CavePublishedSnapshotRepository(db, user);
        var mutations = new CaveMutationCoordinator(new CaveMutationRepository(db, user, snapshots));
        var files = new IntegrationTestFileService(new FileRepository(db, user), user, tags,
            new FileOptions { ConnectionString = "UseDevelopmentStorage=true" },
            new SettingsRepository(db, user), caves, CreateThrottle(db), mutations, blobs);

        Files = files;
        Caves = new CaveService(caves, user, files, tags, new FeatureSettingRepository(db, user),
            new ClientUrlBuilder(new FixedClientRequestOrigin()), mutations);
        CaveChangeRequests = new CaveChangeRequestService(new CaveChangeRequestRepository(db, user), caves, Caves,
            new CaveRevisionQueryRepository(db, user), mutations, files, user);
    }

    public CaveChangeRequestService CaveChangeRequests { get; }
    public CaveService Caves { get; }
    public FileService Files { get; }

    public static IntegrationTestServices For(PlanarianDbContext db, TestFileBlobStore? blobs = null) =>
        new(db, blobs ?? new TestFileBlobStore());

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

    private sealed class IntegrationDbContextFactory(PlanarianDbContext source)
        : IDbContextFactory<PlanarianDbContext>
    {
        public PlanarianDbContext CreateDbContext() => PostgresTestServer.CreateDbContext(
            source.Database.GetConnectionString()
            ?? throw new InvalidOperationException("The integration database has no connection string."),
            source.RequestUser.Id, source.RequestUser.AccountId);
    }
}

internal sealed class TestFileBlobStore
{
    private readonly Dictionary<(string Container, string Key), byte[]> _content = new();

    public bool FailWrites { get; set; }
    public IReadOnlyCollection<(string Container, string Key)> Keys => _content.Keys;

    public async Task WriteAsync(Stream stream, string container, string key, CancellationToken cancellationToken)
    {
        if (FailWrites) throw new IOException("The test blob boundary rejected the write.");
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellationToken);
        _content[(container, key)] = copy.ToArray();
    }

    public Stream OpenRead(string container, string key) =>
        _content.TryGetValue((container, key), out var content)
            ? new MemoryStream(content, writable: false)
            : throw new FileNotFoundException("The staged test blob does not exist.", key);

    public void Delete(string container, string key) => _content.Remove((container, key));
}
