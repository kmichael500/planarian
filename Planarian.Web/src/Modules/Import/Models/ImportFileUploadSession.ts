export interface ImportFileUploadSession {
  sessionId: string;
  uploadedBytes: number;
  totalBytes: number;
  status: string;
}

export interface ImportFileRequest {
  fileName: string;
  fileSize: number;
  delimiterRegex: string;
  idRegex: string;
  ignoreDuplicates: boolean;
  requestId?: string | null;
}
