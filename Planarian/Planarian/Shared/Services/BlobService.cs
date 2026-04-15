using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Net.Http.Headers;
using Planarian.Library.Exceptions;
using Planarian.Library.Options;
using Planarian.Shared.Models;

namespace Planarian.Shared.Services;

public class BlobService
{
    private const string BinaryContentType = "application/octet-stream";

    private readonly BlobContainerClient _containerClient;

    public BlobService(BlobOptions blobOptions)
    {
        _containerClient = new BlobContainerClient(blobOptions.ConnectionString, blobOptions.ContainerName);
    }

    #region Users

    public async Task<string> AddUsersProfilePicture(string userId, Stream stream, string? contentType = null)
    {
        var blobKey = $"{userId}/profilePicture";

        var blobClient = _containerClient.GetBlobClient(blobKey);
        await blobClient.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = string.IsNullOrWhiteSpace(contentType) ? null : contentType
            }
        });

        return blobKey;
    }

    #endregion

    #region Trip Photos

    public async Task<string> AddTripPhoto(string projectId, string tripId,
        string photoId, Stream stream, string fileExtension)
    {
        var blobKey = $"projects/{projectId}/trips/{tripId}/photos/{photoId}";

        var blobClient = _containerClient.GetBlobClient(blobKey);
        await blobClient.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = MimeTypes.GetMimeType(fileExtension)
            }
        });

        return blobKey;
    }

    #endregion

    public async Task<AuthenticatedFileResponse> CreateBlobResponse(string blobKey, string? fileName,
        bool download, CancellationToken cancellationToken, string? fallbackContentType = null)
    {
        if (string.IsNullOrWhiteSpace(blobKey))
            throw ApiExceptionDictionary.NotFound("File");

        var blobClient = _containerClient.GetBlobClient(blobKey);
        return await CreateBlobResponse(blobClient, fileName, download, cancellationToken, fallbackContentType);
    }

    public static async Task<AuthenticatedFileResponse> CreateBlobResponse(BlobClient blobClient, string? fileName,
        bool download, CancellationToken cancellationToken, string? fallbackContentType = null)
    {
        try
        {
            var blobDownload = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            var blobContentType = string.IsNullOrWhiteSpace(blobDownload.Value.Details.ContentType)
                ? fallbackContentType ?? BinaryContentType
                : blobDownload.Value.Details.ContentType;
            var (contentType, forceDownload) = FileResponsePolicy.Resolve(fileName, blobContentType, download);

            return new AuthenticatedFileResponse
            {
                Stream = blobDownload.Value.Content,
                ContentType = contentType,
                FileName = fileName,
                Download = forceDownload,
                EntityTag = EntityTagHeaderValue.Parse(blobDownload.Value.Details.ETag.ToString()),
                LastModified = blobDownload.Value.Details.LastModified
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw ApiExceptionDictionary.NotFound("File");
        }
    }

    public async Task DeleteBlob(string? blobKey)
    {
        if (string.IsNullOrWhiteSpace(blobKey)) return;

        var blobClient = _containerClient.GetBlobClient(blobKey);
        await blobClient.DeleteIfExistsAsync();
    }
}
