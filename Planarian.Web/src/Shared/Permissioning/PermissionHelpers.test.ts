import { PermissionKey } from "../../Modules/Authentication/Models/PermissionKey";
import { hasPermission } from "./PermissionHelpers";

const allPermissions = Object.values(PermissionKey);

const expectedFromSinglePermission = new Map<PermissionKey, PermissionKey[]>([
  [PermissionKey.View, [PermissionKey.View]],
  [PermissionKey.Manager, [PermissionKey.Manager, PermissionKey.View]],
  [
    PermissionKey.Admin,
    [
      PermissionKey.Admin,
      PermissionKey.AdminManager,
      PermissionKey.Manager,
      PermissionKey.View,
    ],
  ],
  [PermissionKey.AdminManager, [PermissionKey.AdminManager]],
  [PermissionKey.Export, [PermissionKey.Export]],
  [PermissionKey.PlanarianAdmin, [PermissionKey.PlanarianAdmin]],
]);

describe("permission hierarchy", () => {
  it.each(allPermissions)(
    "projects exactly the supported client hierarchy from %s",
    (grantedPermission) => {
      const expected = new Set(
        expectedFromSinglePermission.get(grantedPermission) ?? []
      );

      for (const requestedPermission of allPermissions) {
        expect(
          hasPermission([grantedPermission], requestedPermission)
        ).toBe(expected.has(requestedPermission));
      }
    }
  );

  it("fails closed when the session has no permissions", () => {
    for (const requestedPermission of allPermissions) {
      expect(hasPermission([], requestedPermission)).toBe(false);
    }
  });

  it("combines synthetic account capabilities without broadening their meaning", () => {
    const permissions = [
      PermissionKey.Manager,
      PermissionKey.AdminManager,
      PermissionKey.Export,
    ];

    expect(hasPermission(permissions, PermissionKey.View)).toBe(true);
    expect(hasPermission(permissions, PermissionKey.Manager)).toBe(true);
    expect(hasPermission(permissions, PermissionKey.AdminManager)).toBe(true);
    expect(hasPermission(permissions, PermissionKey.Export)).toBe(true);
    expect(hasPermission(permissions, PermissionKey.Admin)).toBe(false);
    expect(hasPermission(permissions, PermissionKey.PlanarianAdmin)).toBe(false);
  });
});
