export interface FileImportResult {
  fileName: string;
  isSuccessful: boolean;
  status?: string;
  associatedCave: string;
  message: string;
  failureCode?: string | null;
  requestId?: string | null;
}
