export enum PermissionKey {
  View = "View",
  CountyCoordinator = "CountyCoordinator",
  Admin = "Admin",
  PlanarianAdmin = "PlanarianAdmin",
}

export const GetPermissionKeyDisplay = (key: PermissionKey): string => {
  switch (key) {
    case PermissionKey.View:
      return "View";
    case PermissionKey.CountyCoordinator:
      return "County Coordinator";
    case PermissionKey.Admin:
      return "Admin";
    case PermissionKey.PlanarianAdmin:
      return "Planarian Admin";
    default:
      throw new Error("Invalid permission key.");
  }
};
