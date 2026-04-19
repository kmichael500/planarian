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
import { ArchiveListItemVm } from "../Models/Archive/ArchiveListItemVm";
import { ArchiveProgressVm } from "../Models/Archive/ArchiveProgressVm";
import { FileAccessUrlVm } from "../../Files/Services/FileService";
import {
  ImportFileRequest,
  ImportFileUploadSession,
} from "../../Import/Models/ImportFileUploadSession";

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

  async CreateArchive(): Promise<void> {
    await HttpClient.post(`${baseUrl}/archive`, {});
  },

  async CancelArchive(): Promise<void> {
    await HttpClient.post(`${baseUrl}/archive/cancel`, {});
  },

  async GetArchiveStatus(): Promise<ArchiveProgressVm | null> {
    const response = await HttpClient.get<ArchiveProgressVm | null>(
      `${baseUrl}/archive/status`
    );
    return response.data;
  },

  async GetRecentArchives(): Promise<ArchiveListItemVm[]> {
    const response = await HttpClient.get<ArchiveListItemVm[]>(
      `${baseUrl}/archive/list`
    );
    return response.data;
  },

  async StartArchiveDownload(blobKey: string): Promise<void> {
    const response = await HttpClient.post<FileAccessUrlVm>(
      `${baseUrl}/archive/download?blobKey=${encodeURIComponent(blobKey)}`,
      {}
    );
    window.location.assign(response.data.url);
  },

  async DeleteArchive(blobKey: string): Promise<void> {
    await HttpClient.delete(`${baseUrl}/archive?blobKey=${encodeURIComponent(blobKey)}`);
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
    isDryRun = false,
    syncExisting = false
  ): Promise<CaveDryRunRecord[]> {
    const response = await HttpClient.post<CaveDryRunRecord[]>(
      `${baseUrl}/import/caves/process/${fileId}?isDryRun=${isDryRun}&syncExisting=${syncExisting}`
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
    isDryRun: boolean,
    syncExisting = false
  ): Promise<EntranceDryRun[]> {
    const response = await HttpClient.post<EntranceDryRun[]>(
      `${baseUrl}/import/entrances/process/${fileId}?isDryRun=${isDryRun}&syncExisting=${syncExisting}`
    );
    return response.data;
  },

  async CreateImportFileUploadSession(
    request: ImportFileRequest
  ): Promise<ImportFileUploadSession> {
    const response = await HttpClient.post<ImportFileUploadSession>(
      `${baseUrl}/import/file/session`,
      request
    );
    return response.data;
  },
  async UploadImportFileChunk(
    sessionId: string,
    chunk: Blob,
    chunkIndex: number,
    offset: number,
    onProgress: (progressEvent: AxiosProgressEvent) => void,
    signal?: AbortSignal
  ): Promise<ImportFileUploadSession> {
    const response = await HttpClient.put<ImportFileUploadSession>(
      `${baseUrl}/import/file/session/${sessionId}?chunkIndex=${chunkIndex}&offset=${offset}`,
      chunk,
      {
        headers: {
          "Content-Type": "application/octet-stream",
        },
        onUploadProgress: onProgress,
        signal,
      }
    );
    return response.data;
  },
  async FinalizeImportFileUploadSession(
    sessionId: string,
    signal?: AbortSignal
  ): Promise<FileImportResult> {
    const response = await HttpClient.post<FileImportResult>(
      `${baseUrl}/import/file/session/${sessionId}/finalize`,
      {},
      {
        signal,
      }
    );
    return response.data;
  },
  async CancelImportFileUploadSession(sessionId: string): Promise<void> {
    await HttpClient.delete(`${baseUrl}/import/file/session/${sessionId}`);
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
