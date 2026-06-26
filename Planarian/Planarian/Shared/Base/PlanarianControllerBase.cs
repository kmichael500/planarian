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
        Response.Headers[HeaderNames.CacheControl] = cacheControl;
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        if (IsNotModified(response))
        {
            var responseHeaders = Response.GetTypedHeaders();
            responseHeaders.ETag = response.EntityTag;
            responseHeaders.LastModified = response.LastModified;
            return StatusCode(StatusCodes.Status304NotModified);
        }

        var stream = await response.OpenReadStreamAsync(HttpContext.RequestAborted);
        var result = response.Download && !string.IsNullOrWhiteSpace(response.FileName)
            ? File(stream, response.ContentType, response.FileName)
            : File(stream, response.ContentType);

        result.EntityTag = response.EntityTag;
        result.LastModified = response.LastModified;
        return result;
    }

    private bool IsNotModified(AuthenticatedFileResponse response)
    {
        var requestHeaders = Request.GetTypedHeaders();

        if (response.EntityTag != null && requestHeaders.IfNoneMatch?.Count > 0)
        {
            return requestHeaders.IfNoneMatch.Any(requestEntityTag =>
                requestEntityTag.ToString() == "*"
                || requestEntityTag.Compare(response.EntityTag, useStrongComparison: false));
        }

        if (response.LastModified != null && requestHeaders.IfModifiedSince != null)
        {
            return TruncateToSecond(requestHeaders.IfModifiedSince.Value) >= TruncateToSecond(response.LastModified.Value);
        }

        return false;
    }

    private static DateTimeOffset TruncateToSecond(DateTimeOffset value)
        => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Offset);
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
