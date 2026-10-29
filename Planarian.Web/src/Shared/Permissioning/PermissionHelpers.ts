import { PermissionKey } from "../../Modules/Authentication/Models/PermissionKey";

const effectiveProjectionVersion = 2;

const hasLegacyPermission = (
  permissionKeys: PermissionKey[],
  permission: PermissionKey
): boolean => {
  if (permission === PermissionKey.View) {
    return (
      permissionKeys.includes(PermissionKey.View) ||
      permissionKeys.includes(PermissionKey.Manager) ||
      permissionKeys.includes(PermissionKey.Admin)
    );
  }

  if (permission === PermissionKey.Manager) {
    return (
      permissionKeys.includes(PermissionKey.Manager) ||
      permissionKeys.includes(PermissionKey.Admin)
    );
  }

  if (permission === PermissionKey.AdminManager) {
    return (
      permissionKeys.includes(PermissionKey.AdminManager) ||
      permissionKeys.includes(PermissionKey.Admin)
    );
  }

  return permissionKeys.includes(permission);
};

const hasPermission = (
  permissionKeys: PermissionKey[],
  permission: PermissionKey,
  projectionVersion?: number
): boolean => {
  if (
    projectionVersion != null &&
    projectionVersion >= effectiveProjectionVersion
  ) {
    return permissionKeys.includes(permission);
  }

  // Rolling-deployment compatibility only. Older APIs return persisted roles
  // rather than the server-expanded effective capability set.
  return hasLegacyPermission(permissionKeys, permission);
};

export { hasPermission };
