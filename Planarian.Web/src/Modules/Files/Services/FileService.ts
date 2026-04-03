import { HttpClient } from "../../..";
import { EditFileMetadataVm } from "../Models/EditFileMetadataVm";

const filesBaseUrl = "api/files";
export enum FileAccessAction {
  View = "view",
  Download = "download",
}

const FileService = {
  async UpdateFilesMetadata(values: EditFileMetadataVm[]): Promise<void> {
    await HttpClient.put<string>(`${filesBaseUrl}/multiple`, values);
  },

  async createFileSasLink(
    fileId: string,
    action: FileAccessAction
  ): Promise<string> {
    const response = await HttpClient.post<FileAccessUrlVm>(
      `${filesBaseUrl}/${fileId}/${action}`
    );
    return response.data.url;
  },

  async startFileDownload(fileId: string): Promise<void> {
    const accessUrl = await this.createFileSasLink(
      fileId,
      FileAccessAction.Download
    );
    window.location.assign(accessUrl);
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

export interface FileAccessUrlVm {
  url: string;
}
