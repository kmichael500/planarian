import { HttpClient } from "../../..";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { NameProfilePhotoVm } from "../../User/Models/NameProfilePhotoVm";

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
  async GetUsersName(userId: string): Promise<NameProfilePhotoVm> {
    const response = await HttpClient.get<NameProfilePhotoVm>(
      `${baseUrl}/users/${userId}`
    );
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
