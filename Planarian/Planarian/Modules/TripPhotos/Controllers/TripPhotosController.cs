using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Database.Entities.TripObjectives;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.TripPhotos.Controllers;

[Route("api/tripPhotos")]
public class TripPhotosController : PlanarianControllerBase<TripPhotoService>
{
  
    public TripPhotosController(RequestUser requestUser, TripPhotoService service) : base(requestUser, service)
    {
    }

    #region Trip Photos

    [HttpDelete("{tripPhotoId:length(10)}")]
    public async Task<ActionResult<TripObjectiveVm?>> GetTripObjective(string tripPhotoId)
    {
        await Service.DeleteTripPhoto(tripPhotoId);

        return new OkResult();
    }

    #endregion
    
}