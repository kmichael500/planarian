using System.Collections.Concurrent;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Planarian.Library.Exceptions;
using Planarian.Library.Helpers;
using Planarian.Shared.Models;
using FileOptions = Planarian.Shared.Options.FileOptions;

namespace Planarian.Shared.Services;

/// <summary>
/// Azure-specific transport used by archive, chunk-upload, and container-level workflows.
/// Domain File/import-source persistence uses IObjectStorage instead; this service keeps
/// provider-specific transport APIs out of those domain services.
/// </summary>
public class AzureBlobTransportService
{
    private static readonly ConcurrentDictionary<string, Task> ContainerInitializationTasks =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly FileOptions _fileOptions;
    private readonly IObjectStorage _objectStorage;

    public AzureBlobTransportService(FileOptions fileOptions, IObjectStorage objectStorage)
    {
        _fileOptions = fileOptions;
        _objectStorage = objectStorage;
    }

    public async Task<BlobContainerClient> GetBlobContainerClient(
        string containerName, bool createIfNotExists = true)
    {
        var containerClient = new BlobContainerClient(
            _fileOptions.ConnectionString,
            containerName.ToLowerInvariant());

        if (createIfNotExists)
            await EnsureContainerExists(containerClient);

        return containerClient;
    }

    public async Task<Stream> OpenBlobReadStream(
        BlobContainerClient containerClient,
        string blobKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(blobKey))
            throw ApiExceptionDictionary.NotFound("File");

        return await containerClient.GetBlobClient(blobKey)
            .OpenReadAsync(cancellationToken: cancellationToken);
    }

    public async Task<Stream> OpenBlobWriteStream(
        BlobContainerClient containerClient,
        string blobKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(blobKey))
            throw ApiExceptionDictionary.NotFound("File");

        return await containerClient.GetBlobClient(blobKey)
            .OpenWriteAsync(overwrite: true, cancellationToken: cancellationToken);
    }

    public async Task<AuthenticatedFileResponse> CreateBlobResponse(
        string blobKey,
        string containerName,
        string fileName,
        bool isDownload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(blobKey) || string.IsNullOrWhiteSpace(containerName))
            throw ApiExceptionDictionary.NotFound("File");

        var client = await GetBlobContainerClient(containerName, createIfNotExists: false);
        return await BlobService.CreateBlobResponse(
            client.GetBlobClient(blobKey),
            fileName,
            isDownload,
            cancellationToken,
            MimeTypes.GetMimeType(Path.GetExtension(fileName)));
    }

    public async Task DeleteFile(string? blobKey, string? blobContainer)
    {
        if (string.IsNullOrWhiteSpace(blobKey) || string.IsNullOrWhiteSpace(blobContainer))
            throw ApiExceptionDictionary.NotFound("File");

        var client = await GetBlobContainerClient(blobContainer, createIfNotExists: false);
        await client.GetBlobClient(blobKey).DeleteIfExistsAsync();
    }

    public virtual async Task DeleteContainer(string containerName)
    {
        var client = new BlobContainerClient(
            _fileOptions.ConnectionString,
            containerName.ToLowerInvariant());

        try
        {
            await client.DeleteIfExistsAsync();
        }
        finally
        {
            ContainerInitializationTasks.TryRemove(GetContainerInitializationKey(client), out _);
            _objectStorage.InvalidateContainer(client.Name);
        }
    }

    internal static string GetContainerInitializationKey(BlobContainerClient containerClient) =>
        containerClient.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');

    private static async Task EnsureContainerExists(BlobContainerClient containerClient)
    {
        var initializationKey = GetContainerInitializationKey(containerClient);
        for (var attempt = 0; ; attempt++)
        {
            var initializationTask = ContainerInitializationTasks.GetOrAdd(
                initializationKey,
                _ => containerClient.CreateIfNotExistsAsync());
            try
            {
                await initializationTask;
                return;
            }
            catch (RequestFailedException ex) when (
                ex.Status == 409 &&
                ex.ErrorCode == BlobErrorCode.ContainerBeingDeleted &&
                attempt < 9)
            {
                ContainerInitializationTasks.TryRemove(initializationKey, out _);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(2 * (attempt + 1), 8)));
            }
            catch
            {
                ContainerInitializationTasks.TryRemove(initializationKey, out _);
                throw;
            }
        }
    }
}
