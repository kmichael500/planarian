export interface ImportFileUploadSession {
  sessionId: string;
  uploadedBytes: number;
  totalBytes: number;
  status: string;
}

export interface CreateImportFileUploadSessionRequest {
  fileName: string;
  fileSize: number;
  delimiterRegex: string;
  idRegex: string;
  ignoreDuplicates: boolean;
  requestId?: string | null;
}
