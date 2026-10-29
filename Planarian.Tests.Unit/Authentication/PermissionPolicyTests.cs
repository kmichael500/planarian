using System.Text.RegularExpressions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.App.Models;
using Xunit;

namespace Planarian.Tests.Unit.Authentication;

public sealed class PermissionPolicyTests
{
    [Theory]
    [InlineData(PermissionPolicyKey.View)]
    [InlineData(PermissionPolicyKey.Manager)]
    [InlineData(PermissionPolicyKey.Admin)]
    public void AccountAdminSatisfiesAccountAndCaveRoles(string requestedPermission)
    {
        Assert.Equal(
            new[] { PermissionPolicyKey.Admin },
            PermissionPolicy.GetSatisfyingUserPermissionKeys(requestedPermission));
    }

    [Theory]
    [InlineData(PermissionPolicyKey.PlanarianAdmin)]
    [InlineData(PermissionPolicyKey.AdminManager)]
    [InlineData(PermissionPolicyKey.Export)]
    [InlineData("FutureRole")]
    public void NonAccountAndUnknownCapabilitiesHaveNoSatisfyingAccountUserRole(string permissionKey)
    {
        Assert.Empty(PermissionPolicy.GetSatisfyingUserPermissionKeys(permissionKey));
    }

    [Fact]
    public void CaveHierarchyIsManagerOverViewOnly()
    {
        Assert.Equal(
            new[] { PermissionPolicyKey.View, PermissionPolicyKey.Manager },
            PermissionPolicy.GetSatisfyingCavePermissionKeys(PermissionPolicyKey.View));
        Assert.Equal(
            new[] { PermissionPolicyKey.Manager },
            PermissionPolicy.GetSatisfyingCavePermissionKeys(PermissionPolicyKey.Manager));
        Assert.Empty(PermissionPolicy.GetSatisfyingCavePermissionKeys(PermissionPolicyKey.Admin));
        Assert.Empty(PermissionPolicy.GetSatisfyingCavePermissionKeys(PermissionPolicyKey.PlanarianAdmin));
    }

    [Theory]
    [InlineData(PermissionType.User, PermissionKey.Admin, true)]
    [InlineData(PermissionType.User, PermissionKey.PlanarianAdmin, false)]
    [InlineData(PermissionType.User, PermissionKey.Manager, false)]
    [InlineData(PermissionType.Cave, PermissionKey.View, true)]
    [InlineData(PermissionType.Cave, PermissionKey.Manager, true)]
    [InlineData(PermissionType.Cave, PermissionKey.Admin, false)]
    [InlineData("Unknown", PermissionKey.View, false)]
    public void AccountPermissionManagementUsesExplicitDomainVocabulary(
        string permissionType, string permissionKey, bool expected)
    {
        Assert.Equal(expected, PermissionPolicy.IsAccountManagementPermission(permissionType, permissionKey));
    }

    [Fact]
    public void AssignableKeysMatchTheDomainVocabulary()
    {
        Assert.Equal(new[] { PermissionKey.Admin }, PermissionPolicy.GetAccountScopedPersistedRoleKeys(PermissionType.User));
        Assert.Equal(
            new[] { PermissionKey.View, PermissionKey.Manager },
            PermissionPolicy.GetAccountScopedPersistedRoleKeys(PermissionType.Cave));
        Assert.Empty(PermissionPolicy.GetAccountScopedPersistedRoleKeys("Unknown"));
    }

    [Fact]
    public void AccountAdminProjectsTheEffectiveAccountRoleHierarchy()
    {
        Assert.Equal(
            new[] { PermissionPolicyKey.View, PermissionPolicyKey.Manager, PermissionPolicyKey.Admin },
            PermissionPolicy.GetEffectiveAccountRoleCapabilities(
                new[] { PermissionPolicyKey.Admin },
                Array.Empty<string>()));
    }

    [Fact]
    public void CaveManagerProjectsManagerAndViewWhileCaveViewProjectsOnlyView()
    {
        Assert.Equal(
            new[] { PermissionPolicyKey.View, PermissionPolicyKey.Manager },
            PermissionPolicy.GetEffectiveAccountRoleCapabilities(
                Array.Empty<string>(),
                new[] { PermissionPolicyKey.Manager }));
        Assert.Equal(
            new[] { PermissionPolicyKey.View },
            PermissionPolicy.GetEffectiveAccountRoleCapabilities(
                Array.Empty<string>(),
                new[] { PermissionPolicyKey.View }));
    }

    [Fact]
    public void MalformedCrossDomainRolesDoNotProjectEffectiveAccountRoles()
    {
        Assert.Empty(PermissionPolicy.GetEffectiveAccountRoleCapabilities(
            new[] { PermissionPolicyKey.Manager, PermissionPolicyKey.PlanarianAdmin },
            new[] { PermissionPolicyKey.Admin }));
    }

    [Theory]
    [InlineData(PermissionPolicyKey.PlanarianAdmin, true)]
    [InlineData(PermissionPolicyKey.AdminManager, true)]
    [InlineData(PermissionPolicyKey.Export, true)]
    [InlineData(PermissionPolicyKey.Admin, false)]
    [InlineData(PermissionPolicyKey.Manager, false)]
    [InlineData(PermissionPolicyKey.View, false)]
    public void OnlyPlatformAndSyntheticCapabilitiesBypassResourceScopeResolution(
        string permissionKey, bool expected)
    {
        Assert.Equal(expected, PermissionPolicy.BypassesResourceScopeResolution(permissionKey));
    }

    [Fact]
    public void AppInitializationAdvertisesTheCanonicalClientProjectionVersion()
    {
        var vm = new AppInitializeVm("https://api.example", "https://signalr.example", "Support", "support@example.com");

        Assert.Equal(PermissionPolicy.ClientProjectionVersion, vm.PermissionProjectionVersion);

        var root = FindRepositoryRoot();
        var helper = System.IO.File.ReadAllText(Path.Combine(
            root, "Planarian.Web", "src", "Shared", "Permissioning", "PermissionHelpers.ts"));
        var versionMatch = Regex.Match(helper, @"effectiveProjectionVersion\s*=\s*(?<version>\d+)");

        Assert.True(versionMatch.Success);
        Assert.Equal(
            PermissionPolicy.ClientProjectionVersion,
            int.Parse(versionMatch.Groups["version"].Value));
    }

    [Fact]
    public void UserCavePermissionsSqlTracksTheCanonicalPersistedHierarchy()
    {
        var root = FindRepositoryRoot();
        var sql = System.IO.File.ReadAllText(Path.Combine(
            root, "Planarian", "Planarian.Model", "Database", "Views", "UserCavePermissions.sql"));

        var caveKeys = PermissionPolicy.GetAccountScopedPersistedRoleKeys(PermissionType.Cave);
        var caveKeyLists = Regex.Matches(
                sql, @"p.""Key""::text IN [(](?<values>[^)]+)[)]")
            .Select(match => Regex.Matches(match.Groups["values"].Value, @"'(?<key>[^']+)'::text")
                .Select(value => value.Groups["key"].Value)
                .ToArray())
            .ToArray();

        Assert.NotEmpty(caveKeyLists);
        Assert.All(caveKeyLists, keys =>
            Assert.Equal(
                caveKeys.OrderBy(key => key).ToArray(),
                keys.OrderBy(key => key).ToArray()));

        var managerUserKeys = PermissionPolicy.GetSatisfyingUserPermissionKeys(PermissionPolicyKey.Manager);
        var accountRoleMatches = Regex.Matches(
                sql,
                @"""Permissions"".""Key""::text = '(?<key>[^']+)'::text[ \r\n\t]+AND ""Permissions"".""PermissionType""::text = 'User'::text")
            .Select(match => match.Groups["key"].Value)
            .ToArray();

        Assert.NotEmpty(accountRoleMatches);
        Assert.All(accountRoleMatches, key => Assert.Contains(key, managerUserKeys));
        Assert.Equal(managerUserKeys.OrderBy(key => key).ToArray(), accountRoleMatches.Distinct().OrderBy(key => key).ToArray());
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "Planarian", "Planarian.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Unable to locate the Planarian repository root.");
    }
}
