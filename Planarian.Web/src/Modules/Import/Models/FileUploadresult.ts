export type FileImportResultStatus = "Uploaded" | "Skipped" | "Canceled" | "Failed";

export interface FileImportResult {
  fileName: string;
  isSuccessful: boolean;
  status?: FileImportResultStatus;
  associatedCave: string;
  message: string;
  failureCode?: string | null;
  requestId?: string | null;
}
