export interface FileImportResult {
  fileName: string;
  isSuccessful: boolean;
  associatedCave: string;
  message: string;
  failureCode?: string | null;
  isRetryable?: boolean;
  requestId?: string | null;
}
