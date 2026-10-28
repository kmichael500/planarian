using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Planarian.Shared.Services;
using Xunit;
using FileOptions = Planarian.Shared.Options.FileOptions;

namespace Planarian.Tests;

public sealed class AzureBlobObjectStorageContractIntegrationTests :
    IClassFixture<AzuriteTestServer>
{
    private readonly AzuriteTestServer _azurite;

    public AzureBlobObjectStorageContractIntegrationTests(AzuriteTestServer azurite) =>
        _azurite = azurite;

    [Fact]
    public async Task CreateThenOpenReadPreservesBytesAndMetadata()
    {
        var storage = CreateStorage();
        var address = Address();
        var bytes = Encoding.UTF8.GetBytes("Planarian storage contract");
        var before = DateTimeOffset.UtcNow.AddSeconds(-2);

        await storage.CreateAsync(
            address, new MemoryStream(bytes), "text/plain; charset=utf-8", default);

        var result = await storage.OpenReadAsync(address, default);
        Assert.NotNull(result);
        Assert.Equal("text/plain; charset=utf-8", result!.ContentType);
        Assert.False(string.IsNullOrWhiteSpace(result!.EntityTag));
        Assert.NotNull(result!.LastModified);
        Assert.InRange(result!.LastModified!.Value, before, DateTimeOffset.UtcNow.AddSeconds(2));
        Assert.Equal(bytes, await ReadAllAsync(await result!.OpenReadStreamAsync(default)));
    }

    [Fact]
    public async Task CreateIsImmutableAndNeverOverwritesExistingBytes()
    {
        var storage = CreateStorage();
        var address = Address();
        var original = Encoding.UTF8.GetBytes("original");
        var replacement = Encoding.UTF8.GetBytes("replacement");

        await storage.CreateAsync(address, new MemoryStream(original), "text/plain", default);

        await Assert.ThrowsAsync<RequestFailedException>(() =>
            storage.CreateAsync(address, new MemoryStream(replacement), "text/plain", default));

        var result = await storage.OpenReadAsync(address, default);
        Assert.NotNull(result);
        Assert.Equal(original, await ReadAllAsync(await result!.OpenReadStreamAsync(default)));
    }

    [Fact]
    public async Task MissingObjectHasNullReadAndIdempotentDelete()
    {
        var storage = CreateStorage();
        var address = Address();

        Assert.Null(await storage.OpenReadAsync(address, default));
        Assert.False(await storage.DeleteIfExistsAsync(address, default));
    }

    [Fact]
    public async Task DeleteExistingObjectReturnsTrueThenBecomesIdempotent()
    {
        var storage = CreateStorage();
        var address = Address();
        await storage.CreateAsync(address, new MemoryStream([1, 2, 3]), "application/octet-stream", default);

        Assert.True(await storage.DeleteIfExistsAsync(address, default));
        Assert.False(await storage.DeleteIfExistsAsync(address, default));
        Assert.Null(await storage.OpenReadAsync(address, default));
    }

    [Fact]
    public async Task ConcurrentFirstUseCreatesOneContainerAndAllObjects()
    {
        var partition = Partition();
        var storages = Enumerable.Range(0, 8).Select(_ => CreateStorage()).ToArray();
        var addresses = Enumerable.Range(0, storages.Length)
            .Select(i => new StorageObjectAddress(partition, $"objects/{i}"))
            .ToArray();

        await Task.WhenAll(storages.Select((storage, i) =>
            storage.CreateAsync(
                addresses[i],
                new MemoryStream([(byte)i]),
                "application/octet-stream",
                default)));

        for (var i = 0; i < storages.Length; i++)
        {
            var result = await storages[i].OpenReadAsync(addresses[i], default);
            Assert.NotNull(result);
            Assert.Equal(new byte[] { (byte)i }, await ReadAllAsync(await result!.OpenReadStreamAsync(default)));
        }
    }

    [Fact]
    public async Task PartitionNamesAreNormalizedConsistently()
    {
        var storage = CreateStorage();
        var mixedCasePartition = $"ConTrAct-{Guid.NewGuid():N}";
        var key = "objects/mixed-case";
        var mixedCase = new StorageObjectAddress(mixedCasePartition, key);
        var lowerCase = new StorageObjectAddress(mixedCasePartition.ToLowerInvariant(), key);

        await storage.CreateAsync(mixedCase, new MemoryStream([4, 5, 6]), null, default);

        Assert.NotNull(await storage.OpenReadAsync(lowerCase, default));
        Assert.True(await storage.DeleteIfExistsAsync(
            new StorageObjectAddress(mixedCasePartition.ToUpperInvariant(), key), default));
    }

    [Fact]
    public async Task ObjectRemovedAfterMetadataLookupFailsAsFileNotFoundWhenOpeningContent()
    {
        var storage = CreateStorage();
        var address = Address();
        await storage.CreateAsync(address, new MemoryStream([7, 8, 9]), null, default);
        var result = await storage.OpenReadAsync(address, default);
        Assert.NotNull(result);

        Assert.True(await storage.DeleteIfExistsAsync(address, default));

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => result!.OpenReadStreamAsync(default));
    }

    [Fact]
    public async Task DeleteRemovesBlobSnapshotsWithBaseObject()
    {
        var storage = CreateStorage();
        var address = Address();
        await storage.CreateAsync(address, new MemoryStream([10, 11]), null, default);

        var container = new BlobContainerClient(
            _azurite.ConnectionString, address.Partition.ToLowerInvariant());
        await container.GetBlobClient(address.Key).CreateSnapshotAsync();

        Assert.True(await storage.DeleteIfExistsAsync(address, default));
        Assert.False((await container.GetBlobClient(address.Key).ExistsAsync()).Value);
    }

    [Fact]
    public async Task CancelledCreateDoesNotPublishObject()
    {
        var storage = CreateStorage();
        var partition = Partition();

        // Initialize the container first so this assertion is specifically about the upload.
        var seed = new StorageObjectAddress(partition, "objects/seed");
        await storage.CreateAsync(seed, new MemoryStream([1]), null, default);

        var cancelled = new StorageObjectAddress(partition, "objects/cancelled");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            storage.CreateAsync(cancelled, new MemoryStream([2]), null, cancellation.Token));
        Assert.Null(await storage.OpenReadAsync(cancelled, default));
    }

    [Fact]
    public async Task InvalidateContainerAllowsRecreationAfterExternalContainerDeletion()
    {
        var storage = CreateStorage();
        var partition = Partition();
        var first = new StorageObjectAddress(partition, "objects/first");
        await storage.CreateAsync(first, new MemoryStream([1]), null, default);

        var container = new BlobContainerClient(_azurite.ConnectionString, partition);
        await container.DeleteAsync();
        storage.InvalidateContainer(partition);

        var second = new StorageObjectAddress(partition, "objects/second");
        await storage.CreateAsync(second, new MemoryStream([2]), null, default);

        var result = await storage.OpenReadAsync(second, default);
        Assert.NotNull(result);
        Assert.Equal(new byte[] { 2 }, await ReadAllAsync(await result!.OpenReadStreamAsync(default)));
    }

    private AzureBlobObjectStorage CreateStorage() =>
        new(new FileOptions { ConnectionString = _azurite.ConnectionString });

    private static StorageObjectAddress Address() =>
        new(Partition(), $"objects/{Guid.NewGuid():N}");

    private static string Partition() => $"contract-{Guid.NewGuid():N}";

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        await using (stream)
        {
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy);
            return copy.ToArray();
        }
    }
}
