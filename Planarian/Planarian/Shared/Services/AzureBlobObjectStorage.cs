using System.Collections.Concurrent;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Planarian.Shared.Options;
using FileOptions = Planarian.Shared.Options.FileOptions;

namespace Planarian.Shared.Services;

public sealed class AzureBlobObjectStorage : IObjectStorage
{
    private static readonly ConcurrentDictionary<string, Task> ContainerInitializationTasks =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly FileOptions _options;

    public AzureBlobObjectStorage(FileOptions options) => _options = options;

    public async Task PutAsync(StorageObjectAddress address, Stream content, string? contentType,
        CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(address.Partition, createIfNotExists: true);
        var client = container.GetBlobClient(address.Key);
        var uploadOptions = new BlobUploadOptions();
        if (!string.IsNullOrWhiteSpace(contentType))
            uploadOptions.HttpHeaders = new BlobHttpHeaders { ContentType = contentType };
        await client.UploadAsync(content, uploadOptions, cancellationToken);
    }

    public async Task<StoredObjectReadResult?> OpenReadAsync(StorageObjectAddress address,
        CancellationToken cancellationToken)
    {
        try
        {
            var container = await GetContainerAsync(address.Partition, createIfNotExists: false);
            var client = container.GetBlobClient(address.Key);
            var properties = await client.GetPropertiesAsync(cancellationToken: cancellationToken);
            return new StoredObjectReadResult(
                async readCancellationToken =>
                {
                    try
                    {
                        return await client.OpenReadAsync(
                            new BlobOpenReadOptions(allowModifications: false),
                            readCancellationToken);
                    }
                    catch (RequestFailedException exception) when (exception.Status == 404)
                    {
                        throw new FileNotFoundException("Stored object was not found.", address.Key, exception);
                    }
                },
                properties.Value.ContentType,
                properties.Value.ETag.ToString(),
                properties.Value.LastModified);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> DeleteIfExistsAsync(StorageObjectAddress address,
        CancellationToken cancellationToken)
    {
        try
        {
            var container = await GetContainerAsync(address.Partition, createIfNotExists: false);
            return (await container.GetBlobClient(address.Key)
                .DeleteIfExistsAsync(cancellationToken: cancellationToken)).Value;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return false;
        }
    }

    private async Task<BlobContainerClient> GetContainerAsync(string partition, bool createIfNotExists)
    {
        if (string.IsNullOrWhiteSpace(partition)) throw new ArgumentException("Storage partition is required.", nameof(partition));
        var normalized = partition.ToLowerInvariant();
        var container = new BlobContainerClient(_options.ConnectionString, normalized);
        if (!createIfNotExists) return container;

        var initializationTask = ContainerInitializationTasks.GetOrAdd(normalized, _ => container.CreateIfNotExistsAsync());
        try
        {
            await initializationTask;
        }
        catch
        {
            ContainerInitializationTasks.TryRemove(normalized, out _);
            throw;
        }

        return container;
    }
}
