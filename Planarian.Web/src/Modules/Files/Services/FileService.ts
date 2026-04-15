import { EditFileMetadataVm } from "../Models/EditFileMetadataVm";
import { HttpClient } from "../../..";
import { HttpHelpers } from "../../../Shared/Helpers/HttpHelpers";

const filesBaseUrl = "api/files";
export enum FileAccessAction {
  View = "view",
  Download = "download",
}

const FileService = {
  async UpdateFilesMetadata(values: EditFileMetadataVm[]): Promise<void> {
    await HttpClient.put<string>(`${filesBaseUrl}/multiple`, values);
  },

  getFileAccessUrl(fileId: string, action: FileAccessAction): string {
    return HttpHelpers.BuildAuthenticatedApiUrl(
      `${filesBaseUrl}/${fileId}/${action}`
    );
  },

  startFileDownload(fileId: string): void {
    HttpHelpers.NavigateToApiUrl(
      `${filesBaseUrl}/${fileId}/${FileAccessAction.Download}`
    );
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
