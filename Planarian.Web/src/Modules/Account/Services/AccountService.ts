import { RcFile } from "antd/lib/upload";
import { AxiosProgressEvent, AxiosRequestConfig } from "axios";
import { HttpClient } from "../../..";
import { FileVm } from "../../Files/Models/FileVm";

const baseUrl = "api/account";
const AccountService = {
  async ResetAccount() {
    await HttpClient.delete(`${baseUrl}/reset`);
  },

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
};
export { AccountService };
