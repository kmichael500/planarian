using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Users.Models;
using Planarian.Modules.Users.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Users.Controllers;

[Route("api/user-manager")]
[Authorize]
public class UserManagerController : PlanarianControllerBase<UserManagerService>
{
    public UserManagerController(
        RequestUser requestUser, 
        UserManagerService service, 
        TokenService tokenService
    ) : base(requestUser, tokenService, service)
    {
    }
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AccountUserVm>>> Get()
    {
        var users = await Service.GetAccountUsers();
        return new JsonResult(users);
    }
    
    [HttpPost("invite")]
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