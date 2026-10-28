using Azure.Storage.Blobs;
using Planarian.Modules.Files.Services;
using Planarian.Shared.Services;
using Xunit;

namespace Planarian.Tests.Unit.Files;

public sealed class BlobContainerInitializationKeyTests
{
    [Fact]
    public void InitializationKeysDistinguishStorageEndpointsButIgnoreQueryCredentials()
    {
        var first = new BlobContainerClient(
            new Uri("http://127.0.0.1:11000/devstoreaccount1/account-a?sig=first"));
        var sameEndpointDifferentQuery = new BlobContainerClient(
            new Uri("http://127.0.0.1:11000/devstoreaccount1/account-a?sig=second"));
        var differentEndpoint = new BlobContainerClient(
            new Uri("http://127.0.0.1:12000/devstoreaccount1/account-a?sig=first"));

        var fileServiceKey = FileService.GetContainerInitializationKey(first);
        var objectStorageKey = AzureBlobObjectStorage.GetContainerInitializationKey(first);

        Assert.Equal(fileServiceKey, objectStorageKey);
        Assert.Equal(
            fileServiceKey,
            FileService.GetContainerInitializationKey(sameEndpointDifferentQuery));
        Assert.Equal(
            objectStorageKey,
            AzureBlobObjectStorage.GetContainerInitializationKey(sameEndpointDifferentQuery));
        Assert.NotEqual(
            fileServiceKey,
            FileService.GetContainerInitializationKey(differentEndpoint));
        Assert.NotEqual(
            objectStorageKey,
            AzureBlobObjectStorage.GetContainerInitializationKey(differentEndpoint));
        Assert.DoesNotContain("sig=", fileServiceKey, StringComparison.OrdinalIgnoreCase);
    }
}
