using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Users.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Register.Controllers;

[Route("api/register")]
public class RegisterController : PlanarianControllerBase
{
    private readonly UserService _userService;
    private readonly RegistrationContinuationService _registrationContinuationService;

    public RegisterController(RequestUser requestUser, UserService userService, TokenService tokenService,
        RegistrationContinuationService registrationContinuationService) : base(requestUser, tokenService)
    {
        _userService = userService;
        _registrationContinuationService = registrationContinuationService;
    }

    [HttpPost]
    public async Task<ActionResult<RegisterUserResultVm>> Register([FromBody] RegisterUserVm user,
        CancellationToken cancellationToken)
    {
        var result = await _userService.RegisterUser(user, cancellationToken);
        _registrationContinuationService.Issue(HttpContext, user.EmailAddress);
        return Ok(result);
    }
}