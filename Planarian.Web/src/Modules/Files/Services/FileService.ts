import { HttpClient } from "../../..";
import { EditFileMetadataVm } from "../Models/EditFileMetadataVm";
import { FileVm } from "../Models/FileVm";

const baseUrl = "api/files";
const FileService = {
  async UpdateFilesMetadata(values: EditFileMetadataVm[]): Promise<void> {
    await HttpClient.put<string>(`${baseUrl}/multiple`, values);
  },
  async GetFile(id: string): Promise<void> {
    await HttpClient.get<FileVm>(`${baseUrl}/${id}`);
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
