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

    /// <summary>
    /// Returns supported persisted roles scoped to an Account for the requested permission domain.
    /// Global platform administration is intentionally outside this vocabulary.
    /// </summary>
    public static string[] GetAccountScopedPersistedRoleKeys(string permissionType) => permissionType switch
    {
        PermissionType.User => [PermissionKey.Admin],
        PermissionType.Cave => [PermissionKey.View, PermissionKey.Manager],
        _ => []
    };

    public static bool IsAccountManagementPermission(string permissionType, string permissionKey) =>
        GetAccountScopedPersistedRoleKeys(permissionType).Contains(permissionKey, StringComparer.Ordinal);

    /// <summary>
    /// Returns account-scoped UserPermission keys that satisfy the requested semantic capability.
    /// Global PlanarianAdmin checks are resolved separately before this mapping is used.
    /// </summary>
    public static string[] GetSatisfyingUserPermissionKeys(string permissionKey) => permissionKey switch
    {
        // Account Admin is the only persisted account-scoped UserPermission that broadens into Cave authority.
        // Platform, synthetic, and unknown capabilities are resolved separately or fail closed.
        PermissionPolicyKey.View => [PermissionPolicyKey.Admin],
        PermissionPolicyKey.Manager => [PermissionPolicyKey.Admin],
        PermissionPolicyKey.Admin => [PermissionPolicyKey.Admin],
        _ => []
    };

    /// <summary>
    /// Returns persisted CavePermission keys that satisfy the requested Cave capability.
    /// </summary>
    public static string[] GetSatisfyingCavePermissionKeys(string permissionKey) => permissionKey switch
    {
        PermissionPolicyKey.View => [PermissionPolicyKey.View, PermissionPolicyKey.Manager],
        PermissionPolicyKey.Manager => [PermissionPolicyKey.Manager],
        _ => []
    };

    /// <summary>
    /// Expands persisted account/Cave role keys into the effective coarse role capabilities returned to clients.
    /// Synthetic capabilities and account settings remain the caller's responsibility.
    /// </summary>
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

    /// <summary>
    /// Identifies platform/synthetic capabilities that must use their coarse resolver even when a resource is supplied.
    /// </summary>
    public static bool BypassesResourceScopeResolution(string permissionKey) =>
        permissionKey is PermissionPolicyKey.PlanarianAdmin
            or PermissionPolicyKey.AdminManager
            or PermissionPolicyKey.Export;
}
