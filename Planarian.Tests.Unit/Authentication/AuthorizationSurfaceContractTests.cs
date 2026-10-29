using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Account.Controller;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Caves.Controllers;
using Xunit;

namespace Planarian.Tests.Unit.Authentication;

public sealed class AuthorizationSurfaceContractTests
{
    [Fact]
    public void CaveControllerNamedPoliciesMatchThePublishedAuthorizationBoundary()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(CaveController.ExportCavesGpx)] = PermissionPolicyKey.Export,
            [nameof(CaveController.ExportCavesCsv)] = PermissionPolicyKey.Export,
            [nameof(CaveController.GetEditAuthoringContext)] = PermissionPolicyKey.Manager,
            [nameof(CaveController.GetNextCountyNumber)] = PermissionPolicyKey.Manager,
            [nameof(CaveController.IsCountyNumberInUse)] = PermissionPolicyKey.Manager,
            [nameof(CaveController.AddCave)] = PermissionPolicyKey.Manager,
            [nameof(CaveController.UpdateCave)] = PermissionPolicyKey.Manager,
            [nameof(CaveController.DeleteCave)] = PermissionPolicyKey.Manager,
            [nameof(CaveController.ArchiveCave)] = PermissionPolicyKey.Manager,
            [nameof(CaveController.UnarchiveCave)] = PermissionPolicyKey.Manager
        };

        AssertNamedPolicies(typeof(CaveController), expected);
    }

    [Fact]
    public void AccountUserManagerPoliciesPreserveManagerAndAdminBoundaries()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(AccountUserManagerController.GetAccountUsers)] = PermissionPolicyKey.Manager,
            [nameof(AccountUserManagerController.GetUserById)] = PermissionPolicyKey.Manager,
            [nameof(AccountUserManagerController.GetInvitationEmailHistory)] = PermissionPolicyKey.Manager,
            [nameof(AccountUserManagerController.GetCavePermissions)] = PermissionPolicyKey.Manager,
            [nameof(AccountUserManagerController.UpdateCavePermissions)] = PermissionPolicyKey.Manager,
            [nameof(AccountUserManagerController.GetPermissionSelectList)] = PermissionPolicyKey.Manager,
            [nameof(AccountUserManagerController.ResendInvitation)] = PermissionPolicyKey.Admin,
            [nameof(AccountUserManagerController.Invite)] = PermissionPolicyKey.Admin,
            [nameof(AccountUserManagerController.Revoke)] = PermissionPolicyKey.Admin,
            [nameof(AccountUserManagerController.GetUserPermissions)] = PermissionPolicyKey.Admin,
            [nameof(AccountUserManagerController.AddUserPermission)] = PermissionPolicyKey.Admin,
            [nameof(AccountUserManagerController.RemoveUserPermission)] = PermissionPolicyKey.Admin
        };

        AssertNamedPolicies(typeof(AccountUserManagerController), expected);
    }

    [Fact]
    public void EveryNamedControllerPolicyIsRegistered()
    {
        var options = new AuthorizationOptions();
        PermissionPolicyConfiguration.Configure(options);

        var violations = typeof(CaveController).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type =>
                type.GetCustomAttributes<AuthorizeAttribute>()
                    .Select(attribute => (Surface: type.FullName!, attribute.Policy))
                    .Concat(type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                        .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>()
                            .Select(attribute =>
                                (Surface: $"{type.FullName}.{method.Name}", attribute.Policy)))))
            .Where(entry =>
                !string.IsNullOrWhiteSpace(entry.Policy) &&
                options.GetPolicy(entry.Policy!) == null)
            .Select(entry => $"{entry.Surface}: {entry.Policy}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();

        Assert.True(violations.Count == 0,
            $"Unregistered named authorization policies:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    private static void AssertNamedPolicies(
        Type controllerType, IReadOnlyDictionary<string, string> expected)
    {
        var actual = controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>()
                .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Policy))
                .Select(attribute => (method.Name, Policy: attribute.Policy!)))
            .ToDictionary(entry => entry.Name, entry => entry.Policy, StringComparer.Ordinal);

        Assert.Equal(expected.Count, actual.Count);
        foreach (var (methodName, policy) in expected)
        {
            Assert.True(actual.TryGetValue(methodName, out var actualPolicy),
                $"Expected {controllerType.Name}.{methodName} to declare authorization policy {policy}.");
            Assert.Equal(policy, actualPolicy);
        }
    }

}
