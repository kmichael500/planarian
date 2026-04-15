using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Shared.Models;

namespace Planarian.Shared.Base;

public abstract class PlanarianControllerBase : ControllerBase
{
    protected readonly RequestUser RequestUser;
    protected readonly TokenService TokenService;

    protected PlanarianControllerBase(RequestUser requestUser, TokenService tokenService)
    {
        RequestUser = requestUser;
        TokenService = tokenService;
    }

    // uses IAsyncActionFilter interface
    // [NonAction]
    // public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    // {
    //     var token = context.HttpContext.Request.Headers["Authorization"].ToString()?.Replace("Bearer ", "");
    //     if (string.IsNullOrWhiteSpace(token))
    //     {
    //         token = context.HttpContext.Request.Query["access_token"].ToString();
    //     }
    //
    //     if (!string.IsNullOrWhiteSpace(token))
    //     {
    //         var accountId = context.HttpContext.Request.Headers["x-account"].ToString();
    //         if (string.IsNullOrWhiteSpace(accountId))
    //         {
    //             accountId = context.HttpContext.Request.Query["account_id"].ToString();
    //         }
    //         var userId = TokenService.GetUserIdFromToken(token);
    //         await RequestUser.Initialize(accountId, userId);
    //     }
    //
    //     await next();
    // }

    protected FileStreamResult CreateFileResult(AuthenticatedFileResponse response,
        string cacheControl = "private, max-age=900")
    {
        Response.Headers[HeaderNames.CacheControl] = cacheControl;
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        var result = response.Download && !string.IsNullOrWhiteSpace(response.FileName)
            ? File(response.Stream, response.ContentType, response.FileName)
            : File(response.Stream, response.ContentType);

        result.EntityTag = response.EntityTag;
        result.LastModified = response.LastModified;
        return result;
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
