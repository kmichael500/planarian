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

    public void InvalidateContainer(string partition)
    {
        var normalized = partition.ToLowerInvariant();
        var container = new BlobContainerClient(_options.ConnectionString, normalized);
        ContainerInitializationTasks.TryRemove(GetContainerInitializationKey(container), out _);
    }

    public async Task CreateAsync(StorageObjectAddress address, Stream content, string? contentType,
        CancellationToken cancellationToken)
    {
        var uploadOptions = new BlobUploadOptions
        {
            // Object addresses are immutable. The BlobUploadOptions overload otherwise overwrites
            // existing block blobs, so require nonexistence at the provider boundary.
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
        };
        if (!string.IsNullOrWhiteSpace(contentType))
            uploadOptions.HttpHeaders = new BlobHttpHeaders { ContentType = contentType };

        // Azure can return ContainerBeingDeleted after a successful account reset.
        // Rewind only seekable streams; non-seekable streams are not safe to replay.
        var initialPosition = content.CanSeek ? content.Position : 0;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var container = await GetContainerAsync(address.Partition, createIfNotExists: true);
                await container.GetBlobClient(address.Key).UploadAsync(content, uploadOptions, cancellationToken);
                return;
            }
            catch (RequestFailedException ex) when (ex.Status == 409 &&
                ex.ErrorCode == BlobErrorCode.ContainerBeingDeleted && attempt < 9)
            {
                InvalidateContainer(address.Partition);
                if (!content.CanSeek)
                    throw;
                if (content.CanSeek) content.Position = initialPosition;
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(2 * (attempt + 1), 8)), cancellationToken);
            }
        }
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
                .DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, conditions: null, cancellationToken)).Value;
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

        var initializationKey = GetContainerInitializationKey(container);
        var initializationTask = ContainerInitializationTasks.GetOrAdd(
            initializationKey, _ => container.CreateIfNotExistsAsync());
        try
        {
            await initializationTask;
        }
        catch
        {
            ContainerInitializationTasks.TryRemove(initializationKey, out _);
            throw;
        }

        return container;
    }

    internal static string GetContainerInitializationKey(BlobContainerClient containerClient) =>
        containerClient.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
}
