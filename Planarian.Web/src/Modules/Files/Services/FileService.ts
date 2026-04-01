import { HttpClient, baseUrl as apiBaseUrl } from "../../..";
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
    const response = await HttpClient.post<FileAccessTicketVm>(
      `${filesBaseUrl}/${fileId}/access-ticket`
    );

    const ticketizedUrl = new URL(
      `${filesBaseUrl}/${fileId}/${action}`,
      apiBaseUrl
    );
    ticketizedUrl.searchParams.set("ticket", response.data.ticket);

    return ticketizedUrl.toString();
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

export interface FileAccessTicketVm {
  ticket: string;
}
