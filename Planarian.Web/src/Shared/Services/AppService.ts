import { HttpClient } from "../..";
import { PermissionKey } from "../../Modules/Authentication/Models/PermissionKey";
import { isNullOrWhiteSpace } from "../Helpers/StringHelpers";
import { SelectListItem } from "../Models/SelectListItem";

const baseUrl = "api/app";

let AppOptions: AppOptionsVm = {
  serverBaseUrl: "",
  signalrBaseUrl: "",
};

let antiforgeryRequestToken: string | null = null;

const AppService = {
  async InitializeApp(
    selectedAccountId?: string | null
  ): Promise<AppInitializeVm> {
    const response = await HttpClient.get<AppInitializeVm>(`${baseUrl}/initialize`, {
      headers: !isNullOrWhiteSpace(selectedAccountId)
        ? { "x-account": selectedAccountId }
        : undefined,
    });

    AppOptions = {
      serverBaseUrl: response.data.serverBaseUrl,
      signalrBaseUrl: response.data.signalrBaseUrl,
    };
    antiforgeryRequestToken = response.data.antiforgeryRequestToken;

    return response.data;
  },
  GetAntiforgeryRequestToken(): string | null {
    return antiforgeryRequestToken;
  },
  async HasCavePermission(
    permissionKey: PermissionKey,
    caveId: string | null = null,
    countyId: string | null = null
  ): Promise<boolean> {
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

export interface AppOptionsVm {
  serverBaseUrl: string;
  signalrBaseUrl: string;
}

export interface AppInitializeVm extends AppOptionsVm {
  accountIds: SelectListItem<string>[];
  permissions: PermissionKey[];
  currentUser: AppInitializeCurrentUserVm | null;
  antiforgeryRequestToken: string | null;
}

export interface AppInitializeCurrentUserVm {
  id: string;
  fullName: string;
  currentAccountId: string | null;
}
