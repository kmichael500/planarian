import { HttpHelpers } from "../../../Shared/Helpers/HttpHelpers";

const filesBaseUrl = "api/files";
export enum FileAccessAction {
  View = "view",
  Download = "download",
}

const FileService = {
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
  Name?: string;
  Extension?: string;
}
