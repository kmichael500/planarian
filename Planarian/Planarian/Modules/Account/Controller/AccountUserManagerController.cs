using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Account.Services;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Users.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Account.Controller;

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

    #region User Manage

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserManagerGridVm>>> Get()
    {
        var users = await Service.GetAccountUsers();
        return new JsonResult(users);
    }

    [HttpPost("{userId:length(10)}/resend-invitation")]
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
    
    [HttpDelete("{userId:length(10)}")]
    public async Task<ActionResult> Revoke(string userId)
    {
        await Service.RevokeAccess(userId);
        return Ok();
    }

    #endregion

    #region Permission Management

    [HttpGet("{userId:length(10)}/location-permissions/{permissionKey}")]
    public async Task<ActionResult<CavePermissionManagementVm>> GetLocationPermissions(string userId, string permissionKey)
    {
        var permissions = await Service.GetLocationPermissions(userId, permissionKey);
        return new JsonResult(permissions);
    }

    [HttpPut("{userId:length(10)}/location-permissions/{permissionKey}")]
    public async Task<IActionResult> UpdateLocationPermissions(
        string userId,
        string permissionKey,
        [FromBody] CreateUserCavePermissionsVm model)
    {
        await Service.UpdateLocationPermissions(userId, permissionKey, model);
        return Ok();
    }

    #endregion
    
}