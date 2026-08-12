using Planarian.Model.Database;
using Planarian.Tests.Integration.Infrastructure.Services;

namespace Planarian.Tests.Integration.Infrastructure.Actors;

internal sealed class CaveTestActor : IAsyncDisposable
{
    private CaveTestActor(PlanarianDbContext db, IntegrationTestServices services)
    {
        Db = db;
        Services = services;
    }

    public PlanarianDbContext Db { get; }
    public IntegrationTestServices Services { get; }
    public Planarian.Modules.Caves.Services.CaveChangeRequestService ChangeRequests => Services.CaveChangeRequests;

    public static async Task<CaveTestActor> CreateAsync(PostgresTestDatabase database, string accountId,
        string userId, TestFileBlobStore? blobs = null)
    {
        var db = database.CreateDbContext(userId, accountId);
        await CavePermissions.EnsureAccountUserAsync(db, accountId);
        await db.RequestUser.Initialize(accountId, db.RequestUser.Id);
        return new CaveTestActor(db, IntegrationTestServices.For(db, blobs));
    }

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
