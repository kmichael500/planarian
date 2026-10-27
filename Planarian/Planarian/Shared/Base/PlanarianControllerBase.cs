using Microsoft.AspNetCore.Mvc;
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
