using Planarian.Shared.Base;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Users.Services;

namespace Planarian.Modules.Users.Controllers;

[Route("api/users")]
public class UserController : PlanarianControllerBase<UserService>
{
    public UserController(RequestUser requestUser, UserService service) : base(requestUser, service)
    {
    }

    #region Users
    
    

    #endregion
}