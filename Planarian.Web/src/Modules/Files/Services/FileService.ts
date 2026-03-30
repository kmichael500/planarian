import { HttpClient } from "../../..";
import { EditFileMetadataVm } from "../Models/EditFileMetadataVm";

const baseUrl = "api/files";
const FileService = {
  async UpdateFilesMetadata(values: EditFileMetadataVm[]): Promise<void> {
    await HttpClient.put<string>(`${baseUrl}/multiple`, values);
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
