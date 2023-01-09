using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Settings.Services;
using Planarian.Modules.Tags.Models;
using Planarian.Modules.Tags.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Tags.Controllers;

[Route("api/tags")]
[Authorize]
public class TagController : PlanarianControllerBase<TagService>
{
    public TagController(RequestUser requestUser, TagService service, TokenService tokenService) : base(requestUser, tokenService, service)
    {
    }


    [HttpPost]
    public async Task<ActionResult> CreateTag([FromBody] CreateOrEditTagVm tag)
    {
        await Service.CreateTag(tag);

        return new OkResult();
    }

}