using Azure.Storage.Blobs;
using Planarian.Library.Exceptions;
using Planarian.Modules.Account.Archive.Services;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

namespace Planarian.Tests.Integration.AccountArchive;

public sealed class ArchiveTenantIsolationIntegrationTests(
    PostgresTestServer postgres,
    AzuriteTestServer azurite)
    : IClassFixture<PostgresTestServer>, IClassFixture<AzuriteTestServer>
{
    [Fact]
    public async Task ListingArchivesDoesNotCreateAMissingAccountContainer()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(ListingArchivesDoesNotCreateAMissingAccountContainer));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'l');

        await using var db = database.CreateDbContext("archive-list-readonly", account.AccountId);
        var services = IntegrationTestServices.For(db, fileConnectionString: azurite.ConnectionString);
        var container = new BlobServiceClient(azurite.ConnectionString)
            .GetBlobContainerClient(db.RequestUser.AccountContainerName);

        Assert.False((await container.ExistsAsync()).Value);

        var archives = (await services.Account.GetRecentArchives(CancellationToken.None)).ToArray();

        Assert.Empty(archives);
        Assert.False((await container.ExistsAsync()).Value);
    }

    [Fact]
    public async Task AccountArchiveOperationsRemainBoundToTheRequestUsersContainer()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(AccountArchiveOperationsRemainBoundToTheRequestUsersContainer));
        var tenantA = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var tenantB = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'b');

        await using var dbA = database.CreateDbContext("archive-user-a", tenantA.AccountId);
        await using var dbB = database.CreateDbContext("archive-user-b", tenantB.AccountId);
        var servicesA = IntegrationTestServices.For(dbA, fileConnectionString: azurite.ConnectionString);
        var servicesB = IntegrationTestServices.For(dbB, fileConnectionString: azurite.ConnectionString);

        var blobService = new BlobServiceClient(azurite.ConnectionString);
        var containerA = blobService.GetBlobContainerClient(dbA.RequestUser.AccountContainerName);
        var containerB = blobService.GetBlobContainerClient(dbB.RequestUser.AccountContainerName);
        await containerA.CreateIfNotExistsAsync();
        await containerB.CreateIfNotExistsAsync();

        const string sharedKey = "archives/shared/archive.tar.gz";
        const string bOnlyKey = "archives/b-only/archive.tar.gz";
        await PublishAsync(containerA, "archives/temp/a/shared.tar.gz", sharedKey, [1, 2, 3]);
        await PublishAsync(containerB, "archives/temp/b/shared.tar.gz", sharedKey, [7, 8, 9]);
        await PublishAsync(containerB, "archives/temp/b/only.tar.gz", bOnlyKey, [4, 5, 6]);

        var listA = (await servicesA.Account.GetRecentArchives(CancellationToken.None)).ToArray();
        var listB = (await servicesB.Account.GetRecentArchives(CancellationToken.None)).ToArray();

        Assert.Single(listA);
        Assert.Equal(sharedKey, listA[0].BlobKey);
        Assert.Equal(2, listB.Length);
        Assert.Contains(listB, item => item.BlobKey == sharedKey);
        Assert.Contains(listB, item => item.BlobKey == bOnlyKey);

        // A cannot turn a B storage key into a cross-container read.
        await Assert.ThrowsAsync<ApiException>(() =>
            servicesA.Account.CreateArchiveDownloadResponse(bOnlyKey, CancellationToken.None));

        // A deleting the same logical key only touches A's account container.
        var sharedProperties = await containerA.GetBlobClient(sharedKey).GetPropertiesAsync();
        Assert.True(
            ArchiveJobCoordinator.IsArchiveReady(
                sharedProperties.Value.CopyStatus, sharedProperties.Value.CopyId),
            $"Expected current publisher output to be ready, CopyStatus={sharedProperties.Value.CopyStatus}, " +
            $"CopyId={sharedProperties.Value.CopyId}, CopySource={sharedProperties.Value.CopySource}, " +
            $"CopyStatusDescription={sharedProperties.Value.CopyStatusDescription}");
        await servicesA.Account.DeleteArchive(sharedKey, CancellationToken.None);
        Assert.False((await containerA.GetBlobClient(sharedKey).ExistsAsync()).Value);
        Assert.True((await containerB.GetBlobClient(sharedKey).ExistsAsync()).Value);
        Assert.Equal(new byte[] { 7, 8, 9 }, await DownloadAsync(containerB, sharedKey));

        // A cannot delete a B-only key: readiness is checked in A's own container
        // before any delete is attempted, so the operation fails closed.
        await Assert.ThrowsAsync<ApiException>(() =>
            servicesA.Account.DeleteArchive(bOnlyKey, CancellationToken.None));
        Assert.True((await containerB.GetBlobClient(bOnlyKey).ExistsAsync()).Value);
        Assert.Equal(new byte[] { 4, 5, 6 }, await DownloadAsync(containerB, bOnlyKey));

        await Assert.ThrowsAsync<ApiException>(() =>
            servicesA.Account.CreateArchiveDownloadResponse(
                "archives/temp/b/only.tar.gz", CancellationToken.None));
    }

    private static async Task PublishAsync(
        BlobContainerClient container,
        string tempKey,
        string finalKey,
        byte[] payload)
    {
        await container.GetBlobClient(tempKey).UploadAsync(new BinaryData(payload), overwrite: true);
        await ArchiveJobCoordinator.PublishArchive(container, tempKey, finalKey, CancellationToken.None);
    }

    private static async Task<byte[]> DownloadAsync(BlobContainerClient container, string key)
    {
        var response = await container.GetBlobClient(key).DownloadContentAsync();
        return response.Value.Content.ToArray();
    }
}
