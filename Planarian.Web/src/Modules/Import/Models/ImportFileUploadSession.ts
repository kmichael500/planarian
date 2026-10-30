import type { ChunkedUploadSessionStatus } from "../../../Shared/Components/FileUploader/types";

export interface ImportFileUploadSession {
  sessionId: string;
  uploadedBytes: number;
  totalBytes: number;
  status: ChunkedUploadSessionStatus;
}

export interface ImportFileRequest {
  fileName: string;
  fileSize: number;
  delimiterRegex: string;
  idRegex: string;
  ignoreDuplicates: boolean;
  requestId?: string | null;
}
