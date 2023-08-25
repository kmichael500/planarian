using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Caves.Services;
using Planarian.Modules.Files.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Import.Controllers;

[Route("api/import")]
[Authorize]
public class ImportController : PlanarianControllerBase<CaveService>
{
    public ImportController(RequestUser requestUser, TokenService tokenService, CaveService service) : base(requestUser, tokenService, service)
    {
    }
    
}