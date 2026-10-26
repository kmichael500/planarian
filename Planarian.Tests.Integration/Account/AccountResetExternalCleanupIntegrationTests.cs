using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Planarian.Library.Exceptions;
using Planarian.Modules.Account.Archive.Services;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Caves.Revisions;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.AccountReset;

public sealed class AccountResetExternalCleanupIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ResetAccountCommitsRelationalPurgeBeforeDeletingExternalContainer()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ResetAccountCommitsRelationalPurgeBeforeDeletingExternalContainer));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "a");
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "resetfilea");
        const string portablePartition = "portable-store";
        const string liveKey = "objects/files/resetfilea";
        const string retainedKey = "objects/files/oldreset01";
        await using var db = database.CreateDbContext("a", tenant.AccountId);
        var live = await db.Files.SingleAsync(row => row.Id == file.FileId);
        live.BlobContainer = portablePartition;
        live.BlobKey = liveKey;
        db.RetainedCaveFileObjects.Add(new RetainedCaveFileObject
        {
            AccountId = tenant.AccountId,
            CaveId = tenant.CaveId,
            FileId = "oldreset01",
            StoragePartition = portablePartition,
            StorageKey = retainedKey
        });
        await db.SaveChangesAsync();

        var requests = new CaveChangeRequestRepository(db, db.RequestUser);
        var requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
            Proposal(tenant, "Reviewed proposal"), default);
        var versionId = await CurrentVersionAsync(db, requestId);
        await requests.RequestChangesAsync(requestId, versionId, "Please revise before reset.", default);
        Assert.Single(await db.CaveChangeRequestReviewEvents.ToListAsync());

        var blobs = new TestFileBlobStore();
        blobs.Seed(portablePartition, liveKey, [1]);
        blobs.Seed(portablePartition, retainedKey, [2]);
        var observedCommittedPurge = false;
        var observedProviderNeutralCleanupBeforeLegacyContainerDelete = false;
        blobs.BeforeContainerDeleteAsync = async _ =>
        {
            await using var observer = database.CreateDbContext("reset-observer", tenant.AccountId);
            observedCommittedPurge = !await observer.Caves.IgnoreQueryFilters()
                .AnyAsync(cave => cave.AccountId == tenant.AccountId && cave.Id == tenant.CaveId);
            observedProviderNeutralCleanupBeforeLegacyContainerDelete =
                !blobs.Contains(portablePartition, liveKey) &&
                !blobs.Contains(portablePartition, retainedKey);
        };

        await IntegrationTestServices.For(db, blobs, maintenanceConnectionString: database.ConnectionString).Account.ResetAccount(default);

        Assert.True(observedCommittedPurge);
        Assert.True(observedProviderNeutralCleanupBeforeLegacyContainerDelete);
        Assert.False(blobs.Contains(portablePartition, liveKey));
        Assert.False(blobs.Contains(portablePartition, retainedKey));
    }

    [Fact]
    public async Task ResetAccountDeletesFavoritesAndLinePlotsWithoutTouchingAnotherAccount()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ResetAccountDeletesFavoritesAndLinePlotsWithoutTouchingAnotherAccount));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        await CavePermissions.GrantViewAsync(database, accountA, "a");
        await CavePermissions.GrantViewAsync(database, accountB, "b");

        foreach (var (cave, actor) in new[] { (accountA, "a"), (accountB, "b") })
        {
            await using var setup = database.CreateDbContext(actor, cave.AccountId);
            setup.Favorites.Add(new Favorite
            {
                Id = $"favorite{actor}",
                AccountId = cave.AccountId,
                CaveId = cave.CaveId,
                UserId = setup.RequestUser.Id
            });
            setup.CaveGeoJsons.Add(new CaveGeoJson
            {
                Id = $"plot{actor}",
                CaveId = cave.CaveId,
                Name = "Line plot",
                GeoJson = "{\"type\":\"FeatureCollection\",\"features\":[]}"
            });
            await setup.SaveChangesAsync();
        }

        await using (var reset = database.CreateDbContext("a", accountA.AccountId))
            await IntegrationTestServices.For(reset, maintenanceConnectionString: database.ConnectionString).Account.ResetAccount(default);

        await using var verify = database.CreateDbContext("verify", accountA.AccountId);
        Assert.False(await verify.Caves.IgnoreQueryFilters()
            .AnyAsync(c => c.AccountId == accountA.AccountId));
        Assert.False(await verify.Favorites.IgnoreQueryFilters()
            .AnyAsync(f => f.AccountId == accountA.AccountId));
        Assert.False(await verify.CaveGeoJsons.IgnoreQueryFilters()
            .AnyAsync(p => p.CaveId == accountA.CaveId));
        Assert.True(await verify.Caves.IgnoreQueryFilters()
            .AnyAsync(c => c.Id == accountB.CaveId && c.AccountId == accountB.AccountId));
        Assert.True(await verify.Favorites.IgnoreQueryFilters()
            .AnyAsync(f => f.AccountId == accountB.AccountId && f.CaveId == accountB.CaveId));
        Assert.True(await verify.CaveGeoJsons.IgnoreQueryFilters()
            .AnyAsync(p => p.CaveId == accountB.CaveId));
    }



    [Fact]
    public async Task PostCommitContainerFailureReportsPartialSuccessAndRetryPreservesTheOtherTenant()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PostCommitContainerFailureReportsPartialSuccessAndRetryPreservesTheOtherTenant));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var blobs = new TestFileBlobStore { FailContainerDeletes = true };
        var containerA = "account-" + accountA.AccountId;
        var containerB = "account-" + accountB.AccountId;
        blobs.Seed(containerA, "archives/backup-a", [1, 2, 3]);
        blobs.Seed(containerB, "archives/backup-b", [4, 5, 6]);

        await using (var reset = database.CreateDbContext("a", accountA.AccountId))
        {
            var result = await IntegrationTestServices.For(reset, blobs, maintenanceConnectionString: database.ConnectionString).Account.ResetAccount(default);
            Assert.True(result.RelationalResetCompleted);
            Assert.False(result.StorageCleanupCompleted);
        }

        await using (var verify = database.CreateDbContext("verify", accountA.AccountId))
        {
            Assert.False(await verify.Caves.IgnoreQueryFilters()
                .AnyAsync(cave => cave.AccountId == accountA.AccountId));
            Assert.True(await verify.Caves.IgnoreQueryFilters()
                .AnyAsync(cave => cave.AccountId == accountB.AccountId && cave.Id == accountB.CaveId));
        }
        Assert.True(blobs.Contains(containerA, "archives/backup-a"));
        Assert.True(blobs.Contains(containerB, "archives/backup-b"));

        blobs.FailContainerDeletes = false;
        await using (var retry = database.CreateDbContext("a", accountA.AccountId))
        {
            var result = await IntegrationTestServices.For(retry, blobs, maintenanceConnectionString: database.ConnectionString).Account.ResetAccount(default);
            Assert.True(result.RelationalResetCompleted);
            Assert.True(result.StorageCleanupCompleted);
        }

        Assert.False(blobs.Contains(containerA, "archives/backup-a"));
        Assert.Equal(new byte[] { 4, 5, 6 }, blobs.Read(containerB, "archives/backup-b"));
        await using var verifyRetry = database.CreateDbContext("verify", accountB.AccountId);
        Assert.True(await verifyRetry.Caves.IgnoreQueryFilters()
            .AnyAsync(cave => cave.AccountId == accountB.AccountId && cave.Id == accountB.CaveId));
    }

    [Fact]
    public async Task ProviderNeutralObjectFailureIsReportedAfterRelationalCommit()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProviderNeutralObjectFailureIsReportedAfterRelationalCommit));
        var account = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var file = await FileTestDataFactory.AddFileAsync(database, account,
            associateWithCave: true, fileId: "object000a");
        const string partition = "portable-account-objects";
        const string key = "portable/object-a";
        await using (var setup = database.CreateDbContext("a", account.AccountId))
        {
            var entity = await setup.Files.SingleAsync(row => row.Id == file.FileId);
            entity.BlobContainer = partition;
            entity.BlobKey = key;
            await setup.SaveChangesAsync();
        }

        var blobs = new TestFileBlobStore();
        blobs.Seed(partition, key, [9]);
        blobs.FailDelete(partition, key);
        await using (var reset = database.CreateDbContext("a", account.AccountId))
        {
            var result = await IntegrationTestServices.For(reset, blobs, maintenanceConnectionString: database.ConnectionString).Account.ResetAccount(default);
            Assert.True(result.RelationalResetCompleted);
            Assert.False(result.StorageCleanupCompleted);
        }
        Assert.True(blobs.Contains(partition, key));
        await using var verify = database.CreateDbContext("observer", account.AccountId);
        Assert.False(await verify.Caves.IgnoreQueryFilters()
            .AnyAsync(c => c.AccountId == account.AccountId));
        Assert.False(await verify.Files.IgnoreQueryFilters()
            .AnyAsync(f => f.AccountId == account.AccountId));
    }


    [Fact]
    public async Task ConcurrentResetAndNewArchiveAreBlockedUntilExistingResetCompletes()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ConcurrentResetAndNewArchiveAreBlockedUntilExistingResetCompletes));
        var account = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var anotherAccount = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var scopeFactory = new ServiceCollection().BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        var coordinator = new ArchiveJobCoordinator(scopeFactory,
            new Planarian.Modules.Account.Repositories.AccountMaintenanceLockRepository(
                new Planarian.Library.Options.ServerOptions
                { SqlConnectionString = database.ConnectionString }));
        var blobs = new TestFileBlobStore();
        var enteredStorageCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStorageCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        blobs.BeforeContainerDeleteAsync = async _ =>
        {
            enteredStorageCleanup.TrySetResult();
            await releaseStorageCleanup.Task;
        };

        await using var firstDb = database.CreateDbContext("a", account.AccountId);
        var firstReset = IntegrationTestServices.For(firstDb, blobs, coordinator, database.ConnectionString).Account.ResetAccount(default);
        await enteredStorageCleanup.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            await using var secondDb = database.CreateDbContext("a", account.AccountId);
            var error = await Assert.ThrowsAsync<ApiException>(() =>
                IntegrationTestServices.For(secondDb, blobs, coordinator, database.ConnectionString).Account.ResetAccount(default));
            Assert.Equal(409, error.StatusCode);
            Assert.False(await coordinator.StartArchiveJobAsync(account.AccountId,
                "account-" + account.AccountId));
        }
        finally
        {
            releaseStorageCleanup.TrySetResult();
        }

        var completed = await firstReset;
        Assert.True(completed.StorageCleanupCompleted);
        await using var verify = database.CreateDbContext("verify", anotherAccount.AccountId);
        Assert.True(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave =>
            cave.AccountId == anotherAccount.AccountId && cave.Id == anotherAccount.CaveId));
    }


    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectsCrossTenantStorageAddressesBeforeDeletingRelationalData(bool foreignContainer)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RejectsCrossTenantStorageAddressesBeforeDeletingRelationalData));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var fileA = await FileTestDataFactory.AddFileAsync(database, accountA,
            associateWithCave: true, fileId: "shared000a");
        var fileB = await FileTestDataFactory.AddFileAsync(database, accountB,
            associateWithCave: true, fileId: "shared000b");
        var partition = foreignContainer ? "account-" + accountB.AccountId : "portable-store";
        const string objectKey = "objects/shared";
        await using (var seedA = database.CreateDbContext("a", accountA.AccountId))
        {
            var entity = await seedA.Files.SingleAsync(file => file.Id == fileA.FileId);
            entity.BlobContainer = partition;
            entity.BlobKey = objectKey;
            await seedA.SaveChangesAsync();
        }

        if (!foreignContainer)
        {
            await using var seedB = database.CreateDbContext("b", accountB.AccountId);
            var entity = await seedB.Files.SingleAsync(file => file.Id == fileB.FileId);
            entity.BlobContainer = partition;
            entity.BlobKey = objectKey;
            await seedB.SaveChangesAsync();
        }

        var blobs = new TestFileBlobStore();
        blobs.Seed(partition, objectKey, [13, 21]);
        await using (var reset = database.CreateDbContext("a", accountA.AccountId))
        {
            var rejected = await Assert.ThrowsAsync<ApiException>(() =>
                IntegrationTestServices.For(reset, blobs, maintenanceConnectionString: database.ConnectionString).Account.ResetAccount(default));
            Assert.Equal(409, rejected.StatusCode);
        }

        Assert.Equal(new byte[] { 13, 21 }, blobs.Read(partition, objectKey));
        await using var verify = database.CreateDbContext("observer", accountA.AccountId);
        Assert.True(await verify.Caves.IgnoreQueryFilters().AnyAsync(c => c.Id == accountA.CaveId));
        Assert.True(await verify.Caves.IgnoreQueryFilters().AnyAsync(c => c.Id == accountB.CaveId));
        Assert.True(await verify.Files.IgnoreQueryFilters().AnyAsync(f => f.Id == fileA.FileId));
        Assert.True(await verify.Files.IgnoreQueryFilters().AnyAsync(f => f.Id == fileB.FileId));
    }


    [Fact]
    public async Task ResetDeletesLegacyFavoriteWithNullAccountIdWhenCaveIsOwned()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ResetDeletesLegacyFavoriteWithNullAccountIdWhenCaveIsOwned));
        var account = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, account, "a");
        await using (var seed = database.CreateDbContext("a", account.AccountId))
        {
            seed.Favorites.Add(new Favorite
            {
                Id = "legacyfavorite", UserId = seed.RequestUser.Id,
                AccountId = account.AccountId, CaveId = account.CaveId
            });
            await seed.SaveChangesAsync();
            await seed.Favorites.IgnoreQueryFilters()
                .Where(f => f.Id == "legacyfavorite")
                .ExecuteUpdateAsync(setters => setters.SetProperty(f => f.AccountId, (string?)null));
        }

        await using (var reset = database.CreateDbContext("a", account.AccountId))
        {
            var result = await IntegrationTestServices.For(reset, maintenanceConnectionString: database.ConnectionString).Account.ResetAccount(default);
            Assert.True(result.StorageCleanupCompleted);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.False(await verify.Favorites.IgnoreQueryFilters().AnyAsync(f => f.Id == "legacyfavorite"));
        Assert.False(await verify.Caves.IgnoreQueryFilters().AnyAsync(c => c.Id == account.CaveId));
    }

    [Fact]
    public async Task FailedObjectDeletionRemainsDurablyRetryableWithoutRepeatingTheRelationalReset()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(FailedObjectDeletionRemainsDurablyRetryableWithoutRepeatingTheRelationalReset));
        var account = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var file = await FileTestDataFactory.AddFileAsync(database, account, fileId: "retryfilea");
        const string partition = "portable-account-objects";
        const string key = "portable/reset-retry-a";
        await using (var setup = database.CreateDbContext("a", account.AccountId))
        {
            var live = await setup.Files.SingleAsync(row => row.Id == file.FileId);
            live.BlobContainer = partition;
            live.BlobKey = key;
            await setup.SaveChangesAsync();
        }

        var blobs = new TestFileBlobStore { FailDeletes = true };
        blobs.Seed(partition, key, [17]);
        await using (var context = database.CreateDbContext("a", account.AccountId))
        {
            var first = await IntegrationTestServices.For(context, blobs, maintenanceConnectionString: database.ConnectionString)
                .Account.ResetAccount(default);
            Assert.True(first.RelationalResetCompleted);
            Assert.False(first.StorageCleanupCompleted);
        }

        await using (var observer = database.CreateDbContext("a", account.AccountId))
        {
            Assert.False(await observer.Caves.IgnoreQueryFilters().AnyAsync(row => row.Id == account.CaveId));
            var debt = await observer.AccountResetCleanupTargets
                .Where(row => row.AccountId == account.AccountId).ToListAsync();
            Assert.Single(debt);
            Assert.Equal(key, debt[0].StorageKey);
        }
        Assert.True(blobs.Contains(partition, key));

        blobs.FailDeletes = false;
        await using (var retry = database.CreateDbContext("a", account.AccountId))
        {
            var result = await IntegrationTestServices.For(retry, blobs, maintenanceConnectionString: database.ConnectionString)
                .Account.RetryResetCleanupAsync(default);
            Assert.True(result.StorageCleanupCompleted);
        }
        await using (var verify = database.CreateDbContext("a", account.AccountId))
        {
            Assert.False(await verify.AccountResetCleanupTargets.AnyAsync(row => row.AccountId == account.AccountId));
            Assert.False(await verify.Caves.IgnoreQueryFilters().AnyAsync(row => row.Id == account.CaveId));
        }
        Assert.False(blobs.Contains(partition, key));
    }

    [Fact]
    public async Task SharedLeaseInAnotherDatabaseConnectionBlocksResetBeforeAnyDeletion()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(SharedLeaseInAnotherDatabaseConnectionBlocksResetBeforeAnyDeletion));
        var account = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locks = new Planarian.Modules.Account.Repositories.AccountMaintenanceLockRepository(
            new Planarian.Library.Options.ServerOptions { SqlConnectionString = database.ConnectionString });
        await using (var activeWriter = await locks.TryAcquireAsync(account.AccountId, exclusive: false))
        {
            Assert.NotNull(activeWriter);
            await using var blocked = database.CreateDbContext("a", account.AccountId);
            var rejected = await Assert.ThrowsAsync<ApiException>(() =>
                IntegrationTestServices.For(blocked, maintenanceConnectionString: database.ConnectionString)
                    .Account.ResetAccount(default));
            Assert.Equal(409, rejected.StatusCode);
            Assert.True(await blocked.Caves.IgnoreQueryFilters().AnyAsync(row => row.Id == account.CaveId));
        }

        await using var allowed = database.CreateDbContext("a", account.AccountId);
        var result = await IntegrationTestServices.For(allowed, maintenanceConnectionString: database.ConnectionString)
            .Account.ResetAccount(default);
        Assert.True(result.StorageCleanupCompleted);
    }

    [Fact]
    public async Task LargeResetStoresAndCompletesCleanupInBatches()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(LargeResetStoresAndCompletesCleanupInBatches));
        var account = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var initial = await FileTestDataFactory.AddFileAsync(database, account, fileId: "batchfilea");
        await using (var setup = database.CreateDbContext("a", account.AccountId))
        {
            var tag = await setup.Files.Where(row => row.Id == initial.FileId)
                .Select(row => row.FileTypeTagId).SingleAsync();
            setup.Files.AddRange(Enumerable.Range(0, 1101).Select(i => new Planarian.Model.Database.Entities.RidgeWalker.File
            {
                Id = i.ToString("D10"), AccountId = account.AccountId, CaveId = account.CaveId,
                FileTypeTagId = tag, Name = "Reset batch file", Extension = ".pdf",
                BlobContainer = "portable-account-objects", BlobKey = "objects/reset-batch-" + i
            }));
            await setup.SaveChangesAsync();
        }

        var counter = new Planarian.Tests.DbCommandCounter();
        await using (var reset = database.CreateDbContext("a", account.AccountId, counter))
        {
            var result = await IntegrationTestServices.For(reset, maintenanceConnectionString: database.ConnectionString)
                .Account.ResetAccount(default);
            Assert.True(result.StorageCleanupCompleted);
        }
        // Two grouped foreign-reference probes per 500 keys during preflight and
        // cleanup, not two probes per File. This structural bound is independent
        // of CI wall-clock and protects 10k-Cave workloads.
        var addressChecks = counter.Commands.Count(sql =>
            sql.Contains("BlobContainer", StringComparison.Ordinal) &&
            sql.Contains("ANY", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("FROM \"Files\"", StringComparison.Ordinal));
        Assert.InRange(addressChecks, 2, 12);

        await using var verify = database.CreateDbContext("a", account.AccountId);
        Assert.False(await verify.Files.IgnoreQueryFilters().AnyAsync(row => row.AccountId == account.AccountId));
        Assert.False(await verify.AccountResetCleanupTargets.AnyAsync(row => row.AccountId == account.AccountId));
    }


    [Fact]
    public async Task ForeignReferenceToUntrackedAccountContainerBlobPreventsPurge()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ForeignReferenceToUntrackedAccountContainerBlobPreventsPurge));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var fileB = await FileTestDataFactory.AddFileAsync(database, accountB, fileId: "foreignb01");
        var containerA = "account-" + accountA.AccountId;
        await using (var setup = database.CreateDbContext("b", accountB.AccountId))
        {
            var file = await setup.Files.SingleAsync(row => row.Id == fileB.FileId);
            // Azure container identities are case-insensitive, while PostgreSQL text
            // equality is not; a mixed-case legacy locator must still block reset.
            file.BlobContainer = containerA.ToUpperInvariant();
            file.BlobKey = "archives/foreign-owned";
            await setup.SaveChangesAsync();
        }
        var blobs = new TestFileBlobStore();
        blobs.Seed(containerA, "archives/foreign-owned", [42]);
        await using (var reset = database.CreateDbContext("a", accountA.AccountId))
        {
            var ex = await Assert.ThrowsAsync<ApiException>(() =>
                IntegrationTestServices.For(reset, blobs, maintenanceConnectionString: database.ConnectionString)
                    .Account.ResetAccount(default));
            Assert.Equal(409, ex.StatusCode);
        }
        Assert.Equal(new byte[] { 42 }, blobs.Read(containerA, "archives/foreign-owned"));
        await using var verify = database.CreateDbContext("observer", accountA.AccountId);
        Assert.True(await verify.Caves.IgnoreQueryFilters().AnyAsync(row => row.Id == accountA.CaveId));
        Assert.True(await verify.Files.IgnoreQueryFilters().AnyAsync(row => row.Id == fileB.FileId));
        Assert.False(await verify.AccountResetCleanupTargets.AnyAsync(row => row.AccountId == accountA.AccountId));
    }

}
