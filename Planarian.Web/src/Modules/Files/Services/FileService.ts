import { RcFile } from "antd/es/upload";
import { AxiosProgressEvent, AxiosRequestConfig } from "axios";
import { HttpClient } from "../../..";
import { FileVm } from "../Models/FileVm";

const baseUrl = "api/files";
const FileService = {
  async UpdateFilesMetadata(values: FileVm[]): Promise<void> {
    await HttpClient.put<string>(`${baseUrl}/multiple`, values);
  },
  async GetFile(id: string): Promise<void> {
    await HttpClient.get<FileVm>(`${baseUrl}/${id}`);
  },
  async AddTemporaryFile(
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
      onUploadProgress: onProgress,
    };

    const response = await HttpClient.post<FileVm>(
      `${baseUrl}/temporary?uuid=${uuid}`,
      formData,
      config
    );
    return response.data;
  },
};
export { FileService };

export interface UploadedFileResponse {
  id: string;
  fileUrl: string;
}

export interface FileInformation {
  FileTypeKey: string;
  CaveId?: string;
  DisplayName?: string;
}
