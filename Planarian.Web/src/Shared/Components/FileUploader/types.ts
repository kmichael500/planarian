import { AxiosProgressEvent } from "axios";
import { ReactNode } from "react";

export type QueuedFileUploadStatus =
  | "queued"
  | "uploading"
  | "uploaded"
  | "skipped"
  | "failed"
  | "canceled";

export interface ChunkedUploadSession {
  sessionId: string;
  uploadedBytes: number;
  totalBytes: number;
  status?: string;
}

export interface ChunkedUploaderConfig {
  maxConcurrentUploads: number;
  maxFileSizeBytes: number;
  chunkSizeBytes: number;
}

export interface QueuedFileUploadFailureDetails {
  message: string;
  failureCode?: string | null;
  requestId?: string | null;
  terminalStatus?: "failed" | "skipped";
}

export type QueuedFileValidationResult =
  | boolean
  | {
      message: string;
      failureCode?: string | null;
      terminalStatus?: "failed";
    };

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
  cancelAllSessions?: () => Promise<void>;
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
  lastError?: string | null;
  failureCode?: string | null;
  requestId?: string | null;
  completedAt?: string | null;
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
  skipped: number;
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
  onEditSettings?: () => void;
  hasResults?: boolean;
  renderRecentActivityTooltip?: (
    item: QueuedFileUploadItem<TResult>
  ) => ReactNode;
}

export interface UseQueuedChunkedFileUploaderOptions<TResult> {
  storageKey: string | null;
  endpoints: QueuedFileUploadEndpoints<TResult>;
  mapFailure?: (
    failure: QueuedFileUploadFailureDetails,
    error: unknown
  ) => QueuedFileUploadFailureDetails;
  validateFile?: (file: File) => QueuedFileValidationResult;
  onCompleted?: (items: QueuedFileUploadItem<TResult>[]) => void;
  pauseOnFailures?: boolean;
  dispatchDelayMs?: number;
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
  isLoadingConfig: boolean;
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
  retryQueueItem: (itemId: string) => void;
  resetQueueState: () => Promise<void>;
  removeQueueItem: (itemId: string) => void;
}
