using Planarian.Model.Shared;
using Planarian.Shared.Base;
using Planarian.Shared.Services;

namespace Planarian.Modules.TripPhotos.Controllers;

public class TripPhotoService : ServiceBase<TripPhotoRepository>
{
    private readonly BlobService _blobService;

    public TripPhotoService(TripPhotoRepository repository, RequestUser requestUser, BlobService blobService) : base(
        repository, requestUser)
    {
        _blobService = blobService;
    }

    public async Task DeleteTripPhoto(string tripPhotoId)
    {
        var photo = await Repository.GetTripPhoto(tripPhotoId);

        if (photo == null)
        {
            throw new ArgumentException("Photo not found", nameof(tripPhotoId));
        }
        
        await _blobService.DeleteBlob(photo.BlobKey);
        
        Repository.Delete(photo);
        await Repository.SaveChangesAsync();
    }
}