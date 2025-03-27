import { HttpClient } from "../..";
import { PermissionKey } from "../../Modules/Authentication/Models/PermissionKey";
import { isNullOrWhiteSpace } from "../Helpers/StringHelpers";
import { SelectListItem } from "../Models/SelectListItem";

const baseUrl = "api/app";
let AppOptions: AppInitializeVm;
const AppService = {
  async InitializeApp(): Promise<void> {
    const response = await HttpClient.get<AppInitializeVm>(
      `${baseUrl}/initialize`
    );
    AppOptions = response.data;
    console.log("init");
  },
  HasPermission(permission: PermissionKey): boolean {
    if (!AppOptions.permissions) {
      return false;
    }
    if (permission === PermissionKey.View) {
      return (
        AppOptions.permissions.includes(PermissionKey.View) ||
        AppOptions.permissions.includes(PermissionKey.Manager) ||
        AppOptions.permissions.includes(PermissionKey.Admin) ||
        AppOptions.permissions.includes(PermissionKey.PlanarianAdmin)
      );
    }

    if (permission === PermissionKey.Manager) {
      return (
        AppOptions.permissions.includes(PermissionKey.Manager) ||
        AppOptions.permissions.includes(PermissionKey.Admin) ||
        AppOptions.permissions.includes(PermissionKey.PlanarianAdmin)
      );
    }

    if (permission === PermissionKey.AdminManager) {
      return (
        AppOptions.permissions.includes(PermissionKey.AdminManager) ||
        AppOptions.permissions.includes(PermissionKey.Admin) ||
        AppOptions.permissions.includes(PermissionKey.PlanarianAdmin)
      );
    }

    if (permission === PermissionKey.Admin) {
      return (
        AppOptions.permissions.includes(PermissionKey.Admin) ||
        AppOptions.permissions.includes(PermissionKey.PlanarianAdmin)
      );
    }

    if (permission === PermissionKey.PlanarianAdmin) {
      return AppOptions.permissions.includes(PermissionKey.PlanarianAdmin);
    }

    return AppOptions.permissions.includes(permission);
  },
  async HasCavePermission(
    permissionKey: PermissionKey,
    caveId: string | null = null,
    countyId: string | null = null
  ): Promise<boolean> {
    if (!this.HasPermission(permissionKey)) {
      return false;
    }
    const params = new URLSearchParams();
    if (!isNullOrWhiteSpace(caveId)) {
      params.append("caveId", caveId);
    }
    if (!isNullOrWhiteSpace(countyId)) {
      params.append("countyId", countyId);
    }
    if (permissionKey) {
      params.append("permissionKey", permissionKey);
    }
    const queryString = params.toString() ? `?${params.toString()}` : "";

    const response = await HttpClient.get<boolean>(
      `${baseUrl}/permissions/caves${queryString}`
    );
    return response.data;
  },
};
export { AppService, AppOptions };

export interface AppInitializeVm {
  serverBaseUrl: string;
  signalrBaseUrl: string;
  accountIds: SelectListItem<string>[];
  permissions: PermissionKey[];
}
