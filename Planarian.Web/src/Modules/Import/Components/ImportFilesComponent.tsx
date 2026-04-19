import {
  Button,
  Card,
  Checkbox,
  Col,
  Dropdown,
  Form,
  Input,
  Row,
  Space,
  Typography,
  message,
} from "antd";
import {
  ClearOutlined,
  ClockCircleOutlined,
  DownOutlined,
  EyeOutlined,
  FileAddOutlined,
  PauseCircleOutlined,
  PlayCircleOutlined,
  RedoOutlined,
} from "@ant-design/icons";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import Papa from "papaparse";
import "./ImportComponent.scss";
import { CSVDisplay } from "../../Files/Components/CsvDisplayComponent";
import { getFileType } from "../../Files/Services/FileHelpers";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { nameof } from "../../../Shared/Helpers/StringHelpers";
import { ApiErrorResponse, ApiExceptionType } from "../../../Shared/Models/ApiErrorResponse";
import { AccountService } from "../../Account/Services/AccountService";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { FileImportResult } from "../Models/FileUploadresult";
import { ImportQueueStorage } from "../Services/ImportQueueStorage";

const MAX_FILE_SIZE_MB = 500;
const DEFAULT_UPLOAD_CONCURRENCY = 1;
const MAX_RETRY_COUNT = 5;
const DISPATCH_DELAY_MS = 1000;
const RECENT_ACTIVITY_LIMIT = 12;
const LOCAL_STORAGE_VERSION = 1;
const LOCAL_STORAGE_PREFIX = "planarian-import-files-queue";

type ImportQueueItemStatus =
  | "queued"
  | "uploading"
  | "uploaded"
  | "failed"
  | "retry_wait"
  | "canceled";

interface DelimiterFormFields {
  delimiter: string;
  idRegex: string;
  ignoreDuplicates: boolean;
}

interface ImportCaveComponentProps {
  onUploaded: () => void;
}

interface ImportSettings {
  delimiter: string;
  idRegex: string;
  ignoreDuplicates: boolean;
}

interface ImportQueueItem {
  id: string;
  fileName: string;
  size: number;
  type: string;
  lastModified: number;
  addedOn: string;
  status: ImportQueueItemStatus;
  progress: number;
  retryCount: number;
  retryAt?: number | null;
  lastError?: string | null;
  associatedCave?: string | null;
  failureCode?: string | null;
  isRetryable: boolean;
  requestId?: string | null;
  result?: FileImportResult | null;
  file?: File | null;
}

interface PersistedImportQueueItem extends Omit<ImportQueueItem, "file"> { }

interface PersistedImportQueueState {
  version: number;
  isPaused: boolean;
  items: PersistedImportQueueItem[];
}

interface UploadFailureDetails {
  message: string;
  failureCode?: string | null;
  isRetryable: boolean;
  retryAfterSeconds?: number;
  requestId?: string | null;
}

const createQueueItemId = () => {
  if (typeof crypto !== "undefined" && crypto.randomUUID) {
    return crypto.randomUUID();
  }

  return `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
};

const createStorageKey = (accountId: string, settings: ImportSettings) =>
  [
    LOCAL_STORAGE_PREFIX,
    accountId,
    settings.delimiter || "__none__",
    settings.idRegex,
    settings.ignoreDuplicates ? "ignore-duplicates" : "keep-duplicates",
  ].join("::");

const isTerminalStatus = (status: ImportQueueItemStatus) =>
  status === "uploaded" || status === "failed" || status === "canceled";

const canStartUpload = (item: ImportQueueItem, now: number) =>
  (item.status === "queued" ||
    (item.status === "retry_wait" && (item.retryAt ?? 0) <= now)) &&
  !!item.file;

const formatFileSize = (bytes: number) =>
  `${(bytes / 1024 / 1024).toFixed(1)} MB`;

const buildRetryDelayMs = (
  retryCount: number,
  retryAfterSeconds?: number
) => {
  if (retryAfterSeconds && retryAfterSeconds > 0) {
    return retryAfterSeconds * 1000;
  }

  const backoffMs = Math.min(60000, 2000 * 2 ** retryCount);
  const jitterMs = Math.min(5000, retryCount * 250);
  return backoffMs + jitterMs;
};

const isFileImportResultError = (
  error: unknown
): error is Partial<FileImportResult> & Partial<ApiErrorResponse> => {
  const candidate = error as Partial<FileImportResult> | undefined;

  return (
    candidate?.isSuccessful === false &&
    typeof candidate.message === "string" &&
    "failureCode" in candidate
  );
};

const normalizeUploadError = (error: unknown): UploadFailureDetails => {
  if (isFileImportResultError(error)) {
    const statusCode = error.statusCode;
    const isTransientStatus =
      statusCode === 408 ||
      statusCode === 429 ||
      statusCode === 500 ||
      statusCode === 502 ||
      statusCode === 503 ||
      statusCode === 504;

    return {
      message: error.message ?? "The upload failed.",
      failureCode:
        typeof error.failureCode === "string" ? error.failureCode : null,
      isRetryable: error.isRetryable ?? isTransientStatus,
      retryAfterSeconds:
        typeof error.retryAfterSeconds === "number"
          ? error.retryAfterSeconds
          : undefined,
      requestId:
        typeof error.requestId === "string" ? error.requestId : null,
    };
  }

  const apiError = error as Partial<ApiErrorResponse> | undefined;
  const statusCode = apiError?.statusCode;
  const messageText =
    typeof apiError?.message === "string" && apiError.message.length > 0
      ? apiError.message
      : error instanceof Error
        ? error.message
        : "The upload failed.";

  const isTransientStatus =
    statusCode === 408 ||
    statusCode === 429 ||
    statusCode === 500 ||
    statusCode === 502 ||
    statusCode === 503 ||
    statusCode === 504;

  const retryable =
    apiError?.errorCode === ApiExceptionType.TooManyRequests ||
    isTransientStatus ||
    (!apiError?.errorCode && !statusCode);

  return {
    message: messageText,
    failureCode:
      typeof apiError?.errorCode === "string" ? apiError.errorCode : null,
    isRetryable: retryable,
    retryAfterSeconds:
      typeof apiError?.retryAfterSeconds === "number"
        ? apiError.retryAfterSeconds
        : undefined,
    requestId:
      typeof apiError?.requestId === "string" ? apiError.requestId : null,
  };
};

const serializeQueueItem = (
  item: ImportQueueItem
): PersistedImportQueueItem => ({
  id: item.id,
  fileName: item.fileName,
  size: item.size,
  type: item.type,
  lastModified: item.lastModified,
  addedOn: item.addedOn,
  status: item.status,
  progress: item.progress,
  retryCount: item.retryCount,
  retryAt: item.retryAt ?? null,
  lastError: item.lastError ?? null,
  associatedCave: item.associatedCave ?? null,
  failureCode: item.failureCode ?? null,
  isRetryable: item.isRetryable,
  requestId: item.requestId ?? null,
  result: item.result ?? null,
});

const createCsvRow = (item: ImportQueueItem): FileImportResult => ({
  fileName: item.fileName,
  isSuccessful: item.status === "uploaded",
  associatedCave: item.associatedCave ?? item.result?.associatedCave ?? "",
  message: item.result?.message ?? item.lastError ?? "",
  failureCode: item.failureCode ?? item.result?.failureCode ?? null,
  isRetryable: item.isRetryable,
  requestId: item.requestId ?? item.result?.requestId ?? null,
});

export const ImportFilesComponent: React.FC<ImportCaveComponentProps> = ({
  onUploaded,
}) => {
  const [form] = Form.useForm<DelimiterFormFields>();
  const [confirmedSettings, setConfirmedSettings] = useState<ImportSettings | null>(
    null
  );
  const [inputsConfirmed, setInputsConfirmed] = useState(false);
  const [queueItems, setQueueItems] = useState<ImportQueueItem[]>([]);
  const [isPaused, setIsPaused] = useState(true);
  const [isRestoring, setIsRestoring] = useState(false);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isDragActive, setIsDragActive] = useState(false);
  const [collapsedToolbarActionsCount, setCollapsedToolbarActionsCount] =
    useState(0);
  const [runnerTick, setRunnerTick] = useState(0);
  const queueItemsRef = useRef<ImportQueueItem[]>([]);
  const toolbarContainerRef = useRef<HTMLDivElement | null>(null);
  const toolbarActionsRef = useRef<HTMLDivElement | null>(null);
  const toolbarStatsRef = useRef<HTMLDivElement | null>(null);
  const toolbarActionWidthsRef = useRef([0, 0, 0, 0, 0]);
  const toolbarMoreWidthRef = useRef(82);
  const startedUploadsRef = useRef<Set<string>>(new Set());
  const nextDispatchAllowedAtRef = useRef<number>(0);
  const runnerTimerRef = useRef<number | null>(null);
  const hasNotifiedCompletionRef = useRef(false);
  const uploadAbortControllersRef = useRef<Map<string, AbortController>>(
    new Map()
  );

  const accountId = AuthenticationService.GetAccountId() ?? "anonymous";
  const queueStorageKey = useMemo(
    () =>
      confirmedSettings ? createStorageKey(accountId, confirmedSettings) : null,
    [accountId, confirmedSettings]
  );

  useEffect(() => {
    const toolbarContainer = toolbarContainerRef.current;
    const toolbarActions = toolbarActionsRef.current;
    const toolbarStats = toolbarStatsRef.current;

    if (!toolbarContainer || !toolbarActions || !toolbarStats) return;

    let animationFrame: number | null = null;

    const updateToolbarMode = () => {
      if (animationFrame) {
        window.cancelAnimationFrame(animationFrame);
      }

      animationFrame = window.requestAnimationFrame(() => {
        toolbarActions
          .querySelectorAll<HTMLElement>("[data-toolbar-action-index]")
          .forEach((element) => {
            const index = Number(element.dataset.toolbarActionIndex);
            if (index >= 0 && index <= 4 && element.offsetWidth > 0) {
              toolbarActionWidthsRef.current[index] = element.offsetWidth;
            }
          });

        const moreButton = toolbarActions.querySelector<HTMLElement>(
          "[data-toolbar-more]"
        );
        if (moreButton && moreButton.offsetWidth > 0) {
          toolbarMoreWidthRef.current = moreButton.offsetWidth;
        }

        const statsWidth = toolbarStats.offsetWidth;
        const toolbarGap = 8;
        const actionGap = 8;
        const availableWidth = toolbarContainer.clientWidth;

        const getActionsWidth = (collapsedCount: number) => {
          const visibleActionIndexes = [0, 1, 2, 3, 4].filter(
            (index) =>
              (collapsedCount < 5 && index === 0) ||
              index < 5 - collapsedCount
          );
          const visibleActionsWidth = visibleActionIndexes.reduce(
            (total, index) => total + toolbarActionWidthsRef.current[index],
            0
          );
          const visibleControlsCount =
            visibleActionIndexes.length + (collapsedCount > 0 ? 1 : 0);

          return (
            visibleActionsWidth +
            (collapsedCount > 0 ? toolbarMoreWidthRef.current : 0) +
            actionGap * Math.max(0, visibleControlsCount - 1)
          );
        };

        let nextCollapsedCount = 0;
        for (let count = 0; count <= 5; count += 1) {
          const requiredWidth = getActionsWidth(count) + statsWidth + toolbarGap;
          if (requiredWidth <= availableWidth - 2) {
            nextCollapsedCount = count;
            break;
          }

          nextCollapsedCount = count;
        }

        setCollapsedToolbarActionsCount((current) =>
          current === nextCollapsedCount ? current : nextCollapsedCount
        );
      });
    };

    updateToolbarMode();

    const resizeObserver = new ResizeObserver(updateToolbarMode);
    resizeObserver.observe(toolbarContainer);
    resizeObserver.observe(toolbarActions);
    resizeObserver.observe(toolbarStats);

    return () => {
      resizeObserver.disconnect();
      if (animationFrame) {
        window.cancelAnimationFrame(animationFrame);
      }
    };
  }, [collapsedToolbarActionsCount, inputsConfirmed]);

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
    return () => {
      if (runnerTimerRef.current !== null) {
        window.clearTimeout(runnerTimerRef.current);
      }

      uploadAbortControllersRef.current.forEach((controller) =>
        controller.abort()
      );
      uploadAbortControllersRef.current.clear();
    };
  }, []);

  const persistQueueState = useCallback(
    (items: ImportQueueItem[], paused: boolean) => {
      if (!queueStorageKey) return;

      const payload: PersistedImportQueueState = {
        version: LOCAL_STORAGE_VERSION,
        isPaused: paused,
        items: items.map(serializeQueueItem),
      };

      localStorage.setItem(queueStorageKey, JSON.stringify(payload));
    },
    [queueStorageKey]
  );

  useEffect(() => {
    if (!queueStorageKey || isRestoring) return;
    persistQueueState(queueItems, isPaused);
  }, [queueItems, isPaused, queueStorageKey, isRestoring, persistQueueState]);

  const resetQueueState = useCallback(async () => {
    if (queueStorageKey) {
      localStorage.removeItem(queueStorageKey);
      await ImportQueueStorage.clearQueue(queueStorageKey);
    }

    startedUploadsRef.current.clear();
    uploadAbortControllersRef.current.forEach((controller) =>
      controller.abort()
    );
    uploadAbortControllersRef.current.clear();
    nextDispatchAllowedAtRef.current = 0;
    setQueueItems([]);
    setIsPaused(true);
    hasNotifiedCompletionRef.current = false;
  }, [queueStorageKey]);

  const loadPersistedQueue = useCallback(async () => {
    if (!queueStorageKey) return;

    setIsRestoring(true);

    try {
      const serialized = localStorage.getItem(queueStorageKey);
      if (!serialized) {
        setQueueItems([]);
        setIsPaused(true);
        return;
      }

      const persisted = JSON.parse(serialized) as PersistedImportQueueState;
      const restoredItems = await Promise.all(
        (persisted.items ?? []).map(async (item) => {
          let file: File | null = null;
          if (!isTerminalStatus(item.status)) {
            file = await ImportQueueStorage.getFile(queueStorageKey, item.id);
          }

          if (!file && !isTerminalStatus(item.status)) {
            return {
              ...item,
              status: "canceled" as const,
              progress: 0,
              retryAt: null,
              lastError:
                item.lastError ??
                "The queued file could not be restored after refresh. Re-add it to continue.",
              isRetryable: false,
              file: null,
            };
          }

          return {
            ...item,
            status:
              item.status === "uploading"
                ? ("queued" as const)
                : item.status,
            retryAt: item.retryAt ?? null,
            file,
          };
        })
      );

      setQueueItems(restoredItems);
      setIsPaused(persisted.isPaused ?? true);
    } catch {
      setQueueItems([]);
      setIsPaused(true);
    } finally {
      setIsRestoring(false);
    }
  }, [queueStorageKey]);

  useEffect(() => {
    if (!queueStorageKey) return;
    void loadPersistedQueue();
  }, [queueStorageKey, loadPersistedQueue]);

  const updateQueueItem = useCallback(
    (
      itemId: string,
      update: (item: ImportQueueItem) => ImportQueueItem
    ): ImportQueueItem | null => {
      const current = queueItemsRef.current.find((item) => item.id === itemId);
      if (!current) return null;

      const nextItem = update(current);
      setQueueItems((items) =>
        items.map((item) => (item.id === itemId ? nextItem : item))
      );
      return nextItem;
    },
    []
  );

  const removeUploadedFilesFromStorage = useCallback(
    async (items: ImportQueueItem[]) => {
      if (!queueStorageKey) return;

      await Promise.all(
        items.map((item) => ImportQueueStorage.deleteFile(queueStorageKey, item.id))
      );
    },
    [queueStorageKey]
  );

  const uploadQueueItem = useCallback(
    async (itemId: string) => {
      if (!confirmedSettings || !queueStorageKey) return;

      let item =
        queueItemsRef.current.find((candidate) => candidate.id === itemId) ??
        null;
      if (!item) return;

      let file = item.file ?? null;
      if (!file) {
        file = await ImportQueueStorage.getFile(queueStorageKey, itemId);
      }

      if (!file) {
        updateQueueItem(itemId, (current) => ({
          ...current,
          status: "canceled",
          progress: 0,
          retryAt: null,
          lastError:
            "The queued file is no longer available. Re-add it to continue.",
          isRetryable: false,
          file: null,
        }));
        startedUploadsRef.current.delete(itemId);
        return;
      }

      updateQueueItem(itemId, (current) => ({
        ...current,
        status: "uploading",
        progress: Math.max(current.progress, 1),
        retryAt: null,
        lastError: null,
        file,
      }));

      const fileToUpload = file;
      const abortController = new AbortController();
      uploadAbortControllersRef.current.set(itemId, abortController);

      try {
        const result = await AccountService.ImportFile(
          fileToUpload,
          itemId,
          confirmedSettings.delimiter,
          confirmedSettings.idRegex,
          confirmedSettings.ignoreDuplicates,
          (event) => {
            const totalBytes = event.total || fileToUpload.size || 1;
            const percent = Math.min(
              99,
              Math.max(1, Math.round((100 * event.loaded) / totalBytes))
            );

            updateQueueItem(itemId, (current) => ({
              ...current,
              progress: percent,
            }));
          },
          abortController.signal
        );

        await ImportQueueStorage.deleteFile(queueStorageKey, itemId);

        updateQueueItem(itemId, (current) => ({
          ...current,
          status: "uploaded",
          progress: 100,
          retryAt: null,
          lastError: null,
          associatedCave: result.associatedCave,
          failureCode: result.failureCode ?? null,
          isRetryable: false,
          requestId: result.requestId ?? null,
          result,
          file: null,
        }));
      } catch (error) {
        if (abortController.signal.aborted) {
          updateQueueItem(itemId, (current) => ({
            ...current,
            status: "queued",
            progress: 0,
            retryAt: null,
            lastError: null,
            failureCode: null,
            isRetryable: false,
            requestId: null,
            result: null,
            file: fileToUpload,
          }));
          return;
        }

        const failure = normalizeUploadError(error);
        const currentItem =
          queueItemsRef.current.find((candidate) => candidate.id === itemId) ??
          item;
        const nextRetryCount = currentItem.retryCount + 1;

        if (failure.isRetryable && nextRetryCount <= MAX_RETRY_COUNT) {
          const delayMs = buildRetryDelayMs(
            currentItem.retryCount,
            failure.retryAfterSeconds
          );

          updateQueueItem(itemId, (current) => ({
            ...current,
            status: "retry_wait",
            progress: 0,
            retryCount: current.retryCount + 1,
            retryAt: Date.now() + delayMs,
            lastError: failure.message,
            failureCode: failure.failureCode ?? null,
            isRetryable: true,
            requestId: failure.requestId ?? null,
            result: null,
            file: fileToUpload,
          }));
        } else {
          updateQueueItem(itemId, (current) => ({
            ...current,
            status: "failed",
            progress: 0,
            retryAt: null,
            retryCount: current.retryCount + (failure.isRetryable ? 1 : 0),
            lastError: failure.message,
            failureCode: failure.failureCode ?? null,
            isRetryable: failure.isRetryable,
            requestId: failure.requestId ?? null,
            result: {
              fileName: current.fileName,
              isSuccessful: false,
              associatedCave: current.associatedCave ?? "",
              message: failure.message,
              failureCode: failure.failureCode ?? null,
              isRetryable: failure.isRetryable,
              requestId: failure.requestId ?? null,
            },
            file: fileToUpload,
          }));
        }
      } finally {
        uploadAbortControllersRef.current.delete(itemId);
        nextDispatchAllowedAtRef.current = Date.now() + DISPATCH_DELAY_MS;
        startedUploadsRef.current.delete(itemId);
        setRunnerTick((value) => value + 1);
      }
    },
    [confirmedSettings, queueStorageKey, updateQueueItem]
  );

  useEffect(() => {
    if (!queueStorageKey || !confirmedSettings || isPaused || isRestoring) return;

    const activeUploads = queueItems.filter((item) => item.status === "uploading")
      .length;
    if (activeUploads >= DEFAULT_UPLOAD_CONCURRENCY) return;

    const now = Date.now();
    const waitForDispatch = nextDispatchAllowedAtRef.current - now;
    if (waitForDispatch > 0) {
      scheduleRunner(waitForDispatch);
      return;
    }

    const nextRetryAt = queueItems
      .filter((item) => item.status === "retry_wait" && item.retryAt)
      .map((item) => item.retryAt as number)
      .sort((left, right) => left - right)[0];

    const runnableItems = queueItems
      .filter((item) => canStartUpload(item, now))
      .slice(0, DEFAULT_UPLOAD_CONCURRENCY - activeUploads);

    if (runnableItems.length === 0) {
      if (nextRetryAt && nextRetryAt > now) {
        scheduleRunner(nextRetryAt - now);
      }
      return;
    }

    runnableItems.forEach((item) => {
      if (startedUploadsRef.current.has(item.id)) return;
      startedUploadsRef.current.add(item.id);
      void uploadQueueItem(item.id);
    });
  }, [
    confirmedSettings,
    isPaused,
    isRestoring,
    queueItems,
    queueStorageKey,
    runnerTick,
    scheduleRunner,
    uploadQueueItem,
  ]);

  const addFilesToQueue = useCallback(
    async (fileList: FileList) => {
      if (!queueStorageKey) return;

      const validFiles = Array.from(fileList).filter((file) => {
        const fileType = getFileType(file.name);
        const fileSizeInMB = file.size / 1024 / 1024;

        if (fileSizeInMB > MAX_FILE_SIZE_MB) {
          message.error(
            `${file.name} exceeds the ${MAX_FILE_SIZE_MB} MB upload limit.`
          );
          return false;
        }

        if (!fileType) {
          message.warning(`${file.name} has no detectable file extension.`);
        }

        return true;
      });

      if (validFiles.length === 0) return;

      try {
        setIsPaused((current) =>
          current || queueItemsRef.current.every((item) => isTerminalStatus(item.status))
            ? true
            : current
        );

        const newItems = validFiles.map<ImportQueueItem>((file) => ({
          id: createQueueItemId(),
          fileName: file.name,
          size: file.size,
          type: file.type,
          lastModified: file.lastModified,
          addedOn: new Date().toISOString(),
          status: "queued",
          progress: 0,
          retryCount: 0,
          retryAt: null,
          lastError: null,
          associatedCave: null,
          failureCode: null,
          isRetryable: false,
          requestId: null,
          result: null,
          file,
        }));

        await Promise.all(
          newItems.map((item) =>
            ImportQueueStorage.putFile(queueStorageKey, item.id, item.file as File)
          )
        );

        setQueueItems((items) => [...items, ...newItems]);
        hasNotifiedCompletionRef.current = false;
      } catch {
        setIsPaused((current) =>
          current || queueItemsRef.current.every((item) => isTerminalStatus(item.status))
            ? true
            : current
        );

        const fallbackItems = validFiles.map<ImportQueueItem>((file) => ({
          id: createQueueItemId(),
          fileName: file.name,
          size: file.size,
          type: file.type,
          lastModified: file.lastModified,
          addedOn: new Date().toISOString(),
          status: "queued",
          progress: 0,
          retryCount: 0,
          retryAt: null,
          lastError: null,
          associatedCave: null,
          failureCode: null,
          isRetryable: false,
          requestId: null,
          result: null,
          file,
        }));

        setQueueItems((items) => [...items, ...fallbackItems]);
      }
    },
    [queueStorageKey]
  );

  const retryFailed = useCallback(() => {
    hasNotifiedCompletionRef.current = false;
    setQueueItems((items) =>
      items.map((item) =>
        item.status === "failed" || item.status === "canceled"
          ? {
            ...item,
            status: item.file ? ("queued" as const) : ("canceled" as const),
            progress: 0,
            retryCount: 0,
            retryAt: null,
            lastError: item.file
              ? null
              : "The queued file is no longer available. Re-add it to continue.",
            failureCode: null,
            isRetryable: false,
            requestId: null,
            result: null,
          }
          : item
      )
    );
  }, []);

  const handleFileSelect = async (
    event: React.ChangeEvent<HTMLInputElement>
  ) => {
    if (!event.target.files) return;
    await addFilesToQueue(event.target.files);
    event.target.value = "";
  };

  const handleDrop = async (event: React.DragEvent<HTMLElement>) => {
    event.preventDefault();
    setIsDragActive(false);
    if (!event.dataTransfer.files) return;
    await addFilesToQueue(event.dataTransfer.files);
  };

  const handleDragOver = (event: React.DragEvent<HTMLElement>) => {
    event.preventDefault();
    if (event.dataTransfer.types.includes("Files")) {
      setIsDragActive(true);
      event.dataTransfer.dropEffect = "copy";
    }
  };

  const handleDragEnter = (event: React.DragEvent<HTMLElement>) => {
    event.preventDefault();
    if (event.dataTransfer.types.includes("Files")) {
      setIsDragActive(true);
    }
  };

  const handleDragLeave = (event: React.DragEvent<HTMLElement>) => {
    event.preventDefault();
    if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
      setIsDragActive(false);
    }
  };

  const handleResetSettings = useCallback(async () => {
    await resetQueueState();
    setInputsConfirmed(false);
    setConfirmedSettings(null);
    form.resetFields();
  }, [form, resetQueueState]);

  const fileResults = useMemo(
    () =>
      queueItems
        .filter((item) => item.status !== "queued" && item.status !== "uploading")
        .map(createCsvRow),
    [queueItems]
  );

  const queueStats = useMemo(() => {
    const uploaded = queueItems.filter((item) => item.status === "uploaded").length;
    const failed = queueItems.filter((item) => item.status === "failed").length;
    const canceled = queueItems.filter((item) => item.status === "canceled").length;
    const uploading = queueItems.filter((item) => item.status === "uploading").length;
    const queued = queueItems.filter(
      (item) => item.status === "queued" || item.status === "retry_wait"
    ).length;
    const remaining = queued + uploading;

    return { uploaded, failed, canceled, uploading, queued, remaining };
  }, [queueItems]);

  const activeItems = useMemo(
    () =>
      queueItems.filter(
        (item) => item.status === "uploading" || item.status === "retry_wait"
      ),
    [queueItems]
  );

  const uploadNowItems = useMemo(() => {
    const uploadingItems = queueItems.filter((item) => item.status === "uploading");
    const queuedItems = queueItems.filter((item) => item.status === "queued");
    const retryWaitItems = queueItems.filter((item) => item.status === "retry_wait");

    return [...uploadingItems, ...queuedItems, ...retryWaitItems].slice(
      0,
      RECENT_ACTIVITY_LIMIT
    );
  }, [queueItems]);

  const { currentUploadItems, waitingUploadItems } = useMemo(() => {
    const currentSlotCount = Math.max(
      DEFAULT_UPLOAD_CONCURRENCY,
      queueStats.uploading
    );
    const currentItems = uploadNowItems.filter(
      (item, index) =>
        item.status === "uploading" ||
        (item.status === "queued" && index < currentSlotCount)
    );
    const waitingItems = uploadNowItems.filter(
      (item) => !currentItems.some((currentItem) => currentItem.id === item.id)
    );

    return {
      currentUploadItems: currentItems,
      waitingUploadItems: waitingItems,
    };
  }, [queueStats.uploading, uploadNowItems]);

  const failedItems = useMemo(
    () =>
      queueItems.filter(
        (item) => item.status === "failed" || item.status === "canceled"
      ),
    [queueItems]
  );

  const recentActivity = useMemo(
    () =>
      queueItems
        .filter(
          (item) =>
            item.status === "uploaded" ||
            item.status === "failed" ||
            item.status === "canceled"
        )
        .slice(-RECENT_ACTIVITY_LIMIT)
        .reverse(),
    [queueItems]
  );

  const aggregateProgress = useMemo(() => {
    if (queueItems.length === 0) return 0;

    const totalBytes = queueItems.reduce((sum, item) => sum + item.size, 0);
    if (totalBytes === 0) return 0;

    const completedBytes = queueItems.reduce((sum, item) => {
      if (item.status === "uploaded") return sum + item.size;
      if (item.status === "uploading") {
        return sum + (item.size * item.progress) / 100;
      }

      return sum;
    }, 0);

    return Math.min(100, Math.round((100 * completedBytes) / totalBytes));
  }, [queueItems]);

  const allWorkComplete =
    queueItems.length > 0 &&
    queueItems.every((item) => isTerminalStatus(item.status));
  const hasSuccessfulUploads = queueStats.uploaded > 0;
  useEffect(() => {
    if (!allWorkComplete || !hasSuccessfulUploads || hasNotifiedCompletionRef.current) {
      return;
    }

    setIsPaused(true);
    hasNotifiedCompletionRef.current = true;
    onUploaded();
  }, [allWorkComplete, hasSuccessfulUploads, onUploaded]);

  const pauseUploads = useCallback(() => {
    setIsPaused(true);
    uploadAbortControllersRef.current.forEach((controller) =>
      controller.abort()
    );
    uploadAbortControllersRef.current.clear();
  }, []);

  const uploadControl = useMemo(() => {
    const canPause = queueStats.uploading > 0 && !isPaused;
    const canStart = isPaused && queueStats.queued > 0;
    const hasPriorActivity = queueStats.uploaded > 0 || failedItems.length > 0;

    if (canPause) {
      return {
        label: "Pause",
        icon: <PauseCircleOutlined />,
        disabled: isRestoring,
        onClick: pauseUploads,
      };
    }

    return {
      label: hasPriorActivity ? "Resume" : "Start Upload",
      icon: <PlayCircleOutlined />,
      disabled: isRestoring || !canStart,
      onClick: () => setIsPaused(false),
    };
  }, [
    failedItems.length,
    isPaused,
    isRestoring,
    pauseUploads,
    queueStats.queued,
    queueStats.uploaded,
    queueStats.uploading,
  ]);

  return (
    <>
      {!inputsConfirmed && (
        <Card
          className="planarian-import-info-card"
          style={{ width: "100%" }}
        >
          <Typography.Title level={4}>File Import Settings</Typography.Title>
          <Typography.Paragraph>
            Tell Planarian how to find the cave id in each filename.
          </Typography.Paragraph>
          <Typography.Paragraph>
            Example: for a file like <strong>BE31_PumphouseCave.pdf</strong>,
            leave <strong>Delimiter</strong> blank and use{" "}
            <strong>{`(?i)^[A-Z]{2}\\d+`}</strong>.
          </Typography.Paragraph>
          <Form
            form={form}
            layout="vertical"
            onFinish={(values) => {
              setConfirmedSettings({
                delimiter: values.delimiter ?? "",
                idRegex: values.idRegex,
                ignoreDuplicates: values.ignoreDuplicates ?? true,
              });
              setInputsConfirmed(true);
            }}
          >
            <Row gutter={16}>
              <Col span={8}>
                <Form.Item
                  label="Delimiter"
                  name={nameof<DelimiterFormFields>("delimiter")}
                  initialValue=""
                  extra="Leave blank when the county code and cave number are together."
                >
                  <Input placeholder="-" />
                </Form.Item>
              </Col>
              <Col span={8}>
                <Form.Item
                  label="ID Regex"
                  name={nameof<DelimiterFormFields>("idRegex")}
                  rules={[
                    { required: true, message: "Please input an ID regex." },
                  ]}
                  extra="Match the cave id at the start of the filename."
                >
                  <Input placeholder="\\d+-\\d+" />
                </Form.Item>
              </Col>
              <Col span={8}>
                <Form.Item
                  name={nameof<DelimiterFormFields>("ignoreDuplicates")}
                  label="Ignore Duplicates"
                  valuePropName="checked"
                  initialValue={true}
                >
                  <Checkbox />
                </Form.Item>
              </Col>
            </Row>
            <Form.Item>
              <Button type="primary" htmlType="submit">
                Confirm Settings
              </Button>
            </Form.Item>
          </Form>
        </Card>
      )}

      {inputsConfirmed && (
        <div
          className={`import-files-dashboard ${isDragActive ? "import-files-dashboard--drag-active" : ""
            }`}
          onDrop={(event) => {
            void handleDrop(event);
          }}
          onDragOver={handleDragOver}
          onDragEnter={handleDragEnter}
          onDragLeave={handleDragLeave}
        >
          <input
            id="import-files-input"
            type="file"
            multiple
            style={{ display: "none" }}
            onChange={(event) => {
              void handleFileSelect(event);
            }}
          />

          <div className="import-files-dashboard__controls import-step-surface import-step-card">
            <div
              className="import-files-dashboard__controls-actions"
              ref={toolbarContainerRef}
            >
              <div
                className={`import-files-dashboard__toolbar-actions import-files-dashboard__toolbar-actions--collapse-${collapsedToolbarActionsCount}`}
                ref={toolbarActionsRef}
              >
                <Button
                  className="import-files-dashboard__toolbar-primary"
                  data-toolbar-action-index="0"
                  icon={<FileAddOutlined />}
                  onClick={() =>
                    document.getElementById("import-files-input")?.click()
                  }
                >
                  Add Files
                </Button>
                <Button
                  className="import-files-dashboard__toolbar-secondary import-files-dashboard__toolbar-secondary--upload-control"
                  data-toolbar-action-index="1"
                  icon={uploadControl.icon}
                  onClick={uploadControl.onClick}
                  disabled={uploadControl.disabled}
                >
                  {uploadControl.label}
                </Button>
                <Button
                  className="import-files-dashboard__toolbar-secondary import-files-dashboard__toolbar-secondary--retry"
                  data-toolbar-action-index="2"
                  icon={<RedoOutlined />}
                  onClick={retryFailed}
                  disabled={isRestoring || failedItems.length === 0}
                >
                  Retry Failed
                </Button>
                <Button
                  className="import-files-dashboard__toolbar-secondary import-files-dashboard__toolbar-secondary--results"
                  data-toolbar-action-index="3"
                  icon={<EyeOutlined />}
                  onClick={() => setIsModalOpen(true)}
                  disabled={fileResults.length === 0}
                >
                  View Results
                </Button>
                <Button
                  className="import-files-dashboard__toolbar-secondary import-files-dashboard__toolbar-secondary--reset"
                  data-toolbar-action-index="4"
                  icon={<ClearOutlined />}
                  onClick={() => void handleResetSettings()}
                >
                  Reset
                </Button>
                <Dropdown
                  className="import-files-dashboard__toolbar-more"
                  overlayClassName="import-files-dashboard__toolbar-menu"
                  trigger={["click"]}
                  menu={{
                    items: [
                      ...(collapsedToolbarActionsCount >= 5
                        ? [
                          {
                            key: "add",
                            icon: <FileAddOutlined />,
                            label: "Add Files",
                            disabled: isRestoring,
                          },
                        ]
                        : []),
                      ...(collapsedToolbarActionsCount >= 4
                        ? [
                          {
                            key: "upload-control",
                            icon: uploadControl.icon,
                            label: uploadControl.label,
                            disabled: uploadControl.disabled,
                          },
                        ]
                        : []),
                      ...(collapsedToolbarActionsCount >= 3
                        ? [
                          {
                            key: "retry",
                            icon: <RedoOutlined />,
                            label: "Retry Failed",
                            disabled: isRestoring || failedItems.length === 0,
                          },
                        ]
                        : []),
                      ...(collapsedToolbarActionsCount >= 2
                        ? [
                          {
                            key: "results",
                            icon: <EyeOutlined />,
                            label: "View Results",
                            disabled: fileResults.length === 0,
                          },
                        ]
                        : []),
                      ...(collapsedToolbarActionsCount >= 1
                        ? [
                          {
                            key: "reset",
                            icon: <ClearOutlined />,
                            label: "Reset",
                            disabled: isRestoring,
                          },
                        ]
                        : []),
                    ],
                    onClick: ({ key }) => {
                      if (key === "add") {
                        document.getElementById("import-files-input")?.click();
                        return;
                      }

                      if (key === "upload-control") {
                        uploadControl.onClick();
                        return;
                      }

                      if (key === "retry") {
                        retryFailed();
                        return;
                      }

                      if (key === "results") {
                        setIsModalOpen(true);
                        return;
                      }

                      if (key === "reset") {
                        void handleResetSettings();
                      }
                    },
                  }}
                >
                  <Button
                    data-toolbar-more
                    icon={<DownOutlined />}
                    aria-label="More file upload actions"
                  >
                    {collapsedToolbarActionsCount >= 5 ? "Actions" : "More"}
                  </Button>
                </Dropdown>
              </div>
              <div
                className="import-files-dashboard__metrics import-files-dashboard__metrics--toolbar"
                ref={toolbarStatsRef}
              >
                <div className="import-step-surface import-files-dashboard__metric import-files-dashboard__metric--compact">
                  <span className="import-files-dashboard__metric-label">
                    Added
                  </span>
                  <span className="import-files-dashboard__metric-value">
                    {queueItems.length}
                  </span>
                </div>
                <div className="import-step-surface import-files-dashboard__metric import-files-dashboard__metric--compact">
                  <span className="import-files-dashboard__metric-label">
                    Uploaded
                  </span>
                  <span className="import-files-dashboard__metric-value">
                    {queueStats.uploaded}
                  </span>
                </div>
                <div className="import-step-surface import-files-dashboard__metric import-files-dashboard__metric--compact">
                  <span className="import-files-dashboard__metric-label">
                    Failed
                  </span>
                  <span className="import-files-dashboard__metric-value">
                    {failedItems.length}
                  </span>
                </div>
              </div>
            </div>

          </div>

          <div className="import-files-dashboard__body">
            <div className="import-files-dashboard__body-grid">
              <div className="import-files-dashboard__pane-card import-step-surface">
                <div className="import-files-dashboard__pane-header">
                  <div className="import-files-dashboard__pane-header-main">
                    <Typography.Title level={4}>Uploading Now</Typography.Title>
                    <Typography.Text
                      type="secondary"
                      className="import-files-dashboard__queued-count"
                    >
                      {queueStats.remaining} remaining
                    </Typography.Text>
                  </div>
                  <div className="import-files-dashboard__pane-summary">
                    <div className="import-files-dashboard__queue-progress import-files-dashboard__queue-progress--active import-files-dashboard__queue-progress--overall">
                      <span
                        className="import-files-dashboard__queue-progress-fill"
                        style={{ width: `${aggregateProgress}%` }}
                      />
                      <span className="import-files-dashboard__queue-progress-content">
                        <span>Overall progress</span>
                        <span>{aggregateProgress}%</span>
                      </span>
                    </div>
                  </div>
                </div>
                <div className="import-files-dashboard__pane-scroll">
                  {uploadNowItems.length === 0 ? (
                    <div className="import-files-dashboard__placeholder-state">
                      <div className="import-files-dashboard__queue-placeholder">
                        <div className="import-files-dashboard__queue-placeholder-line" />
                        <div className="import-files-dashboard__queue-progress import-files-dashboard__queue-progress--placeholder" />
                      </div>
                      <Typography.Text
                        type="secondary"
                        className="import-files-dashboard__queue-placeholder-copy"
                      >
                        Add files to get started.
                      </Typography.Text>
                    </div>
                  ) : (
                    <div className="import-files-dashboard__upload-now-content">
                      <div className="import-files-dashboard__current-list">
                        {currentUploadItems.map((item) => {
                          const isActive = item.status === "uploading";
                          const progressPercent = isActive ? item.progress : 0;

                          return (
                            <div
                              key={item.id}
                              className={`import-files-dashboard__queue-card import-files-dashboard__queue-card--current${isActive
                                ? " import-files-dashboard__queue-card--active"
                                : ""
                                }`}
                            >
                              <Typography.Text
                                strong
                                ellipsis
                                className="import-files-dashboard__queue-file-name"
                              >
                                {item.fileName}
                              </Typography.Text>
                              <div
                                className={`import-files-dashboard__queue-progress${isActive
                                  ? " import-files-dashboard__queue-progress--active"
                                  : ""
                                  }`}
                              >
                                {isActive && (
                                  <span
                                    className="import-files-dashboard__queue-progress-fill"
                                    style={{ width: `${progressPercent}%` }}
                                  />
                                )}
                                <span className="import-files-dashboard__queue-progress-content">
                                  <span>
                                    {isActive
                                      ? `${progressPercent}%`
                                      : formatFileSize(item.size)}
                                  </span>
                                  {isActive && <span>{formatFileSize(item.size)}</span>}
                                </span>
                              </div>
                            </div>
                          );
                        })}
                      </div>
                      {waitingUploadItems.length > 0 && (
                        <>
                          <div className="import-files-dashboard__queue-divider">
                            Waiting files
                          </div>
                          <div className="import-files-dashboard__waiting-list">
                            {waitingUploadItems.map((item) => {
                              const isRetrying = item.status === "retry_wait";

                              return (
                                <div
                                  key={item.id}
                                  className="import-files-dashboard__queue-card import-files-dashboard__queue-card--waiting"
                                >
                                  <Typography.Text
                                    strong
                                    ellipsis
                                    className="import-files-dashboard__queue-file-name"
                                  >
                                    {item.fileName}
                                  </Typography.Text>
                                  <div className="import-files-dashboard__queue-progress">
                                    <span className="import-files-dashboard__queue-progress-content">
                                      <span>{formatFileSize(item.size)}</span>
                                    </span>
                                  </div>
                                  {isRetrying && item.retryAt && (
                                    <Typography.Text
                                      type="secondary"
                                      className="import-files-dashboard__queue-note"
                                    >
                                      <ClockCircleOutlined /> Trying again at{" "}
                                      {new Date(item.retryAt).toLocaleTimeString()}
                                    </Typography.Text>
                                  )}
                                </div>
                              );
                            })}
                          </div>
                        </>
                      )}
                    </div>
                  )}
                </div>
              </div>
              <div className="import-files-dashboard__pane-card import-step-surface">
                <div className="import-files-dashboard__pane-header">
                  <Typography.Title level={4}>Recent Activity</Typography.Title>
                  <Typography.Text type="secondary">
                    Last {RECENT_ACTIVITY_LIMIT}
                  </Typography.Text>
                </div>
                <div className="import-files-dashboard__pane-scroll">
                  {recentActivity.length === 0 ? (
                    <div className="import-files-dashboard__placeholder-state">
                      <div className="import-files-dashboard__queue-placeholder">
                        <div className="import-files-dashboard__queue-placeholder-line" />
                        <div className="import-files-dashboard__queue-progress import-files-dashboard__queue-progress--placeholder" />
                      </div>
                      <Typography.Paragraph
                        type="secondary"
                        className="import-files-dashboard__queue-placeholder-copy"
                      >
                        Finished files and problem files will appear here as the upload runs.
                      </Typography.Paragraph>
                    </div>
                  ) : (
                    recentActivity.map((item) => (
                      <div
                        key={item.id}
                        className={`import-files-dashboard__queue-card import-files-dashboard__queue-card--recent${item.status === "uploaded"
                          ? " import-files-dashboard__queue-card--success"
                          : " import-files-dashboard__queue-card--failure"
                          }`}
                      >
                        <Typography.Text
                          strong
                          ellipsis
                          className="import-files-dashboard__queue-file-name"
                        >
                          {item.fileName}
                        </Typography.Text>
                        <div
                          className={`import-files-dashboard__queue-progress${item.status === "uploaded"
                            ? " import-files-dashboard__queue-progress--success"
                            : " import-files-dashboard__queue-progress--failure"
                            }`}
                        >
                          <span className="import-files-dashboard__queue-progress-fill" />
                          <span className="import-files-dashboard__queue-progress-content">
                            <span>
                              {item.status === "uploaded" ? "Uploaded" : "Needs attention"}
                            </span>
                            <span>{formatFileSize(item.size)}</span>
                          </span>
                        </div>
                        {item.associatedCave && (
                          <div>
                            <Typography.Text type="secondary">
                              Cave: {item.associatedCave}
                            </Typography.Text>
                          </div>
                        )}
                        {item.lastError && (
                          <div>
                            <Typography.Text type="danger">
                              {item.lastError}
                            </Typography.Text>
                          </div>
                        )}
                      </div>
                    ))
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      <PlanarianModal
        fullScreen
        header="File Import Results"
        open={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        footer={null}
      >
        <CSVDisplay data={Papa.unparse(fileResults)} />
      </PlanarianModal>
    </>
  );
};
