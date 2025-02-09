using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Users.Models;
using Planarian.Modules.Users.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Users.Controllers;

[Route("api/account/user-manager")]
[Authorize]
public class AccountUserManagerController : PlanarianControllerBase<AccountUserManagerService>
{
    public AccountUserManagerController(
        RequestUser requestUser, 
        AccountUserManagerService service, 
        TokenService tokenService
    ) : base(requestUser, tokenService, service)
    {
    }
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserManagerGridVm>>> Get()
    {
        var users = await Service.GetAccountUsers();
        return new JsonResult(users);
    }

    [HttpPost("{userId}/resend-invitation")]
    public async Task<IActionResult> ResendInvitation(string userId)
    {
        await Service.ResendInvitation(userId);
        return Ok();
    }

    [HttpPost("")]
    public async Task<ActionResult> Invite([FromBody] InviteUserRequest request, CancellationToken cancellationToken)
    {
        await Service.InviteUser(request, cancellationToken);
        return Ok();
    }
    
    [HttpDelete("{userId}")]
    public async Task<ActionResult> Revoke(string userId)
    {
        await Service.RevokeAccess(userId);
        return Ok();
    }
    
}