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

    public RegisterController(RequestUser requestUser, UserService userService, TokenService tokenService) : base(
        requestUser, tokenService)
    {
        _userService = userService;
    }

    [HttpPost]
    public async Task<ActionResult<string>> Register([FromBody] RegisterUserVm user,
        CancellationToken cancellationToken)
    {
        await _userService.RegisterUser(user, cancellationToken);
        return Ok();
    }
}