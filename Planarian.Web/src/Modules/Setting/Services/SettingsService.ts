import { HttpClient } from "../../..";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { CacheService } from "../../../Shared/Services/CacheService";
import { TagType } from "../../Tag/Models/TagType";
import { NameProfilePhotoVm } from "../../User/Models/NameProfilePhotoVm";

const baseUrl = "api/settings";
const SettingsService = {
  async GetStates(): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/tags/states`
    );
    return response.data;
  },
  async GetStateName(stateId?: string): Promise<string> {
    const cacheKey = `state-name-${stateId}`;
    const cachedDisplayValue = CacheService.get<string>(cacheKey);
    if (cachedDisplayValue) {
      return cachedDisplayValue;
    }

    const response = await HttpClient.get<string>(
      `${baseUrl}/tags/states/${stateId}`
    );
    CacheService.set(cacheKey, response.data);
    return response.data;
  },
  async GetCounties(stateId: string): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/tags/states/${stateId}/counties/`
    );
    return response.data;
  },
  async GetCountyName(countyId?: string): Promise<string> {
    const cacheKey = `county-name-${countyId}`;
    const cachedDisplayValue = CacheService.get<string>(cacheKey);
    if (cachedDisplayValue) {
      return cachedDisplayValue;
    }
    const response = await HttpClient.get<string>(
      `${baseUrl}/tags/counties/${countyId}`
    );
    CacheService.set(cacheKey, response.data);
    return response.data;
  },
  async GetTagName(tagId: string): Promise<string> {
    const cacheKey = `tag-name-${tagId}`;
    const cachedDisplayValue = CacheService.get<string>(cacheKey);
    if (cachedDisplayValue) {
      return cachedDisplayValue;
    }
    const response = await HttpClient.get<string>(`${baseUrl}/tags/${tagId}`);
    CacheService.set(cacheKey, response.data);
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
  async GetTags(
    key: TagType,
    projectId: string | null = null
  ): Promise<SelectListItem<string>[]> {
    const queryString = !isNullOrWhiteSpace(projectId)
      ? `?key=${key}&projectId=${projectId}`
      : `?key=${key}`;
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/tags${queryString}`
    );
    return response.data;
  },
};

export { SettingsService };
