using Microsoft.AspNetCore.Authorization;

namespace Planarian.Modules.Authentication.Models;

public class PermissionRequirement : IAuthorizationRequirement
{
    public List<string> PermissionNames { get; }

    public PermissionRequirement(List<string> permissionNames)
    {
        PermissionNames = permissionNames;
    }
}