using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Users.Models;
using Planarian.Modules.Users.Services;
using Planarian.Shared.Attributes;
using Planarian.Shared.Base;

namespace Planarian.Modules.Users.Controllers;

[Route("api/users")]
[Authorize]
public class UserController : PlanarianControllerBase<UserService>
{
    public UserController(RequestUser requestUser, UserService service, TokenService tokenService) : base(requestUser,
        tokenService, service)
    {
    }

    #region Confirm

    [AllowAnonymous]
    [HttpPost("confirm-email")]
    public async Task<ActionResult> ConfirmEmail(string code)
    {
        await Service.ConfirmEmail(code);

        return new OkResult();
    }

    #endregion

    #region Users

    [HttpGet("current")]
    public async Task<ActionResult<UserVm>> GetCurrentUser()
    {
        var user = await Service.GetUserVm(RequestUser.Id);
        return new JsonResult(user);
    }

    [HttpGet("{userId:length(10)}")]
    public async Task<ActionResult<NameProfilePhotoVm>> GetUserDisplayInfo(string userId)
    {
        var user = await Service.GetUserDisplayInfo(userId);
        return new JsonResult(user);
    }

    [HttpGet("{userId:length(10)}/photo")]
    public async Task<IActionResult> GetUserProfilePhoto(string userId, CancellationToken cancellationToken)
    {
        var result = await Service.GetUserProfilePhotoResponse(userId, cancellationToken);
        return CreateFileResult(result);
    }

    [HttpPut("current")]
    public async Task<ActionResult> UpdateCurrentUser([FromBody] UserVm user)
    {
        await Service.UpdateCurrentUser(user);

        return new OkResult();
    }

    [HttpPut("current/password")]
    public async Task<ActionResult> UpdateCurrentUserPassword([FromBody] string password)
    {
        await Service.UpdateCurrentUserPassword(password);

        return new OkResult();
    }

    #endregion

    #region Invitations

    [HttpGet("invitations")]
    public async Task<ActionResult<IEnumerable<AcceptInvitationVm>>> GetPendingInvitations()
    {
        var result = await Service.GetPendingInvitationsForCurrentUser();

        return new JsonResult(result);
    }

    [HttpPost("invitations/{code:length(10)}/accept")]
    public async Task<ActionResult> AcceptInvitation(string code, CancellationToken cancellationToken)
    {
        await Service.AcceptInvitation(code, cancellationToken);
        return new OkResult();
    }

    [HttpPost("invitations/{code:length(10)}/decline")]
    public async Task<ActionResult> DeclineInvitation(string code)
    {
        await Service.DeclineInvitation(code);

        return new OkResult();
    }
    
    [AllowAnonymous]
    [HttpGet("invitations/{code:length(10)}")]
    public async Task<ActionResult<AcceptInvitationVm>> GetInvitation(string code)
    {
        var result = await Service.GetInvitation(code);

        return new JsonResult(result);
    }
    #endregion

    #region Password Reset

    [AllowAnonymous]
    [HttpPost("reset-password/email/{email}")]
    [Throttle]
    public async Task<ActionResult> SendPasswordReset(string email)
    {
        await Service.SendResetPasswordEmail(email);

        return new OkResult();
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword(string code, [FromBody] string password)
    {
        await Service.ResetPassword(code, password);

        return new OkResult();
    }

    #endregion
}
