using Microsoft.AspNetCore.Http;
using Planarian.Library.Exceptions;
using Planarian.Library.Options;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Account.Services;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;

namespace Planarian.Tests.Integration.AccountReset;

public sealed class AccountMaintenanceMiddlewareIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task HttpWritesRespectDatabaseLeasesAcrossTenants()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(HttpWritesRespectDatabaseLeasesAcrossTenants));
        var a = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var b = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var locks = new AccountMaintenanceLockRepository(new ServerOptions
        {
            SqlConnectionString = database.ConnectionString
        });
        var invoked = 0;
        var middleware = new AccountMaintenanceMiddleware(_ =>
        {
            invoked++;
            return Task.CompletedTask;
        });

        await using (var lease = await locks.TryAcquireAsync(a.AccountId, true))
        {
            Assert.NotNull(lease);
            await using var dbA = database.CreateDbContext("a", a.AccountId);
            var request = new DefaultHttpContext();
            request.Request.Method = "POST";
            request.Request.Path = "/api/caves";
            var error = await Assert.ThrowsAsync<ApiException>(() =>
                middleware.InvokeAsync(request, dbA.RequestUser, locks,
                    new AccountRepository(dbA, dbA.RequestUser)));
            Assert.Equal(409, error.StatusCode);

            await using var dbB = database.CreateDbContext("b", b.AccountId);
            await middleware.InvokeAsync(request, dbB.RequestUser, locks,
                new AccountRepository(dbB, dbB.RequestUser));
            Assert.Equal(1, invoked);
        }

        await using var available = database.CreateDbContext("a", a.AccountId);
        var allowed = new DefaultHttpContext();
        allowed.Request.Method = "PATCH";
        allowed.Request.Path = "/api/caves";
        await middleware.InvokeAsync(allowed, available.RequestUser, locks,
            new AccountRepository(available, available.RequestUser));
        Assert.Equal(2, invoked);
    }

    [Fact]
    public async Task PendingCleanupPermitsReadsButRejectsMutatingRequests()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(PendingCleanupPermitsReadsButRejectsMutatingRequests));
        var account = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await using var context = database.CreateDbContext("a", account.AccountId);
        context.StorageCleanupTargets.Add(new Planarian.Model.Database.Entities.RidgeWalker.StorageCleanupTarget
        {
            AccountId = account.AccountId,
            StoragePartition = "portable-account-objects",
            StorageKey = "objects/pending-reset",
            Purpose = Planarian.Model.Database.Entities.RidgeWalker.StorageCleanupPurpose.AccountReset
        });
        await context.SaveChangesAsync();

        var maintenance = new AccountMaintenanceLockRepository(new ServerOptions
        {
            SqlConnectionString = database.ConnectionString
        });
        var executed = false;
        var middleware = new AccountMaintenanceMiddleware(_ =>
        {
            executed = true;
            return Task.CompletedTask;
        });
        var http = new DefaultHttpContext();
        http.Request.Path = "/api/caves";
        http.Request.Method = "POST";
        var repo = new AccountRepository(context, context.RequestUser);
        var blocked = await Assert.ThrowsAsync<ApiException>(() =>
            middleware.InvokeAsync(http, context.RequestUser, maintenance, repo));
        Assert.Equal(409, blocked.StatusCode);
        Assert.False(executed);

        http.Request.Method = "GET";
        await middleware.InvokeAsync(http, context.RequestUser, maintenance, repo);
        Assert.True(executed);
    }

    [Fact]
    public async Task PendingCaveAssetCleanupDoesNotBlockUnrelatedAccountWrites()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PendingCaveAssetCleanupDoesNotBlockUnrelatedAccountWrites));
        var account = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await using var context = database.CreateDbContext("a", account.AccountId);
        context.StorageCleanupTargets.Add(new Planarian.Model.Database.Entities.RidgeWalker.StorageCleanupTarget
        {
            AccountId = account.AccountId,
            StoragePartition = "portable-account-objects",
            StorageKey = "objects/pending-asset-delete",
            Purpose = Planarian.Model.Database.Entities.RidgeWalker.StorageCleanupPurpose.CaveAssetRemoval
        });
        await context.SaveChangesAsync();

        var maintenance = new AccountMaintenanceLockRepository(new ServerOptions
        {
            SqlConnectionString = database.ConnectionString
        });
        var executed = false;
        var middleware = new AccountMaintenanceMiddleware(_ =>
        {
            executed = true;
            return Task.CompletedTask;
        });
        var http = new DefaultHttpContext();
        http.Request.Path = "/api/caves";
        http.Request.Method = "POST";

        await middleware.InvokeAsync(http, context.RequestUser, maintenance,
            new AccountRepository(context, context.RequestUser));

        Assert.True(executed);
    }


}
