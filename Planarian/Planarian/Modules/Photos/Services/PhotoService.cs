using Planarian.Model.Shared;
using Planarian.Modules.Photos.Repositories;
using Planarian.Shared.Base;
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

        if (photo == null) throw new ArgumentException("Photo not found", nameof(photoId));

        await _blobService.DeleteBlob(photo.BlobKey);

        Repository.Delete(photo);
        await Repository.SaveChangesAsync();
    }
}