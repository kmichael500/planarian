import { PermissionKey } from "../../Modules/Authentication/Models/PermissionKey";
import { hasPermission } from "./PermissionHelpers";

const allPermissions = Object.values(PermissionKey);
const effectiveProjectionVersion = 2;

describe("server-projected permissions", () => {
  it.each(allPermissions)(
    "checks %s by exact membership for effective server projections",
    (grantedPermission) => {
      for (const requestedPermission of allPermissions) {
        expect(
          hasPermission(
            [grantedPermission],
            requestedPermission,
            effectiveProjectionVersion
          )
        ).toBe(requestedPermission === grantedPermission);
      }
    }
  );

  it("fails closed when the effective session has no permissions", () => {
    for (const requestedPermission of allPermissions) {
      expect(
        hasPermission([], requestedPermission, effectiveProjectionVersion)
      ).toBe(false);
    }
  });

  it("accepts every effective capability explicitly projected by the server", () => {
    const permissions = [
      PermissionKey.View,
      PermissionKey.Manager,
      PermissionKey.AdminManager,
      PermissionKey.Export,
    ];

    for (const permission of permissions) {
      expect(
        hasPermission(permissions, permission, effectiveProjectionVersion)
      ).toBe(true);
    }

    expect(
      hasPermission(
        permissions,
        PermissionKey.Admin,
        effectiveProjectionVersion
      )
    ).toBe(false);
    expect(
      hasPermission(
        permissions,
        PermissionKey.PlanarianAdmin,
        effectiveProjectionVersion
      )
    ).toBe(false);
  });
});

describe("legacy API compatibility", () => {
  it("preserves the old account hierarchy when projection metadata is absent", () => {
    expect(hasPermission([PermissionKey.Admin], PermissionKey.View)).toBe(true);
    expect(hasPermission([PermissionKey.Admin], PermissionKey.Manager)).toBe(
      true
    );
    expect(
      hasPermission([PermissionKey.Admin], PermissionKey.AdminManager)
    ).toBe(true);
    expect(hasPermission([PermissionKey.Manager], PermissionKey.View)).toBe(
      true
    );
  });

  it("does not broaden legacy synthetic or platform capabilities", () => {
    expect(
      hasPermission([PermissionKey.AdminManager], PermissionKey.Manager)
    ).toBe(false);
    expect(
      hasPermission([PermissionKey.PlanarianAdmin], PermissionKey.Admin)
    ).toBe(false);
    expect(
      hasPermission([PermissionKey.Export], PermissionKey.View)
    ).toBe(false);
  });

  it("also supports explicit version-one responses", () => {
    expect(
      hasPermission([PermissionKey.Admin], PermissionKey.Manager, 1)
    ).toBe(true);
  });
});
