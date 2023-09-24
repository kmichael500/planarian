using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Services;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Caves.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Account.Controller;

[Route(Route)]
[Authorize]
public class AccountController : PlanarianControllerBase<AccountService>
{
    private const string Route = "api/account";

    private readonly CaveService _caveService;

    public AccountController(RequestUser requestUser, TokenService tokenService, AccountService service,
        CaveService caveService) : base(
        requestUser, tokenService, service)
    {
        _caveService = caveService;
    }

    [HttpDelete("reset")]
    public async Task<ActionResult<string>> RestAccount(CancellationToken cancellationToken)
    {
        await Service.ResetAccount(cancellationToken);
        return Ok();
    }

}