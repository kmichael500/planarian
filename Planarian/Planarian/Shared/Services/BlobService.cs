using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Caching.Memory;
using Planarian.Library.Options;

namespace Planarian.Shared.Services;

public class BlobService
{
    public const string TemporaryBackupPrefix = "temp/backups";

    private readonly MemoryCache _cache;
    private readonly BlobContainerClient _containerClient;

    // Keep this service singleton only while it depends on shared thread-safe components.
    // If it ever needs scoped state such as RequestUser or DbContext, resolve it in the cleanup worker via IServiceScopeFactory.
    public BlobService(BlobOptions blobOptions, MemoryCache cache)
    {
        _cache = cache;
        _containerClient = new BlobContainerClient(blobOptions.ConnectionString, blobOptions.ContainerName);
    }

    #region Users

    public async Task<string> AddUsersProfilePicture(string userId, Stream stream)
    {
        var blobKey = $"{userId}/profilePicture";
        await _containerClient.UploadBlobAsync(blobKey, stream);
        return blobKey;
    }

    #endregion

    #region Trip Photos

    public async Task<string> AddTripPhoto(string projectId, string tripId,
        string photoId, Stream stream, string fileExtension)
    {
        var blobKey = $"projects/{projectId}/trips/{tripId}/photos/{photoId}";

        await _containerClient.UploadBlobAsync(blobKey, stream);

        return blobKey;
    }

    #endregion

    #region Temporary Backups

    public async Task<string> AddTemporaryBackupArchive(string accountId, Stream stream, CancellationToken cancellationToken)
    {
        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobKey = $"{TemporaryBackupPrefix}/{accountId}/{Guid.NewGuid():N}.zip";
        var blobClient = _containerClient.GetBlobClient(blobKey);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        await blobClient.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/zip"
            }
        }, cancellationToken);

        return blobKey;
    }

    public string GetTemporaryBackupDownloadUrl(string blobKey, string fileName, int expiresInHours)
    {
        var blobClient = _containerClient.GetBlobClient(blobKey);
        var now = DateTimeOffset.UtcNow;
        var sasBuilder = new BlobSasBuilder(BlobSasPermissions.Read, now.AddHours(Math.Max(1, expiresInHours)))
        {
            BlobContainerName = blobClient.BlobContainerName,
            BlobName = blobClient.Name,
            Resource = "b",
            StartsOn = now.AddMinutes(-5),
            ContentDisposition = $"attachment; filename=\"{fileName}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}",
            ContentType = "application/zip"
        };

        return blobClient.GenerateSasUri(sasBuilder).ToString();
    }

    public async Task DeleteExpiredTemporaryBackupBlobs(int retentionHours, CancellationToken cancellationToken)
    {
        try
        {
            var containerExists = await _containerClient.ExistsAsync(cancellationToken);
            if (!containerExists.Value)
            {
                return;
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404 || string.Equals(ex.ErrorCode, BlobErrorCode.ContainerNotFound.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var retentionCutoff = DateTimeOffset.UtcNow.AddHours(-Math.Max(1, retentionHours));

        await foreach (var blobItem in _containerClient.GetBlobsAsync(
                           prefix: $"{TemporaryBackupPrefix}/",
                           cancellationToken: cancellationToken))
        {
            if (blobItem.Properties.LastModified is not { } lastModified || lastModified > retentionCutoff)
            {
                continue;
            }

            await _containerClient.DeleteBlobIfExistsAsync(
                blobItem.Name,
                DeleteSnapshotsOption.IncludeSnapshots,
                cancellationToken: cancellationToken);

            _cache.Remove(blobItem.Name);
        }
    }

    #endregion

    public Uri? GetSasUrl(string blobKey, int expiresInHours = 48)
    {
        var exists = _cache.TryGetValue(blobKey, out Uri? uri);
        if (exists) return uri;

        var blobClient = _containerClient.GetBlobClient(blobKey);

        var now = DateTimeOffset.UtcNow;

        var sasExpiresOn = now.AddHours(expiresInHours);
        var cacheExpiresOn = now.AddHours(expiresInHours).AddMinutes(-10);
        var maxAge = (cacheExpiresOn - now).TotalSeconds;

        var blobSasBuilder = new BlobSasBuilder(BlobSasPermissions.Read, sasExpiresOn)
        {
            ContentType = "image/jpg",
            CacheControl = $"public, max-age={maxAge}",
            ContentLanguage = "en-US"
        };

        var sasUrl = blobClient.GenerateSasUri(blobSasBuilder);
        _cache.Set(blobKey, sasUrl, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = cacheExpiresOn
        });
        return sasUrl;
    }


    public async Task DeleteBlob(string? blobKey)
    {
        if (string.IsNullOrWhiteSpace(blobKey)) return;

        var blobClient = _containerClient.GetBlobClient(blobKey);
        await blobClient.DeleteIfExistsAsync();
    }
}