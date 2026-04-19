import { AxiosProgressEvent } from "axios";
import { ReactNode } from "react";

export type QueuedFileUploadStatus =
  | "queued"
  | "uploading"
  | "uploaded"
  | "failed"
  | "retry_wait"
  | "canceled";

export interface ChunkedUploadSession {
  sessionId: string;
  uploadedBytes: number;
  totalBytes: number;
  status?: string;
}

export interface QueuedFileUploadFailureDetails {
  message: string;
  failureCode?: string | null;
  isRetryable: boolean;
  retryAfterSeconds?: number;
  requestId?: string | null;
}

export interface QueuedFileUploadEndpoints<TResult> {
  createSession: (
    file: File,
    requestId: string,
    signal?: AbortSignal
  ) => Promise<ChunkedUploadSession>;
  uploadChunk: (
    sessionId: string,
    chunk: Blob,
    chunkIndex: number,
    offset: number,
    onProgress: (progressEvent: AxiosProgressEvent) => void,
    signal?: AbortSignal
  ) => Promise<ChunkedUploadSession>;
  finalizeSession: (
    sessionId: string,
    signal?: AbortSignal
  ) => Promise<TResult>;
  cancelSession: (sessionId: string) => Promise<void>;
}

export interface QueuedFileUploadItem<TResult> {
  id: string;
  queueOrder: number;
  fileName: string;
  size: number;
  type: string;
  lastModified: number;
  addedOn: string;
  status: QueuedFileUploadStatus;
  progress: number;
  retryCount: number;
  retryAt?: number | null;
  lastError?: string | null;
  failureCode?: string | null;
  isRetryable: boolean;
  requestId?: string | null;
  result?: TResult | null;
  sessionId?: string | null;
  uploadedBytes?: number;
  liveUploadedBytes?: number;
  displayUploadedBytes?: number;
  totalBytes?: number;
  isCurrentUploadSlot?: boolean;
  transportStatus?: "chunk_uploading" | "finalizing" | null;
  file?: File | null;
}

export interface QueuedFileUploadStats {
  uploaded: number;
  failed: number;
  canceled: number;
  uploading: number;
  queued: number;
  remaining: number;
}

export interface QueuedFileUploaderCopy {
  addFilesLabel?: string;
  startLabel?: string;
  queueTitle?: string;
  recentActivityTitle?: string;
  emptyQueueText?: string;
  emptyRecentActivityText?: string;
}

export interface QueuedFileUploaderProps<TResult> {
  queue: UseQueuedChunkedFileUploaderResult<TResult>;
  copy?: QueuedFileUploaderCopy;
  onViewResults?: () => void;
  hasResults?: boolean;
  renderRecentActivityTooltip?: (
    item: QueuedFileUploadItem<TResult>
  ) => ReactNode;
}

export interface UseQueuedChunkedFileUploaderOptions<TResult> {
  storageKey: string | null;
  endpoints: QueuedFileUploadEndpoints<TResult>;
  normalizeError: (error: unknown) => QueuedFileUploadFailureDetails;
  isAbortError: (error: unknown) => boolean;
  validateFile?: (file: File) => boolean;
  onCompleted?: (items: QueuedFileUploadItem<TResult>[]) => void;
  maxRetryCount?: number;
  chunkSize?: number;
  maxConcurrencyWeight?: number;
  dispatchDelayMs?: number;
  smallFileConcurrencyThresholdMb?: number;
  largeFileConcurrencyThresholdMb?: number;
  filePreparationBatchSize?: number;
}

export interface UseQueuedChunkedFileUploaderResult<TResult> {
  queueItems: QueuedFileUploadItem<TResult>[];
  queueStats: QueuedFileUploadStats;
  uploadQueueItems: QueuedFileUploadItem<TResult>[];
  failedItems: QueuedFileUploadItem<TResult>[];
  recentActivity: QueuedFileUploadItem<TResult>[];
  aggregateProgress: number;
  fileResults: QueuedFileUploadItem<TResult>[];
  isPaused: boolean;
  hasStartedUploadRun: boolean;
  isRestoring: boolean;
  isResettingQueue: boolean;
  isDragActive: boolean;
  uploadControl: {
    label: string;
    icon: ReactNode;
    disabled: boolean;
    onClick: () => void;
  };
  addFilesToQueue: (fileList: FileList) => Promise<void>;
  handleFileSelect: (event: React.ChangeEvent<HTMLInputElement>) => Promise<void>;
  handleDrop: (event: React.DragEvent<HTMLElement>) => Promise<void>;
  handleDragOver: (event: React.DragEvent<HTMLElement>) => void;
  handleDragEnter: (event: React.DragEvent<HTMLElement>) => void;
  handleDragLeave: (event: React.DragEvent<HTMLElement>) => void;
  retryFailed: () => void;
  resetQueueState: () => Promise<void>;
  removeQueueItem: (itemId: string) => void;
}
