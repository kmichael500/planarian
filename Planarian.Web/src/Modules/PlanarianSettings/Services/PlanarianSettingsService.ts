import { HttpClient } from "../../../Shared/Http/HttpClient";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { CreateAccountVm } from "../Models/CreateAccountVm";

const baseUrl = "api/planarian-settings";

const PlanarianSettingsService = {
  async GetAllStates(): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/states`
    );
    return response.data;
  },

  async CreateAccount(account: CreateAccountVm): Promise<string> {
    const response = await HttpClient.post<string>(
      `${baseUrl}/accounts`,
      account
    );
    return response.data;
  },
};

export { PlanarianSettingsService };
