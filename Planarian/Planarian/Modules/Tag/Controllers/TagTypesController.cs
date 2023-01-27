using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Tag.Models;
using Planarian.Modules.Tags.Models;
using Planarian.Modules.Tags.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Tags.Controllers;

[Route("api/tag-types")]
[Authorize]
public class TagTypesController : PlanarianControllerBase<TagService>
{
    public TagTypesController(RequestUser requestUser, TagService service, TokenService tokenService) : base(requestUser,
        tokenService, service)
    {
    }


    [HttpPost]
    public async Task<ActionResult> CreateTag([FromBody] CreateOrEditTagTypeVm tagType)
    {
        await Service.CreateTag(tagType);

        return new OkResult();
    }
}