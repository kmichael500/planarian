export interface ProgressVm {
  statusMessage: string;
  processedCount?: number;
  totalCount?: number;
  message?: string;
  isError?: boolean;
  isCanceled?: boolean;
}
