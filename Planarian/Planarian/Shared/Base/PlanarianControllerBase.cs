using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
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

    protected async Task<IActionResult> CreateFileResult(AuthenticatedFileResponse response,
        string cacheControl = "private, no-cache")
    {
        var isFileStreamSession = RequestThrottleService.HasValidFileStreamSession(Request);
        Response.Headers[HeaderNames.CacheControl] = isFileStreamSession ? "private, no-store" : cacheControl;
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        var stream = await response.OpenReadStreamAsync(HttpContext.RequestAborted);
        var result = response.Download && !string.IsNullOrWhiteSpace(response.FileName)
            ? File(stream, response.ContentType, response.FileName)
            : File(stream, response.ContentType);

        // Let ASP.NET Core own conditional/range semantics for ordinary responses.
        // PDF.js stream sessions deliberately omit validators so a segmented byte
        // request cannot be answered with 304 instead of the requested range.
        if (!isFileStreamSession)
        {
            result.EntityTag = response.EntityTag;
            result.LastModified = response.LastModified;
        }

        result.EnableRangeProcessing = true;
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
