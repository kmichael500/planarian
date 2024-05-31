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
  async ImportCavesFileProcess(fileId: string): Promise<void> {
    const response = await HttpClient.post<void>(
      `${baseUrl}/import/caves/process/${fileId}`
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
  async ImportEntrancesProcess(fileId: string): Promise<void> {
    const response = await HttpClient.post<void>(
      `${baseUrl}/import/entrances/process/${fileId}`
    );
    return response.data;
  },

  async ImportFile(
    file: string | Blob | RcFile,
    uuid: string,
    delmiterRegex: string,
    countyCodeRegex: string,
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

    const regexQueryStringUrlSafe = `delimiterRegex=${encodeURIComponent(
      delmiterRegex
    )}&countyCodeRegex=${countyCodeRegex}`;

    const response = await HttpClient.post<FileVm>(
      `${baseUrl}/import/file?uuid=${uuid}&${regexQueryStringUrlSafe}`,
      formData,
      config
    );
    return response.data;
  },
  //#endregion

  async GetFeatureSettings() {
    const response = await HttpClient.get<FeatureSettingVm[]>(
      `${baseUrl}/feature-settings`
    );
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
