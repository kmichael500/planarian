using Planarian.Model.Shared;
using Planarian.Library.Exceptions;
using Planarian.Modules.Photos.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Models;
using Planarian.Shared.Services;

namespace Planarian.Modules.Photos.Services;

public class PhotoService : ServiceBase<PhotoRepository>
{
    private readonly BlobService _blobService;

    public PhotoService(PhotoRepository repository, RequestUser requestUser, BlobService blobService) : base(
        repository, requestUser)
    {
        _blobService = blobService;
    }

    public async Task DeletePhoto(string photoId)
    {
        var photo = await Repository.GetPhoto(photoId);

        if (photo == null) throw ApiExceptionDictionary.NotFound("Photo");

        await _blobService.DeleteBlob(photo.BlobKey);

        Repository.Delete(photo);
        await Repository.SaveChangesAsync();
    }

    public async Task<AuthenticatedFileResponse> GetPhotoResponse(string photoId, CancellationToken cancellationToken)
    {
        var photo = await Repository.GetPhoto(photoId);
        if (photo == null || string.IsNullOrWhiteSpace(photo.BlobKey))
            throw ApiExceptionDictionary.NotFound("Photo");

        return await _blobService.CreateBlobResponse(
            photo.BlobKey,
            $"photo{photo.FileType}",
            download: false,
            cancellationToken,
            MimeTypes.GetMimeType(photo.FileType));
    }
}
