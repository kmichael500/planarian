import { HttpClient } from "../..";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";
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
  },
};
export { AppService, AppOptions };

export interface AppInitializeVm {
  serverBaseUrl: string;
  signalrBaseUrl: string;
  accountIds: SelectListItem<string>[];
}
