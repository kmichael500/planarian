import { RcFile } from "antd/lib/upload";
import { HttpClient } from "../../..";

const baseUrl = "api/files";
const FileService = {};
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
