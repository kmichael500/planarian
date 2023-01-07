import { HttpClient } from "../..";
import { SelectListItem } from "../Models/SelectListItem";

const baseUrl = "api/settings";
const SettingsService = {
  async GetTripObjectiveTypes(): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/objectiveTypes`
    );
    return response.data;
  },
  async GetObjectiveTypeName(objectiveTypeId: string): Promise<string> {
    const response = await HttpClient.get<string>(
      `${baseUrl}/objectiveTypes/${objectiveTypeId}`
    );
    return response.data;
  },
  async GetUsersName(userId: string): Promise<string> {
    const response = await HttpClient.get<string>(`${baseUrl}/users/${userId}`);
    return response.data;
  },

  async GetUsers(): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/users`
    );
    return response.data;
  },
};

export { SettingsService };
