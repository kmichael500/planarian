import { HttpClient } from "../../..";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { CacheService } from "../../../Shared/Services/CacheService";
import { NameProfilePhotoVm } from "../../User/Models/NameProfilePhotoVm";

const baseUrl = "api/settings";
const SettingsService = {
  async GetTripTags(): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/tags/trip`
    );
    return response.data;
  },
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

  async GetGeology(): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/tags/geology`
    );
    return response.data;
  },
  async GetLocationQualityTags(): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/tags/location-quality`
    );
    return response.data;
  },
  async GetEntranceStatusTags(): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/tags/entrance-status`
    );
    return response.data;
  },
  async GetFieldIndicationTags(): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/tags/field-indication`
    );
    return response.data;
  },
  async GetEntranceHydrology(): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/tags/entrance-hydrology`
    );
    return response.data;
  },
  async GetFileTags(): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/tags/file`
    );
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
};

export { SettingsService };
