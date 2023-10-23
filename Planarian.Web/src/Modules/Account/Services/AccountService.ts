import { RcFile } from "antd/lib/upload";
import { AxiosProgressEvent, AxiosRequestConfig } from "axios";
import { HttpClient } from "../../..";
import { FileVm } from "../../Files/Models/FileVm";
import { TagType } from "../../Tag/Models/TagType";
import { TagTypeTableVm } from "../Components/TagTypeEditComponent";
import { CreateEditTagTypeVm } from "../Components/CreateEditTagTypeVm";

const baseUrl = "api/account";
const AccountService = {
  async ResetAccount() {
    await HttpClient.delete(`${baseUrl}/reset`);
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
      `${baseUrl}/import/file?uuid=${uuid}`,
      formData,
      config
    );
    return response.data;
  },
  //#endregion

  //#region Tags

  async GetTagsForTable(tagType: TagType): Promise<TagTypeTableVm[]> {
    const response = await HttpClient.get<TagTypeTableVm[]>(
      `${baseUrl}/tags-table/${tagType}`
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
