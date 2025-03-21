using Microsoft.AspNetCore.Authorization;
using Planarian.Library.Exceptions;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Models;

namespace Planarian.Modules.Authentication.Services;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly RequestUser _requestUser;

    public PermissionHandler(RequestUser requestUser)
    {
        _requestUser = requestUser;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        foreach (var permissionName in requirement.PermissionNames)
        {
            var hasPermission = await _requestUser.HasCavePermission(permissionName, false);
            if (!hasPermission) continue;
            
            context.Succeed(requirement);
            return;
        }
    }
}

public class RequestUserMiddleware
{
    private readonly RequestDelegate _next;

    public RequestUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Resolve the scoped services from the current request's IServiceProvider.
        var tokenService = context.RequestServices.GetRequiredService<TokenService>();
        var requestUser = context.RequestServices.GetRequiredService<RequestUser>();

        // Extract the token from header or query string.
        var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        if (string.IsNullOrWhiteSpace(token))
        {
            token = context.Request.Query["access_token"];
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            var accountId = context.Request.Headers["x-account"].ToString();
            if (string.IsNullOrWhiteSpace(accountId))
            {
                accountId = context.Request.Query["account_id"];
            }
            var userId = tokenService.GetUserIdFromToken(token);
            await requestUser.Initialize(accountId, userId);
        }

        await _next(context);
    }
}