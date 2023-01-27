using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Photos.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Photos.Controllers;

[Route("api/photos")]
[Authorize]
public class PhotoController : PlanarianControllerBase<PhotoService>
{
    public PhotoController(RequestUser requestUser, PhotoService service, TokenService tokenService) : base(
        requestUser, tokenService, service)
    {
    }

    #region Trip Photos

    [HttpDelete("{photoId:length(10)}")]
    public async Task<ActionResult> DeletePhoto(string photoId)
    {
        await Service.DeletePhoto(photoId);

        return new OkResult();
    }

    #endregion
}