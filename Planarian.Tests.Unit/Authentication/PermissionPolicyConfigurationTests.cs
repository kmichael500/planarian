using Microsoft.AspNetCore.Authorization;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Authentication.Models;
using Xunit;

namespace Planarian.Tests.Unit.Authentication;

public sealed class PermissionPolicyConfigurationTests
{
    [Theory]
    [InlineData(PermissionPolicyKey.Admin, PermissionKey.Admin)]
    [InlineData(PermissionPolicyKey.Manager, PermissionKey.Manager)]
    [InlineData(PermissionPolicyKey.View, PermissionKey.View)]
    [InlineData(PermissionPolicyKey.PlanarianAdmin, PermissionKey.PlanarianAdmin)]
    [InlineData(PermissionPolicyKey.Export, PermissionPolicyKey.Export)]
    public void NamedPolicyRequiresOnlyItsSemanticPermission(string policyName, string permissionName)
    {
        var options = new AuthorizationOptions();
        PermissionPolicyConfiguration.Configure(options);

        var policy = options.GetPolicy(policyName);
        Assert.NotNull(policy);
        var requirement = Assert.IsType<PermissionRequirement>(Assert.Single(policy.Requirements));
        Assert.Equal([permissionName], requirement.PermissionNames);
    }
}
