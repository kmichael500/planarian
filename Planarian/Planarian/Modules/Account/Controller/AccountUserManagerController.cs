using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Database.Entities.RidgeWalker;
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

    #region User Manager

    [HttpGet]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<ActionResult<IEnumerable<UserManagerGridVm>>> GetAccountUsers()
    {
        var users = await Service.GetAccountUsers();
        return new JsonResult(users);
    }

    [HttpGet("{userId:length(10)}")]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<ActionResult<UserManagerGridVm>> GetUserById(string userId)
    {
        var user = await Service.GetUserById(userId);
        return new JsonResult(user);
    }

    [HttpPost("{userId:length(10)}/resend-invitation")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<IActionResult> ResendInvitation(string userId)
    {
        await Service.ResendInvitation(userId);
        return Ok();
    }

    [HttpPost("")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult> Invite([FromBody] InviteUserRequest request, CancellationToken cancellationToken)
    {
        var userId = await Service.InviteUser(request, cancellationToken);
        return new JsonResult(userId);
    }

    [HttpDelete("{userId:length(10)}")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult> Revoke(string userId)
    {
        await Service.RevokeAccess(userId);
        return Ok();
    }

    #endregion

    #region Permission Management

    [HttpGet("{userId:length(10)}/cave-permissions/{permissionKey}")]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<ActionResult<CavePermissionManagementVm>> GetCavePermissions(string userId,
        string permissionKey)
    {
        var permissions = await Service.GetcavePermissions(userId, permissionKey);
        return new JsonResult(permissions);
    }

    [HttpPut("{userId:length(10)}/cave-permissions/{permissionKey}")]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<IActionResult> UpdateCavePermissions(string userId,
        string permissionKey,
        [FromBody] CreateUserCavePermissionsVm model, CancellationToken cancellationToken)
    {
        await Service.UpdateCavePermissions(userId, permissionKey, model, cancellationToken);
        return Ok();
    }

    [HttpGet("select/permissions")]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<ActionResult<IEnumerable<SelectListItemDescriptionData<string, PermissionSelectListData>>>>
        GetPermissionSelectList([FromQuery] string permissionType)
    {
        var results = await Service.GetPermissionSelectList(permissionType);
        return new JsonResult(results);
    }


    [HttpGet("{userId:length(10)}/user-permissions")]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<ActionResult<IEnumerable<UserPermissionVm>>>
        GetUserPermissions(string userId)
    {
        var perms = await Service.GetUserPermissions(userId);
        return new JsonResult(perms);
    }

    [HttpPost("{userId:length(10)}/user-permissions/{permissionKey}")]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<IActionResult> AddUserPermission(string userId, string permissionKey)
    {
        await Service.AddUserPermission(userId, permissionKey);
        return Ok();
    }

    [HttpDelete("{userId:length(10)}/user-permissions/{permissionKey}")]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<IActionResult> RemoveUserPermission(
        string userId,
        string permissionKey)
    {
        await Service.RemoveUserPermission(userId, permissionKey);
        return Ok();
    }

    #endregion

}