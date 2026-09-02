using Microsoft.AspNetCore.Authorization;
using Planarian.Model.Database.Entities.RidgeWalker;

namespace Planarian.Modules.Authentication.Models;

public static class PermissionPolicyConfiguration
{
    public static void Configure(AuthorizationOptions options)
    {
        Add(options, PermissionPolicyKey.Admin, PermissionKey.Admin);
        Add(options, PermissionPolicyKey.PlanarianAdmin, PermissionKey.PlanarianAdmin);
        Add(options, PermissionPolicyKey.Manager, PermissionKey.Manager);
        Add(options, PermissionPolicyKey.View, PermissionKey.View);
        Add(options, PermissionPolicyKey.Export, PermissionPolicyKey.Export);
    }

    private static void Add(AuthorizationOptions options, string policyName, string permissionName)
    {
        options.AddPolicy(policyName, policy =>
            policy.Requirements.Add(new PermissionRequirement([permissionName])));
    }
}
