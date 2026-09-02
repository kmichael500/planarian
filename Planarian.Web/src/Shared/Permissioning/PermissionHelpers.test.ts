import { PermissionKey } from "../../Modules/Authentication/Models/PermissionKey";
import { hasPermission } from "./PermissionHelpers";

describe("permission hierarchy", () => {
  it("keeps PlanarianAdmin platform-scoped", () => {
    const permissions = [PermissionKey.PlanarianAdmin];

    expect(hasPermission(permissions, PermissionKey.PlanarianAdmin)).toBe(true);
    expect(hasPermission(permissions, PermissionKey.Admin)).toBe(false);
    expect(hasPermission(permissions, PermissionKey.AdminManager)).toBe(false);
    expect(hasPermission(permissions, PermissionKey.Manager)).toBe(false);
    expect(hasPermission(permissions, PermissionKey.View)).toBe(false);
    expect(hasPermission(permissions, PermissionKey.Export)).toBe(false);
  });

  it("preserves the account hierarchy from Admin through View", () => {
    const permissions = [PermissionKey.Admin];

    expect(hasPermission(permissions, PermissionKey.Admin)).toBe(true);
    expect(hasPermission(permissions, PermissionKey.AdminManager)).toBe(true);
    expect(hasPermission(permissions, PermissionKey.Manager)).toBe(true);
    expect(hasPermission(permissions, PermissionKey.View)).toBe(true);
    expect(hasPermission(permissions, PermissionKey.PlanarianAdmin)).toBe(
      false
    );
  });
});
