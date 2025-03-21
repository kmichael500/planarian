import { RcFile } from "antd/lib/upload";
import { AxiosProgressEvent, AxiosRequestConfig } from "axios";
import { HttpClient } from "../../..";
import { FileVm } from "../../Files/Models/FileVm";
import { TagType } from "../../Tag/Models/TagType";
import { TagTypeTableVm } from "../Models/TagTypeTableVm";
import { CreateEditTagTypeVm } from "../Models/CreateEditTagTypeVm";
import { TagTypeTableCountyVm } from "../Models/TagTypeTableCountyVm";
import { CreateCountyVm } from "../Models/CreateEditCountyVm";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { MiscAccountSettingsVm } from "../Components/MiscAccountSettingsComponent";
import { FeatureKey, FeatureSettingVm } from "../Models/FeatureSettingVm";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { CacheService } from "../../../Shared/Services/CacheService";
import { CaveDryRunRecord } from "../../Import/Models/CaveDryRunRecord";
import { EntranceDryRun } from "../../Import/Models/EntranceDryRun";
import { FileImportResult } from "../../Import/Models/FileUploadresult";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";

const baseUrl = "api/account";
const AccountService = {
  async ResetAccount() {
    await HttpClient.delete(`${baseUrl}/reset`);
  },

  //#region Settings

  async GetSettings() {
    const response = await HttpClient.get<MiscAccountSettingsVm>(
      `${baseUrl}/settings`
    );
    return response.data;
  },
  async UpdateSettings(settings: MiscAccountSettingsVm) {
    const response = await HttpClient.put<MiscAccountSettingsVm>(
      `${baseUrl}/settings`,
      settings
    );
    return response.data;
  },

  //#region Import
  async ImportCavesFile(
    file: string | Blob | RcFile,
    uuid: string,
    onProgress: (progressEvent: AxiosProgressEvent) => void
  ): Promise<FileVm> {
    const formData = new FormData();
    formData.append("file", file);

    const config: AxiosRequestConfig = {
      headers: {
        "Content-Type": "multipart/form-data",
      },
      onUploadProgress: onProgress, // Set the onUploadProgress callback
    };

    const response = await HttpClient.post<FileVm>(
      `${baseUrl}/import/caves/file?uuid=${uuid}`,
      formData,
      config
    );
    return response.data;
  },
  async ImportCavesFileProcess(
    fileId: string,
    isDryRun = false
  ): Promise<CaveDryRunRecord[]> {
    const response = await HttpClient.post<CaveDryRunRecord[]>(
      `${baseUrl}/import/caves/process/${fileId}?isDryRun=${isDryRun}`
    );
    return response.data;
  },
  async ImportEntrancesFile(
    file: string | Blob | RcFile,
    uuid: string,
    onProgress: (progressEvent: AxiosProgressEvent) => void
  ): Promise<FileVm> {
    const formData = new FormData();
    formData.append("file", file);

    const config: AxiosRequestConfig = {
      headers: {
        "Content-Type": "multipart/form-data",
      },
      onUploadProgress: onProgress, // Set the onUploadProgress callback
    };

    const response = await HttpClient.post<FileVm>(
      `${baseUrl}/import/entrances/file?uuid=${uuid}`,
      formData,
      config
    );
    return response.data;
  },
  async ImportEntrancesProcess(
    fileId: string,
    isDryRun: boolean
  ): Promise<EntranceDryRun[]> {
    const response = await HttpClient.post<EntranceDryRun[]>(
      `${baseUrl}/import/entrances/process/${fileId}?isDryRun=${isDryRun}`
    );
    return response.data;
  },

  async ImportFile(
    file: string | Blob | RcFile,
    uuid: string,
    delmiterRegex: string,
    idRegex: string,
    ignoreDuplicates: boolean = false,
    onProgress: (progressEvent: AxiosProgressEvent) => void
  ): Promise<FileImportResult> {
    const formData = new FormData();
    formData.append("file", file);

    const config: AxiosRequestConfig = {
      headers: {
        "Content-Type": "multipart/form-data",
      },
      onUploadProgress: onProgress, // Set the onUploadProgress callback
    };

    let regexQueryStringUrlSafe = `delimiterRegex=${encodeURIComponent(
      delmiterRegex
    )}&idRegex=${encodeURIComponent(
      idRegex
    )}&ignoreDuplicates=${ignoreDuplicates}`;

    if (!isNullOrWhiteSpace(uuid)) {
      regexQueryStringUrlSafe += `&uuid=${encodeURIComponent(uuid)}`;
    }

    const response = await HttpClient.post<FileImportResult>(
      `${baseUrl}/import/file?${regexQueryStringUrlSafe}`,
      formData,
      config
    );
    return response.data;
  },
  //#endregion

  async GetFeatureSettings(resetCache: boolean = false) {
    const cacheKey = `${AuthenticationService.GetAccountId}-featureSettings`;

    const cachedData = CacheService.get<FeatureSettingVm[]>(cacheKey);
    if (!resetCache && cachedData) {
      return cachedData;
    }

    const response = await HttpClient.get<FeatureSettingVm[]>(
      `${baseUrl}/feature-settings`
    );

    CacheService.set(cacheKey, response.data);

    return response.data;
  },

  async CreateOrUpdateFeatureSetting(key: FeatureKey, isEnabled: boolean) {
    const response = await HttpClient.post<FeatureSettingVm[]>(
      `${baseUrl}/feature-settings/${key}?isEnabled=${isEnabled}`,
      {}
    );
    return response.data;
  },

  //#region Tags
  async GetAllStates(): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/states`
    );
    return response.data;
  },
  async GetCountiesForTable(stateId: string): Promise<TagTypeTableCountyVm[]> {
    const response = await HttpClient.get<TagTypeTableCountyVm[]>(
      `${baseUrl}/counties-table/${stateId}`
    );
    return response.data;
  },
  async AddCounty(
    tag: CreateCountyVm,
    stateId: string
  ): Promise<TagTypeTableCountyVm> {
    const response = await HttpClient.post<TagTypeTableCountyVm>(
      `${baseUrl}/states/${stateId}/counties`,
      tag
    );
    return response.data;
  },
  async GetTagsForTable(tagType: TagType): Promise<TagTypeTableVm[]> {
    const response = await HttpClient.get<TagTypeTableVm[]>(
      `${baseUrl}/tags-table/${tagType}`
    );
    return response.data;
  },
  async UpdateCounties(
    countyId: string,
    stateId: string,
    tag: TagTypeTableCountyVm
  ): Promise<TagTypeTableCountyVm> {
    const response = await HttpClient.put<TagTypeTableCountyVm>(
      `${baseUrl}/states/${stateId}/counties/${countyId}`,
      tag
    );
    return response.data;
  },
  async DeleteCounties(countyIds: string[], stateId: string): Promise<void> {
    const parameter = "countyIds";
    let queryString = `${parameter}=${countyIds.join(",")}`;

    const response = await HttpClient.delete<void>(
      `${baseUrl}/states/${stateId}/counties?${queryString}`
    );
    return response.data;
  },

  async AddTagType(tag: CreateEditTagTypeVm): Promise<TagTypeTableVm> {
    const response = await HttpClient.post<TagTypeTableVm>(
      `${baseUrl}/tags/`,
      tag
    );
    return response.data;
  },
  async UpdateTagTypes(
    tagTypeId: string,
    tag: CreateEditTagTypeVm
  ): Promise<TagTypeTableVm> {
    const response = await HttpClient.put<TagTypeTableVm>(
      `${baseUrl}/tags/${tagTypeId}`,
      tag
    );
    return response.data;
  },
  async DeleteTagTypes(tagTypeIds: string[]): Promise<void> {
    const parameter = "tagTypeIds";
    let queryString = `${parameter}=${tagTypeIds.join(",")}`;

    const response = await HttpClient.delete<void>(
      `${baseUrl}/tags?${queryString}`
    );
    return response.data;
  },
  async MergeTagTypes(
    sourceTagTypeIds: string[],
    destinationTagTypeId: string
  ) {
    const parameter = "sourceTagTypeIds";
    let queryString = `${parameter}=${sourceTagTypeIds.join(",")}`;

    const response = await HttpClient.post<void>(
      `${baseUrl}/tags/merge/${destinationTagTypeId}?${queryString}`
    );
    return response.data;
  },

  //#endregion
};
export { AccountService };
