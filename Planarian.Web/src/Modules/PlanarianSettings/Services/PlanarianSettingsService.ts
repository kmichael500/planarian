import { HttpClient } from "../../..";
import { CreateAccountVm } from "../Models/CreateAccountVm";

const baseUrl = "api/planarian-settings";

const PlanarianSettingsService = {
  async CreateAccount(account: CreateAccountVm): Promise<string> {
    const response = await HttpClient.post<string>(
      `${baseUrl}/accounts`,
      account
    );
    return response.data;
  },
};

export { PlanarianSettingsService };
