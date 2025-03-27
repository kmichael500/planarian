export enum PermissionKey {
  View = "View",
  Manager = "Manager",
  AdminManager = "AdminManager",
  Admin = "Admin",
  PlanarianAdmin = "PlanarianAdmin",
  Export = "Export",
}

export const GetPermissionKeyDisplay = (key: PermissionKey): string => {
  switch (key) {
    case PermissionKey.View:
      return "View";
    case PermissionKey.Manager:
      return "Manager";
    case PermissionKey.Admin:
      return "Admin";
    case PermissionKey.PlanarianAdmin:
      return "Planarian Admin";
    default:
      throw new Error("Invalid permission key.");
  }
};
