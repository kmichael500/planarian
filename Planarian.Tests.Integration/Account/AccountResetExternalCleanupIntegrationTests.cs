using Microsoft.EntityFrameworkCore;
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

        await IntegrationTestServices.For(db, blobs).Account.ResetAccount(default);

        Assert.True(observedCommittedPurge);
        Assert.True(observedProviderNeutralCleanupBeforeLegacyContainerDelete);
        Assert.False(blobs.Contains(portablePartition, liveKey));
        Assert.False(blobs.Contains(portablePartition, retainedKey));
    }
}
