using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Library.Exceptions;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Users.Models;
using Planarian.Modules.Users.Services;
using Planarian.Shared.Attributes;
using Planarian.Shared.Base;
using Planarian.Shared.Routing;

namespace Planarian.Modules.Users.Controllers;

[Route("api/users")]
[Authorize]
public class UserController : PlanarianControllerBase<UserService>
{
    private readonly AuthenticationService _authenticationService;
    private readonly RegistrationContinuationService _registrationContinuationService;
    private readonly ILogger<UserController> _logger;

    public UserController(RequestUser requestUser, UserService service, TokenService tokenService,
        AuthenticationService authenticationService, RegistrationContinuationService registrationContinuationService,
        ILogger<UserController> logger) : base(requestUser, tokenService, service)
    {
        _authenticationService = authenticationService;
        _registrationContinuationService = registrationContinuationService;
        _logger = logger;
    }

    #region Confirm

    [AllowAnonymous]
    [HttpPost(UserEmailConfirmationRoutes.Api.Confirm, Name = UserEmailConfirmationRoutes.Api.Names.Confirm)]
    public async Task<ActionResult> ConfirmEmail(string code)
    {
        var confirmation = await Service.ConfirmEmail(code);

        if (User.Identity?.IsAuthenticated == true &&
            confirmation.SessionVersionChanged &&
            string.Equals(RequestUser.Id, confirmation.UserId, StringComparison.Ordinal))
        {
            await _authenticationService.RefreshAuthenticatedSessionForUser(HttpContext, confirmation.UserId);
        }
        else if (User.Identity?.IsAuthenticated != true &&
                 _registrationContinuationService.TryConsume(HttpContext, confirmation.EmailAddress))
        {
            try
            {
                await _authenticationService.SetAuthenticatedSessionForConfirmedUser(
                    HttpContext, confirmation.EmailAddress);
            }
            catch (Exception exception)
            {
                // Email confirmation is the primary operation. Automatic login is a best-effort
                // continuation of the recent registration and must not turn a successful confirmation
                // into an error if session issuance fails.
                _logger.LogError(exception, "Unable to continue the recently registered user session after email confirmation.");
            }
        }

        return new OkResult();
    }

    [AllowAnonymous]
    [HttpPost(UserEmailConfirmationRoutes.Api.Resend, Name = UserEmailConfirmationRoutes.Api.Names.Resend)]
    [Throttle]
    public async Task<ActionResult> ResendEmailConfirmation([FromBody] ResendEmailConfirmationVm? request,
        CancellationToken cancellationToken)
    {
        // TODO: Move Planarian controllers to [ApiController] after preserving the existing ApiErrorResponse contract.
        // Until then, model validation must be enforced explicitly.
        if (request == null || !ModelState.IsValid)
            throw ApiExceptionDictionary.BadRequest("Please enter a valid email address.");

        await Service.ResendEmailConfirmation(request.EmailAddress, cancellationToken);
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
        return await CreateFileResult(result);
    }

    [HttpPut("current")]
    public async Task<ActionResult> UpdateCurrentUser([FromBody] UpdateCurrentUserVm user,
        CancellationToken cancellationToken)
    {
        await Service.UpdateCurrentUser(user, cancellationToken);

        return new OkResult();
    }

    [HttpPut("current/password")]
    public async Task<ActionResult> UpdateCurrentUserPassword([FromBody] UpdateCurrentUserPasswordVm request)
    {
        await Service.UpdateCurrentUserPassword(request);
        await _authenticationService.RefreshAuthenticatedSessionForUser(HttpContext, RequestUser.Id);

        return new OkResult();
    }

    #endregion

    #region Invitations

    [HttpGet(UserInvitationRoutes.Api.Pending, Name = UserInvitationRoutes.Api.Names.Pending)]
    public async Task<ActionResult<IEnumerable<AcceptInvitationVm>>> GetPendingInvitations()
    {
        var result = await Service.GetPendingInvitationsForCurrentUser();

        return new JsonResult(result);
    }

    [HttpPost(UserInvitationRoutes.Api.Accept, Name = UserInvitationRoutes.Api.Names.Accept)]
    public async Task<ActionResult> AcceptInvitation(string code, CancellationToken cancellationToken)
    {
        await Service.AcceptInvitation(code, cancellationToken);
        return new OkResult();
    }

    [AllowAnonymous]
    [HttpPost(UserInvitationRoutes.Api.Decline, Name = UserInvitationRoutes.Api.Names.Decline)]
    public async Task<ActionResult> DeclineInvitation(string code)
    {
        await Service.DeclineInvitation(code);

        return new OkResult();
    }
    
    [AllowAnonymous]
    [HttpGet(UserInvitationRoutes.Api.ByCode, Name = UserInvitationRoutes.Api.Names.Get)]
    public async Task<ActionResult<AcceptInvitationVm>> GetInvitation(string code)
    {
        var result = await Service.GetInvitation(code);

        return new JsonResult(result);
    }
    #endregion

    #region Password Reset

    [AllowAnonymous]
    [HttpPost(UserPasswordResetRoutes.Api.SendEmail, Name = UserPasswordResetRoutes.Api.Names.SendEmail)]
    [Throttle]
    public async Task<ActionResult> SendPasswordReset(string email)
    {
        await Service.SendResetPasswordEmail(email);

        return new OkResult();
    }

    [AllowAnonymous]
    [HttpPost(UserPasswordResetRoutes.Api.Reset, Name = UserPasswordResetRoutes.Api.Names.Reset)]
    public async Task<ActionResult> ResetPassword(string code, [FromBody] string password)
    {
        await Service.ResetPassword(code, password);

        return new OkResult();
    }

    #endregion
}
