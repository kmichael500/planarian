import { HttpClient } from "../Http/HttpClient";
import { RequestRuntimeState } from "../Http/RequestRuntimeState";
import { PermissionKey } from "../../Modules/Authentication/Models/PermissionKey";
import { isNullOrWhiteSpace } from "../Helpers/StringHelpers";
import { SelectListItem } from "../Models/SelectListItem";

const baseUrl = "api/app";

let AppOptions: AppOptionsVm = {
  apiBaseUrl: "",
  signalrBaseUrl: "",
  supportName: "",
  supportEmail: "",
};


const AppService = {
  async InitializeApp(
    selectedAccountId?: string | null
  ): Promise<AppInitializeVm> {
    const response = await HttpClient.get<AppInitializeVm>(`${baseUrl}/initialize`, {
      headers: !isNullOrWhiteSpace(selectedAccountId)
        ? { "x-account": selectedAccountId }
        : undefined,
    });

    return response.data;
  },
  ApplyInitializationOptions(options: AppInitializeVm): void {
    AppOptions = {
      apiBaseUrl: options.apiBaseUrl,
      signalrBaseUrl: options.signalrBaseUrl,
      supportName: options.supportName,
      supportEmail: options.supportEmail,
    };
    RequestRuntimeState.setAntiforgeryRequestToken(options.antiforgeryRequestToken);
  },
  GetAntiforgeryRequestToken(): string | null {
    return RequestRuntimeState.getAntiforgeryRequestToken();
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
  apiBaseUrl: string;
  signalrBaseUrl: string;
  supportName: string;
  supportEmail: string;
}

export interface AppInitializeVm extends AppOptionsVm {
  accountIds: SelectListItem<string>[];
  revokedAccountIds?: SelectListItem<string>[];
  permissions: PermissionKey[];
  currentUser: AppInitializeCurrentUserVm | null;
  antiforgeryRequestToken: string | null;
}

export interface AppInitializeCurrentUserVm {
  id: string;
  fullName: string;
  currentAccountId: string | null;
}
