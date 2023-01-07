using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Planarian.Model.Shared;

namespace Planarian.Shared.Base;

public abstract class PlanarianControllerBase : ControllerBase, IAsyncActionFilter
{
    protected readonly RequestUser RequestUser;

    protected PlanarianControllerBase(RequestUser requestUser)
    {
        RequestUser = requestUser;
    }

    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        await RequestUser.Initialize("uJ9a1oaA10");
        await next();
    }
}

public abstract class PlanarianControllerBase<TService> : PlanarianControllerBase
{
    protected readonly TService Service;

    protected PlanarianControllerBase(RequestUser requestUser, TService service) : base(requestUser)
    {
        Service = service;
    }
}