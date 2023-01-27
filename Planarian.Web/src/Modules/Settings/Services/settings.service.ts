import { HttpClient } from "../../..";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { NameProfilePhotoVm } from "../../User/Models/NameProfilePhotoVm";

const baseUrl = "api/settings";
const SettingsService = {
  async GetTripTags(): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/tags/trip`
    );
    return response.data;
  },
  async GetTagName(tagId: string): Promise<string> {
    const response = await HttpClient.get<string>(`${baseUrl}/tags/${tagId}`);
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
