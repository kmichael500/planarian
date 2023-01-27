using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Caching.Memory;
using Planarian.Library.Options;
using Planarian.Shared.Options;

namespace Planarian.Shared.Services;

public class BlobService
{
    private readonly MemoryCache _cache;
    private readonly BlobContainerClient _containerClient;

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

    public Uri? GetSasUrl(string blobKey, int expiresInHours = 1)
    {
        var exists = _cache.TryGetValue(blobKey, out Uri uri);
        if (exists) return uri;
        var expiresOn = DateTimeOffset.UtcNow.AddHours(expiresInHours);

        var blobClient = _containerClient.GetBlobClient(blobKey);
        var maxCacheAgeInDays = 2;

        var blobSasBuilder = new BlobSasBuilder(BlobSasPermissions.Read, expiresOn)
        {
            ContentType = "image/jpg",
            CacheControl = "public, max-age=31536000",
            ContentLanguage = "en-US"
        };
        var sasUrl = blobClient.GenerateSasUri(blobSasBuilder);
        _cache.Set(blobKey, sasUrl, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(31536000)
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