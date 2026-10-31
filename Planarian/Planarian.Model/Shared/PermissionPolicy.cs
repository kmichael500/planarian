using Planarian.Model.Database.Entities.RidgeWalker;

namespace Planarian.Model.Shared;

/// <summary>
/// Canonical permission vocabulary and role-inheritance semantics.
/// Resource-scope resolution, persistence, and account settings remain owned by their feature-specific callers.
/// </summary>
public static class PermissionPolicy
{
    public const int ClientProjectionVersion = 2;

    private static readonly string[] AccountRoleCapabilities =
        [PermissionPolicyKey.View, PermissionPolicyKey.Manager, PermissionPolicyKey.Admin];

    public static string[] GetAccountScopedPersistedRoleKeys(string permissionType) => permissionType switch
    {
        PermissionType.User => [PermissionKey.Admin],
        PermissionType.Cave => [PermissionKey.View, PermissionKey.Manager],
        _ => []
    };

    public static bool IsAccountManagementPermission(string permissionType, string permissionKey) =>
        GetAccountScopedPersistedRoleKeys(permissionType).Contains(permissionKey, StringComparer.Ordinal);

    public static string[] GetSatisfyingUserPermissionKeys(string permissionKey) => permissionKey switch
    {
        // Account Admin is the only persisted account-scoped UserPermission that broadens into Cave authority.
        // Platform, synthetic, and unknown capabilities are resolved separately or fail closed.
        PermissionPolicyKey.View => [PermissionPolicyKey.Admin],
        PermissionPolicyKey.Manager => [PermissionPolicyKey.Admin],
        PermissionPolicyKey.Admin => [PermissionPolicyKey.Admin],
        _ => []
    };

    public static string[] GetSatisfyingCavePermissionKeys(string permissionKey) => permissionKey switch
    {
        PermissionPolicyKey.View => [PermissionPolicyKey.View, PermissionPolicyKey.Manager],
        PermissionPolicyKey.Manager => [PermissionPolicyKey.Manager],
        _ => []
    };

    public static string[] GetEffectiveAccountRoleCapabilities(
        IEnumerable<string> userPermissionKeys,
        IEnumerable<string> cavePermissionKeys)
    {
        var userPermissions = userPermissionKeys.ToHashSet(StringComparer.Ordinal);
        var cavePermissions = cavePermissionKeys.ToHashSet(StringComparer.Ordinal);

        return AccountRoleCapabilities
            .Where(capability =>
                GetSatisfyingUserPermissionKeys(capability).Any(userPermissions.Contains) ||
                GetSatisfyingCavePermissionKeys(capability).Any(cavePermissions.Contains))
            .ToArray();
    }

    public static bool BypassesResourceScopeResolution(string permissionKey) =>
        permissionKey is PermissionPolicyKey.PlanarianAdmin
            or PermissionPolicyKey.AdminManager
            or PermissionPolicyKey.Export;
}
