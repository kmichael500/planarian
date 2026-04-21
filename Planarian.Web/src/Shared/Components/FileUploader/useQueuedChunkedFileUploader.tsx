import {
  PauseCircleOutlined,
  PlayCircleOutlined,
} from "@ant-design/icons";
import axios from "axios";
import { message } from "antd";
import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { SettingsService } from "../../../Modules/Setting/Services/SettingsService";
import { ApiErrorResponse } from "../../Models/ApiErrorResponse";
import { QueuedFileStorage } from "../../Services/QueuedFileStorage";
import {
  createQueueItemId,
  deriveOrderedQueueView,
  formatFileSize,
  getOrderedPendingQueueItems,
  getPositiveNumber,
  getSafeTotalBytes,
  getSmoothedDisplayBytes,
  getUploadDisplayState,
  isTerminalStatus,
  normalizeQueueItem,
  toProgressPercent,
  waitForNextPaint,
} from "./fileUploaderHelpers";
import {
  ChunkedUploaderConfig,
  QueuedFileUploadFailureDetails,
  QueuedFileUploadItem,
  QueuedFileValidationResult,
  UseQueuedChunkedFileUploaderOptions,
  UseQueuedChunkedFileUploaderResult,
} from "./types";

const DEFAULT_DISPATCH_DELAY_MS = 0;
const FILE_STORAGE_BATCH_SIZE = 100;
const MAX_CONSECUTIVE_UPLOAD_FAILURES = 5;
const PERSISTED_QUEUE_VERSION = 1;

const isValidationFailure = (
  result: QueuedFileValidationResult
): result is Exclude<QueuedFileValidationResult, boolean> =>
  typeof result === "object" && result !== null;

interface PersistedQueueItem<TResult>
  extends Omit<
    QueuedFileUploadItem<TResult>,
    | "file"
    | "sessionId"
    | "uploadedBytes"
    | "liveUploadedBytes"
    | "displayUploadedBytes"
    | "totalBytes"
    | "isCurrentUploadSlot"
  > {}

interface PersistedQueueState<TResult> {
  version: number;
  isPaused: boolean;
  hasStartedUploadRun?: boolean;
  items: PersistedQueueItem<TResult>[];
}

const serializeQueueItem = <TResult,>(
  item: QueuedFileUploadItem<TResult>
): PersistedQueueItem<TResult> => ({
  id: item.id,
  queueOrder: item.queueOrder,
  fileName: item.fileName,
  size: item.size,
  type: item.type,
  lastModified: item.lastModified,
  addedOn: item.addedOn,
  status: item.status,
  progress: getUploadDisplayState(item).displayPercent,
  retryCount: item.retryCount,
  lastError: item.lastError ?? null,
  failureCode: item.failureCode ?? null,
  requestId: item.requestId ?? null,
  completedAt: item.completedAt ?? null,
  result: item.result ?? null,
  transportStatus: item.transportStatus ?? null,
});

export const useQueuedChunkedFileUploader = <TResult,>({
  storageKey,
  endpoints,
  mapFailure,
  validateFile,
  onCompleted,
  pauseOnFailures = true,
  dispatchDelayMs = DEFAULT_DISPATCH_DELAY_MS,
}: UseQueuedChunkedFileUploaderOptions<TResult>): UseQueuedChunkedFileUploaderResult<TResult> => {
  const [queueItems, setQueueItems] = useState<QueuedFileUploadItem<TResult>[]>([]);
  const [uploaderConfig, setUploaderConfig] = useState<ChunkedUploaderConfig | null>(
    null
  );
  const [isLoadingConfig, setIsLoadingConfig] = useState(true);
  const [isPaused, setIsPaused] = useState(true);
  const [hasStartedUploadRun, setHasStartedUploadRun] = useState(false);
  const [isRestoring, setIsRestoring] = useState(false);
  const [hydratedStorageKey, setHydratedStorageKey] = useState<string | null>(
    null
  );
  const [isResettingQueue, setIsResettingQueue] = useState(false);
  const [isDragActive, setIsDragActive] = useState(false);
  const [runnerTick, setRunnerTick] = useState(0);
  const queueItemsRef = useRef<QueuedFileUploadItem<TResult>[]>([]);
  const startedUploadsRef = useRef<Set<string>>(new Set());
  const nextDispatchAllowedAtRef = useRef<number>(0);
  const runnerTimerRef = useRef<number | null>(null);
  const hasNotifiedCompletionRef = useRef(false);
  const isPausedRef = useRef(true);
  const consecutiveCompletedFailuresRef = useRef(0);
  const uploadAbortControllersRef = useRef<Map<string, AbortController>>(
    new Map()
  );
  const canceledUploadItemIdsRef = useRef<Set<string>>(new Set());
  const filePreparationSessionRef = useRef(0);
  const nextQueueOrderRef = useRef(0);
  const uploaderConfigRef = useRef<ChunkedUploaderConfig | null>(null);
  const queuePersistenceChainRef = useRef<Promise<void>>(Promise.resolve());

  const isAbortLikeError = useCallback((error: unknown) => {
    if (axios.isCancel(error)) {
      return true;
    }

    const candidate = error as
      | { code?: string; name?: string; message?: string }
      | undefined;

    return (
      candidate?.code === "ERR_CANCELED" ||
      candidate?.name === "CanceledError" ||
      candidate?.name === "AbortError" ||
      candidate?.message === "canceled"
    );
  }, []);

  const normalizeUploadError = useCallback(
    (error: unknown): QueuedFileUploadFailureDetails => {
      const uploadResult = error as
        | Partial<{
            isSuccessful: boolean;
            message: string;
            failureCode: string | null;
            requestId: string | null;
          }>
        | undefined;

      if (
        uploadResult?.isSuccessful === false &&
        typeof uploadResult.message === "string" &&
        "failureCode" in uploadResult
      ) {
        return {
          message: uploadResult.message ?? "The upload failed.",
          failureCode:
            typeof uploadResult.failureCode === "string"
              ? uploadResult.failureCode
              : null,
          requestId:
            typeof uploadResult.requestId === "string"
              ? uploadResult.requestId
              : null,
        };
      }

      const apiError = error as Partial<ApiErrorResponse> | undefined;
      const messageText =
        typeof apiError?.message === "string" && apiError.message.length > 0
          ? apiError.message
          : error instanceof Error
            ? error.message
            : "The upload failed.";

      return {
        message: messageText,
        failureCode:
          typeof apiError?.errorCode === "string" ? apiError.errorCode : null,
        requestId:
          typeof apiError?.requestId === "string" ? apiError.requestId : null,
      };
    },
    []
  );

  const scheduleRunner = useCallback((delayMs: number) => {
    if (runnerTimerRef.current !== null) {
      window.clearTimeout(runnerTimerRef.current);
    }

    runnerTimerRef.current = window.setTimeout(() => {
      runnerTimerRef.current = null;
      setRunnerTick((value) => value + 1);
    }, delayMs);
  }, []);

  useEffect(() => {
    queueItemsRef.current = queueItems;
  }, [queueItems]);

  useEffect(() => {
    isPausedRef.current = isPaused;
  }, [isPaused]);

  useEffect(() => {
    uploaderConfigRef.current = uploaderConfig;
  }, [uploaderConfig]);

  useEffect(() => {
    let isMounted = true;

    setIsLoadingConfig(true);
    void (async () => {
      try {
        const config = await SettingsService.GetChunkedUploaderConfig();
        if (!isMounted) {
          return;
        }

        setUploaderConfig({
          maxConcurrentUploads: Math.max(0, config.maxConcurrentUploads),
          maxFileSizeBytes: Math.max(1, config.maxFileSizeBytes),
          chunkSizeBytes: Math.max(1, config.chunkSizeBytes),
        });
      } catch {
        if (!isMounted) {
          return;
        }

        setUploaderConfig(null);
        message.error("Error fetching upload settings.");
      } finally {
        if (isMounted) {
          setIsLoadingConfig(false);
        }
      }
    })();

    return () => {
      isMounted = false;
    };
  }, []);

  useEffect(() => {
    const abortControllers = uploadAbortControllersRef.current;
    return () => {
      if (runnerTimerRef.current !== null) {
        window.clearTimeout(runnerTimerRef.current);
      }

      abortControllers.forEach((controller) => controller.abort());
      abortControllers.clear();
    };
  }, []);

  const createPersistedQueueState = useCallback(
    (
      items: QueuedFileUploadItem<TResult>[],
      paused: boolean,
      started: boolean
    ): PersistedQueueState<TResult> => ({
      version: PERSISTED_QUEUE_VERSION,
      isPaused: paused,
      hasStartedUploadRun: started,
      items: items.map(serializeQueueItem),
    }),
    []
  );

  const enqueueQueueStorageWrite = useCallback(
    (write: () => Promise<void>): Promise<void> => {
      const queuedWrite = queuePersistenceChainRef.current
        .catch(() => undefined)
        .then(write);

      queuePersistenceChainRef.current = queuedWrite.catch(() => undefined);
      return queuedWrite;
    },
    []
  );

  const persistQueueState = useCallback(
    (items: QueuedFileUploadItem<TResult>[], paused: boolean, started: boolean) => {
      if (!storageKey) return;

      const payload = createPersistedQueueState(items, paused, started);

      void enqueueQueueStorageWrite(() =>
        QueuedFileStorage.putQueueState(storageKey, payload)
      ).catch(() => {
        message.warning(
          "This upload queue could not be saved for refresh recovery. Keep this tab open until the upload finishes."
        );
      });
    },
    [createPersistedQueueState, enqueueQueueStorageWrite, storageKey]
  );

  const cancelAllKnownSessions = useCallback(async () => {
    const sessionIds = Array.from(
      new Set(
        queueItemsRef.current
          .map((item) => item.sessionId)
          .filter((sessionId): sessionId is string => !!sessionId)
      )
    );

    await Promise.allSettled(
      sessionIds.map((sessionId) => endpoints.cancelSession(sessionId))
    );
    await endpoints.cancelAllSessions?.();
  }, [endpoints]);

  useEffect(() => {
    if (!storageKey || isRestoring || hydratedStorageKey !== storageKey) return;
    persistQueueState(queueItems, isPaused, hasStartedUploadRun);
  }, [
    hydratedStorageKey,
    hasStartedUploadRun,
    isRestoring,
    isPaused,
    persistQueueState,
    queueItems,
    storageKey,
  ]);

  const resetQueueState = useCallback(async () => {
    filePreparationSessionRef.current += 1;
    setIsResettingQueue(true);

    try {
      await waitForNextPaint();

      const sessionIds = Array.from(
        new Set(
          queueItemsRef.current
            .map((item) => item.sessionId)
            .filter((sessionId): sessionId is string => !!sessionId)
        )
      );

      startedUploadsRef.current.clear();
      uploadAbortControllersRef.current.forEach((controller) =>
        controller.abort()
      );
      uploadAbortControllersRef.current.clear();

      await Promise.allSettled(
        sessionIds.map((sessionId) => endpoints.cancelSession(sessionId))
      );
      await endpoints.cancelAllSessions?.();

      if (storageKey) {
        await enqueueQueueStorageWrite(() => QueuedFileStorage.clearQueue(storageKey));
      }

      nextDispatchAllowedAtRef.current = 0;
      setQueueItems([]);
      setIsPaused(true);
      setHasStartedUploadRun(false);
      setHydratedStorageKey(storageKey ?? null);
      hasNotifiedCompletionRef.current = false;
      nextQueueOrderRef.current = 0;
    } finally {
      setIsResettingQueue(false);
    }
  }, [endpoints, enqueueQueueStorageWrite, storageKey]);

  const loadPersistedQueue = useCallback(async () => {
    if (!storageKey) {
      setHydratedStorageKey(null);
      return;
    }

    setHydratedStorageKey(null);
    setIsRestoring(true);

    try {
      await cancelAllKnownSessions();

      const persisted =
        await QueuedFileStorage.getQueueState<PersistedQueueState<TResult>>(
          storageKey
        );
      if (!persisted) {
        setQueueItems([]);
        setIsPaused(true);
        setHasStartedUploadRun(false);
        nextQueueOrderRef.current = 0;
        return;
      }

      let didFailToReadStoredFile = false;
      const restoredItems = await Promise.all(
        (persisted.items ?? []).map(async (item) => {
          let file: File | null = null;
          const shouldRestoreFile =
            item.status === "queued" || item.status === "uploading";
          const restoredStatus = item.status as string;

          try {
            if (shouldRestoreFile) {
              file = await QueuedFileStorage.getFile(storageKey, item.id);
            }
          } catch {
            didFailToReadStoredFile = true;
            file = null;
          }

          if (!file && shouldRestoreFile) {
            return normalizeQueueItem<TResult>({
              ...item,
              queueOrder:
                typeof item.queueOrder === "number" ? item.queueOrder : 0,
              status: "canceled",
              progress: 0,
              lastError:
                item.lastError ??
                "The queued file could not be restored after refresh. Re-add it to continue.",
              completedAt: item.completedAt ?? new Date().toISOString(),
              file: null,
              sessionId: null,
              uploadedBytes: 0,
              liveUploadedBytes: 0,
              displayUploadedBytes: 0,
              totalBytes: getPositiveNumber(item.size),
              isCurrentUploadSlot: false,
              transportStatus: null,
            });
          }

          // Queue entries survive refresh, but upload sessions do not.
          // We intentionally clear transient server/session progress here so any
          // interrupted upload restarts cleanly from byte 0 when resumed.
          return normalizeQueueItem<TResult>({
            ...item,
            queueOrder:
              typeof item.queueOrder === "number" ? item.queueOrder : 0,
            status: restoredStatus === "uploading" ? "queued" : item.status,
            progress: restoredStatus === "uploading" ? 0 : item.progress,
            completedAt:
              restoredStatus === "uploading" || restoredStatus === "queued"
                ? null
                : item.completedAt ?? null,
            sessionId: null,
            uploadedBytes: 0,
            liveUploadedBytes: 0,
            displayUploadedBytes: 0,
            totalBytes: getPositiveNumber(file?.size, item.size),
            isCurrentUploadSlot: false,
            transportStatus: null,
            file,
          });
        })
      );

      if (didFailToReadStoredFile) {
        message.error("Some queued files could not be restored from browser storage.");
      }

      setQueueItems(restoredItems);
      nextQueueOrderRef.current =
        restoredItems.reduce(
          (maxOrder, item) => Math.max(maxOrder, item.queueOrder),
          -1
        ) + 1;
      setIsPaused(persisted.isPaused ?? true);
      setHasStartedUploadRun(
        persisted.hasStartedUploadRun ??
          restoredItems.some((item) => item.status !== "queued")
      );
    } catch {
      message.error("Error restoring upload queue.");
      setQueueItems([]);
      setIsPaused(true);
      setHasStartedUploadRun(false);
      nextQueueOrderRef.current = 0;
    } finally {
      setIsRestoring(false);
      setHydratedStorageKey(storageKey);
    }
  }, [cancelAllKnownSessions, storageKey]);

  useEffect(() => {
    void loadPersistedQueue();
  }, [loadPersistedQueue]);

  const updateQueueItem = useCallback(
    (
      itemId: string,
      update: (
        item: QueuedFileUploadItem<TResult>
      ) => QueuedFileUploadItem<TResult>
    ): QueuedFileUploadItem<TResult> | null => {
      const current = queueItemsRef.current.find((item) => item.id === itemId);
      if (!current) return null;

      const nextItem = normalizeQueueItem(update(current));
      const nextItems = queueItemsRef.current.map((item) =>
        item.id === itemId ? nextItem : item
      );
      queueItemsRef.current = nextItems;
      setQueueItems(nextItems);
      return nextItem;
    },
    []
  );

  const uploadQueueItem = useCallback(
    async (itemId: string) => {
      if (!storageKey) return;

      const item =
        queueItemsRef.current.find((candidate) => candidate.id === itemId) ??
        null;
      if (!item) return;

      let file = item.file ?? null;
      if (!file) {
        file = await QueuedFileStorage.getFile(storageKey, itemId);
      }

      if (!file) {
        updateQueueItem(itemId, (current) => ({
          ...current,
          status: "canceled",
          progress: 0,
          lastError:
            "The queued file is no longer available. Re-add it to continue.",
          completedAt: current.completedAt ?? new Date().toISOString(),
          isCurrentUploadSlot: false,
          file: null,
        }));
        startedUploadsRef.current.delete(itemId);
        return;
      }

      const fileToUpload = file;
      const activeUploaderConfig = uploaderConfigRef.current;
      if (!activeUploaderConfig) {
        startedUploadsRef.current.delete(itemId);
        return;
      }
      const chunkSizeBytes = activeUploaderConfig.chunkSizeBytes;

      updateQueueItem(itemId, (current) => ({
        ...current,
        status: "uploading",
        progress: Math.max(getUploadDisplayState(current).displayPercent, 1),
        completedAt: null,
        lastError: null,
        file: fileToUpload,
        uploadedBytes: current.uploadedBytes ?? 0,
        liveUploadedBytes: Math.max(
          current.uploadedBytes ?? 0,
          current.liveUploadedBytes ?? 0
        ),
        displayUploadedBytes: Math.max(
          current.uploadedBytes ?? 0,
          current.displayUploadedBytes ?? 0
        ),
        totalBytes: getSafeTotalBytes(current, fileToUpload.size),
        isCurrentUploadSlot: true,
        transportStatus: "chunk_uploading",
      }));

      try {
        let currentItem =
          queueItemsRef.current.find((candidate) => candidate.id === itemId) ??
          item;
        let sessionId = currentItem.sessionId ?? null;
        let uploadedBytes = currentItem.uploadedBytes ?? 0;

        if (!sessionId) {
          const session = await endpoints.createSession(fileToUpload, itemId);

          sessionId = session.sessionId;
          uploadedBytes = session.uploadedBytes;

          updateQueueItem(itemId, (current) => ({
            ...current,
            sessionId,
            uploadedBytes,
            liveUploadedBytes: Math.max(uploadedBytes, current.liveUploadedBytes ?? 0),
            displayUploadedBytes: Math.max(
              uploadedBytes,
              current.displayUploadedBytes ?? 0
            ),
            totalBytes: session.totalBytes,
            progress: Math.max(getUploadDisplayState(current).displayPercent, 1),
            isCurrentUploadSlot: true,
            transportStatus: "chunk_uploading",
          }));
        }

        while (uploadedBytes < fileToUpload.size) {
          if (isPausedRef.current) {
            updateQueueItem(itemId, (current) => ({
              ...current,
              status: "queued",
              progress: getUploadDisplayState(current).displayPercent,
              isCurrentUploadSlot: true,
              transportStatus: null,
              completedAt: null,
              file: fileToUpload,
            }));
            return;
          }

          const offset = uploadedBytes;
          const chunkIndex = Math.floor(offset / chunkSizeBytes);
          const chunk = fileToUpload.slice(
            offset,
            Math.min(offset + chunkSizeBytes, fileToUpload.size)
          );
          let lastProgressUpdateAt = 0;
          const abortController = new AbortController();
          uploadAbortControllersRef.current.set(itemId, abortController);

          const chunkResult = await endpoints.uploadChunk(
            sessionId,
            chunk,
            chunkIndex,
            offset,
            (event) => {
              const now = Date.now();
              if (event.loaded < chunk.size && now - lastProgressUpdateAt < 100) {
                return;
              }

              lastProgressUpdateAt = now;
              const chunkBytes = chunk.size || 1;
              const loaded = Math.min(event.loaded, chunkBytes);
              const overallUploaded = Math.min(
                fileToUpload.size,
                offset + loaded
              );

              updateQueueItem(itemId, (current) => {
                const nextDisplayUploadedBytes = getSmoothedDisplayBytes(
                  current.displayUploadedBytes ?? current.uploadedBytes ?? 0,
                  overallUploaded,
                  fileToUpload.size
                );

                return {
                  ...current,
                  liveUploadedBytes: Math.max(
                    current.uploadedBytes ?? 0,
                    overallUploaded
                  ),
                  displayUploadedBytes: nextDisplayUploadedBytes,
                  progress: toProgressPercent(
                    nextDisplayUploadedBytes,
                    fileToUpload.size
                  ),
                  totalBytes: getSafeTotalBytes(current, fileToUpload.size),
                  isCurrentUploadSlot: true,
                  transportStatus: "chunk_uploading",
                };
              });
            },
            abortController.signal
          );

          uploadedBytes = chunkResult.uploadedBytes;
          updateQueueItem(itemId, (current) => ({
            ...current,
            sessionId: chunkResult.sessionId,
            uploadedBytes: chunkResult.uploadedBytes,
            liveUploadedBytes: Math.max(
              chunkResult.uploadedBytes,
              current.liveUploadedBytes ?? 0
            ),
            displayUploadedBytes: Math.max(
              chunkResult.uploadedBytes,
              current.displayUploadedBytes ?? 0
            ),
            totalBytes: chunkResult.totalBytes,
            progress: toProgressPercent(
              Math.max(chunkResult.uploadedBytes, current.displayUploadedBytes ?? 0),
              chunkResult.totalBytes
            ),
            isCurrentUploadSlot: true,
            transportStatus: "chunk_uploading",
          }));

          uploadAbortControllersRef.current.delete(itemId);
        }

        if (isPausedRef.current) {
          updateQueueItem(itemId, (current) => ({
            ...current,
            status: "queued",
            progress: getUploadDisplayState(current).displayPercent,
            isCurrentUploadSlot: true,
            transportStatus: null,
            completedAt: null,
            file: fileToUpload,
          }));
          return;
        }

        updateQueueItem(itemId, (current) => ({
          ...current,
          transportStatus: "finalizing",
          uploadedBytes: fileToUpload.size,
          liveUploadedBytes: fileToUpload.size,
          displayUploadedBytes: fileToUpload.size,
          totalBytes: fileToUpload.size,
          isCurrentUploadSlot: true,
          progress: 99,
        }));

        const finalizeAbortController = new AbortController();
        uploadAbortControllersRef.current.set(itemId, finalizeAbortController);
        const result = await endpoints.finalizeSession(
          sessionId,
          finalizeAbortController.signal
        );

        await QueuedFileStorage.deleteFile(storageKey, itemId);
        consecutiveCompletedFailuresRef.current = 0;

        updateQueueItem(itemId, (current) => ({
          ...current,
          status: "uploaded",
          progress: 100,
          lastError: null,
          failureCode: null,
          requestId: null,
          completedAt: new Date().toISOString(),
          result,
          sessionId: null,
          uploadedBytes: fileToUpload.size,
          liveUploadedBytes: fileToUpload.size,
          displayUploadedBytes: fileToUpload.size,
          totalBytes: fileToUpload.size,
          isCurrentUploadSlot: false,
          transportStatus: null,
          file: null,
        }));
      } catch (error) {
        const currentItem =
          queueItemsRef.current.find((candidate) => candidate.id === itemId) ??
          item;
        const currentSessionId = currentItem.sessionId ?? null;

        if (
          uploadAbortControllersRef.current.get(itemId)?.signal.aborted ||
          isAbortLikeError(error)
        ) {
          const shouldDiscardSession = canceledUploadItemIdsRef.current.delete(itemId);
          const preservedUploadedBytes = shouldDiscardSession
            ? 0
            : currentItem.uploadedBytes ?? 0;

          if (shouldDiscardSession && currentSessionId) {
            void endpoints.cancelSession(currentSessionId).catch(() => {});
          }

          updateQueueItem(itemId, (current) => ({
            ...current,
            status: "queued",
            progress: shouldDiscardSession
              ? 0
              : getUploadDisplayState(current).displayPercent,
            lastError: null,
            failureCode: null,
            requestId: null,
            completedAt: null,
            result: null,
            sessionId: shouldDiscardSession ? null : current.sessionId ?? null,
            uploadedBytes: preservedUploadedBytes,
            liveUploadedBytes: Math.max(
              preservedUploadedBytes,
              shouldDiscardSession ? 0 : current.liveUploadedBytes ?? 0
            ),
            displayUploadedBytes: Math.max(
              preservedUploadedBytes,
              shouldDiscardSession ? 0 : current.displayUploadedBytes ?? 0
            ),
            totalBytes: getSafeTotalBytes(current, fileToUpload.size),
            isCurrentUploadSlot: !shouldDiscardSession,
            transportStatus: null,
            file: fileToUpload,
          }));
          return;
        }

        const failure = mapFailure
          ? mapFailure(normalizeUploadError(error), error)
          : normalizeUploadError(error);

        if (failure.failureCode === "NotFound" && currentSessionId) {
          updateQueueItem(itemId, (current) => ({
            ...current,
            status: "queued",
            progress: 0,
            lastError: null,
            failureCode: null,
            requestId: null,
            completedAt: null,
            result: null,
            sessionId: null,
            uploadedBytes: 0,
            liveUploadedBytes: 0,
            displayUploadedBytes: 0,
            totalBytes: fileToUpload.size,
            isCurrentUploadSlot: false,
            transportStatus: null,
            file: fileToUpload,
          }));
          return;
        }

        const terminalStatus = failure.terminalStatus ?? "failed";
        if (terminalStatus === "failed") {
          consecutiveCompletedFailuresRef.current += 1;
        } else {
          consecutiveCompletedFailuresRef.current = 0;
        }

        updateQueueItem(itemId, (current) => ({
          ...current,
          status: terminalStatus,
          progress: 0,
          retryCount:
            terminalStatus === "failed" ? current.retryCount + 1 : current.retryCount,
          lastError: failure.message,
          failureCode: failure.failureCode ?? null,
          requestId: failure.requestId ?? null,
          completedAt: new Date().toISOString(),
          result: null,
          sessionId: null,
          uploadedBytes: 0,
          liveUploadedBytes: 0,
          displayUploadedBytes: 0,
          totalBytes: fileToUpload.size,
          isCurrentUploadSlot: false,
          transportStatus: null,
          file: terminalStatus === "skipped" ? null : fileToUpload,
        }));

        if (terminalStatus === "skipped") {
          void QueuedFileStorage.deleteFile(storageKey, itemId).catch(() => {});
        }

        if (
          pauseOnFailures &&
          terminalStatus === "failed" &&
          consecutiveCompletedFailuresRef.current >= MAX_CONSECUTIVE_UPLOAD_FAILURES
        ) {
          setIsPaused(true);
          uploadAbortControllersRef.current.forEach((controller, activeItemId) => {
            if (activeItemId !== itemId) {
              canceledUploadItemIdsRef.current.add(activeItemId);
              controller.abort();
            }
          });
        }
      } finally {
        uploadAbortControllersRef.current.delete(itemId);
        nextDispatchAllowedAtRef.current = Date.now() + dispatchDelayMs;
        startedUploadsRef.current.delete(itemId);
        setRunnerTick((value) => value + 1);
      }
    },
    [
      dispatchDelayMs,
      endpoints,
      isAbortLikeError,
      mapFailure,
      normalizeUploadError,
      pauseOnFailures,
      storageKey,
      updateQueueItem,
    ]
  );

  useEffect(() => {
    if (!storageKey || !uploaderConfig || isPaused || isRestoring) return;

    const now = Date.now();
    const { runnableItems } = deriveOrderedQueueView(
      queueItems,
      now,
      uploaderConfig.maxConcurrentUploads
    );
    const waitForDispatch = nextDispatchAllowedAtRef.current - now;
    if (waitForDispatch > 0) {
      scheduleRunner(waitForDispatch);
      return;
    }

    if (runnableItems.length === 0) {
      return;
    }

    runnableItems.forEach((item) => {
      if (startedUploadsRef.current.has(item.id)) return;
      startedUploadsRef.current.add(item.id);
      void uploadQueueItem(item.id);
    });
  }, [
    isPaused,
    isRestoring,
    queueItems,
    runnerTick,
    scheduleRunner,
    storageKey,
    uploaderConfig,
    uploadQueueItem,
  ]);

  const addFilesToQueue = useCallback(
    async (fileList: FileList) => {
      const activeUploaderConfig = uploaderConfigRef.current;
      if (!activeUploaderConfig) {
        return;
      }

      const addedAt = Date.now();
      const startingQueueOrder = nextQueueOrderRef.current;
      const files = Array.from(fileList);
      const validFiles: Array<{ file: File; fileIndex: number }> = [];
      const failedItems: QueuedFileUploadItem<TResult>[] = [];
      let nextConsecutiveCompletedFailures =
        consecutiveCompletedFailuresRef.current;

      files.forEach((file, index) => {
        if (file.size > activeUploaderConfig.maxFileSizeBytes) {
          const sizeLabel = formatFileSize(activeUploaderConfig.maxFileSizeBytes);
          message.error(`${file.name} exceeds the ${sizeLabel} upload limit.`);
          return;
        }

        const validationResult = validateFile ? validateFile(file) : true;
        if (isValidationFailure(validationResult)) {
          failedItems.push({
            id: createQueueItemId(),
            queueOrder: startingQueueOrder + index,
            fileName: file.name,
            size: file.size,
            type: file.type,
            lastModified: file.lastModified,
            addedOn: new Date(addedAt + index).toISOString(),
            status: validationResult.terminalStatus ?? "failed",
            progress: 0,
            retryCount: 1,
            lastError: validationResult.message,
            failureCode: validationResult.failureCode ?? null,
            requestId: null,
            completedAt: new Date().toISOString(),
            result: null,
            sessionId: null,
            uploadedBytes: 0,
            liveUploadedBytes: 0,
            displayUploadedBytes: 0,
            totalBytes: file.size,
            isCurrentUploadSlot: false,
            transportStatus: null,
            file,
          });
          nextConsecutiveCompletedFailures += 1;
          return;
        }

        if (validationResult) {
          validFiles.push({ file, fileIndex: index });
        }
      });

      if (validFiles.length === 0 && failedItems.length === 0) return;

      setIsPaused((current) =>
        current || queueItemsRef.current.every((item) => isTerminalStatus(item.status))
          ? true
          : current
      );

      const newItems = validFiles.map<QueuedFileUploadItem<TResult>>(
        ({ file, fileIndex }) => ({
          id: createQueueItemId(),
          queueOrder: startingQueueOrder + fileIndex,
          fileName: file.name,
          size: file.size,
          type: file.type,
          lastModified: file.lastModified,
          addedOn: new Date(addedAt + fileIndex).toISOString(),
          status: "queued",
          progress: 0,
          retryCount: 0,
          lastError: null,
          failureCode: null,
          requestId: null,
          completedAt: null,
          result: null,
          sessionId: null,
          uploadedBytes: 0,
          liveUploadedBytes: 0,
          displayUploadedBytes: 0,
          totalBytes: file.size,
          isCurrentUploadSlot: false,
          transportStatus: null,
          file,
        })
      );
      nextQueueOrderRef.current = startingQueueOrder + files.length;

      const itemsToAdd = [...failedItems, ...newItems].sort(
        (left, right) => left.queueOrder - right.queueOrder
      );
      const nextItems = [...queueItemsRef.current, ...itemsToAdd];
      const nextPaused =
        pauseOnFailures &&
        nextConsecutiveCompletedFailures >= MAX_CONSECUTIVE_UPLOAD_FAILURES
          ? true
          : isPausedRef.current ||
            queueItemsRef.current.every((item) => isTerminalStatus(item.status));

      consecutiveCompletedFailuresRef.current =
        nextConsecutiveCompletedFailures;
      queueItemsRef.current = nextItems;
      setQueueItems(nextItems);
      hasNotifiedCompletionRef.current = false;

      if (nextPaused) {
        setIsPaused(true);
        if (pauseOnFailures) {
          uploadAbortControllersRef.current.forEach((controller, activeItemId) => {
            canceledUploadItemIdsRef.current.add(activeItemId);
            controller.abort();
          });
        }
      } else {
        setIsPaused((current) =>
          current ||
          queueItemsRef.current.every((item) => isTerminalStatus(item.status))
            ? true
            : current
        );
      }

      if (!storageKey || newItems.length === 0) {
        return;
      }

      const preparationSession = filePreparationSessionRef.current + 1;
      filePreparationSessionRef.current = preparationSession;

      void (async () => {
        try {
          await waitForNextPaint();

          for (
            let startIndex = 0;
            startIndex < newItems.length;
            startIndex += FILE_STORAGE_BATCH_SIZE
          ) {
            if (preparationSession !== filePreparationSessionRef.current) {
              return;
            }

            const batch = newItems.slice(
              startIndex,
              startIndex + FILE_STORAGE_BATCH_SIZE
            );

            await QueuedFileStorage.putFiles(
              storageKey,
              batch.map((item) => ({
                itemId: item.id,
                file: item.file as File,
              }))
            );

            if (preparationSession !== filePreparationSessionRef.current) {
              return;
            }

            await waitForNextPaint();
          }
        } catch {
          message.warning(
            "Some selected files could not be saved for refresh recovery. Keep this tab open until the upload finishes."
          );
        }
      })();
    },
    [pauseOnFailures, storageKey, validateFile]
  );

  const retryFailed = useCallback(() => {
    hasNotifiedCompletionRef.current = false;
    consecutiveCompletedFailuresRef.current = 0;
    setIsPaused(true);
    setQueueItems((items) =>
      items.map((item) =>
        item.status === "failed" || item.status === "canceled"
          ? {
              ...item,
              status: item.file ? ("queued" as const) : ("canceled" as const),
              progress: 0,
              retryCount: 0,
              lastError: item.file
                ? null
                : "The queued file is no longer available. Re-add it to continue.",
              failureCode: null,
              requestId: null,
              completedAt: null,
              result: null,
              sessionId: null,
              uploadedBytes: 0,
              liveUploadedBytes: 0,
              displayUploadedBytes: 0,
              totalBytes: getPositiveNumber(item.file?.size, item.size),
              isCurrentUploadSlot: false,
              transportStatus: null,
            }
          : item
      )
    );
  }, []);

  const handleFileSelect = useCallback(
    async (event: React.ChangeEvent<HTMLInputElement>) => {
      if (!event.target.files) return;
      await addFilesToQueue(event.target.files);
      event.target.value = "";
    },
    [addFilesToQueue]
  );

  const handleDrop = useCallback(
    async (event: React.DragEvent<HTMLElement>) => {
      event.preventDefault();
      setIsDragActive(false);
      if (!event.dataTransfer.files) return;
      await addFilesToQueue(event.dataTransfer.files);
    },
    [addFilesToQueue]
  );

  const handleDragOver = useCallback((event: React.DragEvent<HTMLElement>) => {
    event.preventDefault();
    if (event.dataTransfer.types.includes("Files")) {
      setIsDragActive(true);
      event.dataTransfer.dropEffect = "copy";
    }
  }, []);

  const handleDragEnter = useCallback((event: React.DragEvent<HTMLElement>) => {
    event.preventDefault();
    if (event.dataTransfer.types.includes("Files")) {
      setIsDragActive(true);
    }
  }, []);

  const handleDragLeave = useCallback((event: React.DragEvent<HTMLElement>) => {
    event.preventDefault();
    if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
      setIsDragActive(false);
    }
  }, []);

  const fileResults = useMemo(
    () =>
      queueItems.filter(
        (item) => item.status !== "queued" && item.status !== "uploading"
      ),
    [queueItems]
  );

  const queueStats = useMemo(() => {
    const uploaded = queueItems.filter((item) => item.status === "uploaded").length;
    const skipped = queueItems.filter((item) => item.status === "skipped").length;
    const failed = queueItems.filter((item) => item.status === "failed").length;
    const canceled = queueItems.filter((item) => item.status === "canceled").length;
    const uploading = queueItems.filter((item) => item.status === "uploading").length;
    const queued = queueItems.filter((item) => item.status === "queued").length;
    const remaining = queued + uploading;

    return { uploaded, skipped, failed, canceled, uploading, queued, remaining };
  }, [queueItems]);

  const uploadQueueItems = useMemo(
    () => getOrderedPendingQueueItems(queueItems),
    [queueItems]
  );

  const failedItems = useMemo(
    () =>
      queueItems.filter(
        (item) => item.status === "failed" || item.status === "canceled"
      ),
    [queueItems]
  );

  const recentActivity = useMemo(
    () =>
      [...queueItems]
        .filter((item) => isTerminalStatus(item.status))
        .sort(
          (left, right) =>
            (right.completedAt ?? "").localeCompare(left.completedAt ?? "") ||
            right.queueOrder - left.queueOrder
        ),
    [queueItems]
  );

  const aggregateProgress = useMemo(() => {
    if (queueItems.length === 0) return 0;

    const totalBytes = queueItems.reduce(
      (sum, item) => sum + getUploadDisplayState(item).totalBytes,
      0
    );
    if (totalBytes === 0) {
      return queueItems.every((item) => isTerminalStatus(item.status)) ? 100 : 0;
    }

    const completedBytes = queueItems.reduce((sum, item) => {
      const displayState = getUploadDisplayState(item);
      const completedItemBytes = isTerminalStatus(item.status)
        ? displayState.totalBytes
        : displayState.displayUploadedBytes;
      return sum + completedItemBytes;
    }, 0);

    if (completedBytes <= 0) {
      return 0;
    }

    const rawPercent = (100 * completedBytes) / totalBytes;
    if (rawPercent < 1) {
      return 1;
    }

    return Math.min(100, Math.round(rawPercent));
  }, [queueItems]);

  const allWorkComplete =
    queueItems.length > 0 &&
    queueItems.every((item) => isTerminalStatus(item.status));
  const hasSuccessfulUploads = queueStats.uploaded + queueStats.skipped > 0;
  useEffect(() => {
    if (!allWorkComplete || !hasSuccessfulUploads || hasNotifiedCompletionRef.current) {
      return;
    }

    setIsPaused(true);
    hasNotifiedCompletionRef.current = true;
    onCompleted?.(queueItems);
  }, [allWorkComplete, hasSuccessfulUploads, onCompleted, queueItems]);

  const pauseUploads = useCallback(() => {
    setIsPaused(true);
    uploadAbortControllersRef.current.forEach((controller) =>
      controller.abort()
    );
    uploadAbortControllersRef.current.clear();
  }, []);

  const startOrResumeUploads = useCallback(() => {
    consecutiveCompletedFailuresRef.current = 0;
    setHasStartedUploadRun(true);
    setIsPaused(false);
  }, []);

  const uploadControl = useMemo(() => {
    const canPause = !isPaused && queueStats.remaining > 0;
    const canStart =
      isPaused &&
      queueStats.queued > 0 &&
      !isLoadingConfig &&
      (uploaderConfig?.maxConcurrentUploads ?? 0) > 0;

    if (canPause) {
      return {
        label: "Pause",
        icon: <PauseCircleOutlined />,
        disabled: isRestoring,
        onClick: pauseUploads,
      };
    }

    return {
      label: hasStartedUploadRun ? "Resume" : "Start Upload",
      icon: <PlayCircleOutlined />,
      disabled: isRestoring || !canStart,
      onClick: startOrResumeUploads,
    };
  }, [
    hasStartedUploadRun,
    isPaused,
    isLoadingConfig,
    isRestoring,
    pauseUploads,
    queueStats.queued,
    queueStats.remaining,
    startOrResumeUploads,
    uploaderConfig,
  ]);

  const removeQueueItem = useCallback(
    (itemId: string) => {
      const sessionId =
        queueItemsRef.current.find((item) => item.id === itemId)?.sessionId ?? null;

      uploadAbortControllersRef.current.get(itemId)?.abort();
      uploadAbortControllersRef.current.delete(itemId);
      startedUploadsRef.current.delete(itemId);

      setQueueItems((items) => items.filter((item) => item.id !== itemId));

      if (sessionId) {
        void endpoints.cancelSession(sessionId).catch(() => {});
      }

      if (storageKey) {
        void QueuedFileStorage.deleteFile(storageKey, itemId).catch(() => {});
      }
    },
    [endpoints, storageKey]
  );

  return {
    queueItems,
    queueStats,
    uploadQueueItems,
    failedItems,
    recentActivity,
    aggregateProgress,
    fileResults,
    isPaused,
    isLoadingConfig,
    hasStartedUploadRun,
    isRestoring,
    isResettingQueue,
    isDragActive,
    uploadControl,
    addFilesToQueue,
    handleFileSelect,
    handleDrop,
    handleDragOver,
    handleDragEnter,
    handleDragLeave,
    retryFailed,
    resetQueueState,
    removeQueueItem,
  };
};
