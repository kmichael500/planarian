import { PermissionKey } from "../../Modules/Authentication/Models/PermissionKey";

const hasPermission = (
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

  if (permission === PermissionKey.Admin) {
    return permissionKeys.includes(PermissionKey.Admin);
  }

  if (permission === PermissionKey.PlanarianAdmin) {
    return permissionKeys.includes(PermissionKey.PlanarianAdmin);
  }

  return permissionKeys.includes(permission);
};

export { hasPermission };
