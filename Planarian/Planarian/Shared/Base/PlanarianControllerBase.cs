using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;

namespace Planarian.Shared.Base;

public abstract class PlanarianControllerBase : ControllerBase, IAsyncActionFilter
{
    protected readonly RequestUser RequestUser;
    protected readonly TokenService TokenService;

    protected PlanarianControllerBase(RequestUser requestUser, TokenService tokenService)
    {
        RequestUser = requestUser;
        TokenService = tokenService;
    }

    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var token = context.HttpContext.Request.Headers["Authorization"].ToString()?.Replace("Bearer ", "");
        if (string.IsNullOrWhiteSpace(token))
        {
            token = context.HttpContext.Request.Query["access_token"].ToString();
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            var accountId = context.HttpContext.Request.Headers["x-account"].ToString();
            if (string.IsNullOrWhiteSpace(accountId))
            {
                accountId = context.HttpContext.Request.Query["account_id"].ToString();
            }
            var userId = TokenService.GetUserIdFromToken(token);
            await RequestUser.Initialize(accountId, userId);
        }

        await next();
    }
}

public abstract class PlanarianControllerBase<TService> : PlanarianControllerBase
{
    protected readonly TService Service;

    protected PlanarianControllerBase(RequestUser requestUser, TokenService tokenService, TService service) : base(
        requestUser, tokenService)
    {
        Service = service;
    }
}