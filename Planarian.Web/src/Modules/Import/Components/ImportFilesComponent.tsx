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
import axios from "axios";
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
const MAX_UPLOAD_CONCURRENCY_WEIGHT = 3;
const MAX_RETRY_COUNT = 5;
const DISPATCH_DELAY_MS = 0;
const RECENT_ACTIVITY_LIMIT = 12;
const FILE_UPLOAD_CHUNK_SIZE = 32 * 1024 * 1024;
const SMALL_FILE_CONCURRENCY_THRESHOLD_MB = 30;
const LARGE_FILE_CONCURRENCY_THRESHOLD_MB = 100;
const FILE_PREPARATION_BATCH_SIZE = 100;
const FILE_PREPARATION_PROGRESS_STEP = 25;
const FILE_RESET_PROGRESS_STEP = 25;
const LOCAL_STORAGE_VERSION = 1;
const LOCAL_STORAGE_PREFIX = "planarian-import-files-queue";
const SETTINGS_STORAGE_PREFIX = "planarian-import-files-settings";

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
  sessionId?: string | null;
  uploadedBytes?: number;
  liveUploadedBytes?: number;
  displayUploadedBytes?: number;
  totalBytes?: number;
  isCurrentUploadSlot?: boolean;
  transportStatus?: "chunk_uploading" | "finalizing" | null;
  file?: File | null;
}

interface PersistedImportQueueItem
  extends Omit<
    ImportQueueItem,
    | "file"
    | "sessionId"
    | "uploadedBytes"
    | "liveUploadedBytes"
    | "displayUploadedBytes"
    | "totalBytes"
    | "isCurrentUploadSlot"
    | "transportStatus"
  > { }

interface PersistedImportQueueState {
  version: number;
  isPaused: boolean;
  hasStartedUploadRun?: boolean;
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

const createSettingsStorageKey = (accountId: string) =>
  `${SETTINGS_STORAGE_PREFIX}::${accountId}`;

const isTerminalStatus = (status: ImportQueueItemStatus) =>
  status === "uploaded" || status === "failed" || status === "canceled";

const getPositiveNumber = (...values: Array<number | undefined | null>) =>
  values.find((value): value is number => typeof value === "number" && value > 0) ??
  0;

const canStartUpload = (item: ImportQueueItem, now: number) =>
  (item.status === "queued" ||
    (item.status === "retry_wait" && (item.retryAt ?? 0) <= now)) &&
  !!item.file;

const getDisplayTotalBytes = (item: ImportQueueItem) => {
  return getPositiveNumber(item.totalBytes, item.size, item.file?.size);
};

const getAcknowledgedUploadedBytes = (item: ImportQueueItem) => {
  const totalBytes = getDisplayTotalBytes(item);
  if (totalBytes <= 0) {
    return 0;
  }

  if (typeof item.uploadedBytes !== "number" || item.uploadedBytes <= 0) {
    return 0;
  }

  return Math.min(item.uploadedBytes, totalBytes);
};

const hasPartialUploadProgress = (item: ImportQueueItem) => {
  const totalBytes = getDisplayTotalBytes(item);
  const uploadedBytes = getAcknowledgedUploadedBytes(item);

  return totalBytes > 0 && uploadedBytes > 0 && uploadedBytes < totalBytes;
};

const getStoredVisualUploadedBytes = (item: ImportQueueItem, totalBytes: number) => {
  if (totalBytes <= 0) {
    return 0;
  }

  const storedDisplayBytes =
    typeof item.displayUploadedBytes === "number" && item.displayUploadedBytes > 0
      ? item.displayUploadedBytes
      : 0;
  const liveBytes =
    typeof item.liveUploadedBytes === "number" && item.liveUploadedBytes > 0
      ? item.liveUploadedBytes
      : 0;
  const progressBytes =
    typeof item.progress === "number" && item.progress > 0
      ? Math.round((totalBytes * item.progress) / 100)
      : 0;

  return Math.min(totalBytes, Math.max(storedDisplayBytes, liveBytes, progressBytes));
};

const getUploadDisplayState = (item: ImportQueueItem) => {
  const totalBytes = getDisplayTotalBytes(item);
  const acknowledgedBytes =
    item.status === "uploaded" ? totalBytes : getAcknowledgedUploadedBytes(item);
  const liveUploadedBytes =
    item.status === "uploaded"
      ? totalBytes
      : Math.max(acknowledgedBytes, getStoredVisualUploadedBytes(item, totalBytes));
  const displayUploadedBytes =
    item.status === "uploaded"
      ? totalBytes
      : Math.max(acknowledgedBytes, liveUploadedBytes);
  const acknowledgedPercent = toProgressPercent(acknowledgedBytes, totalBytes);
  const rawDisplayPercent = toProgressPercent(displayUploadedBytes, totalBytes);
  const displayPercent =
    item.status === "uploaded"
      ? 100
      : displayUploadedBytes > 0
        ? Math.min(99, Math.max(1, rawDisplayPercent))
        : 0;
  const hasProgress = displayUploadedBytes > 0 && totalBytes > 0;

  return {
    totalBytes,
    acknowledgedBytes,
    liveUploadedBytes,
    displayUploadedBytes,
    acknowledgedPercent,
    displayPercent,
    hasProgress,
    sizeLabel: formatFileSize(totalBytes),
  };
};

const getDisplayProgressPercent = (item: ImportQueueItem) => {
  if (item.status === "uploaded") {
    return 100;
  }

  return getUploadDisplayState(item).displayPercent;
};

const isPausedCurrentSlotItem = (item: ImportQueueItem) =>
  (item.status === "queued" || item.status === "retry_wait") &&
  !!item.isCurrentUploadSlot;

const getUploadWeight = (size: number) => {
  const sizeInMb = size / 1024 / 1024;
  if (sizeInMb > LARGE_FILE_CONCURRENCY_THRESHOLD_MB) {
    return 3;
  }

  if (sizeInMb > SMALL_FILE_CONCURRENCY_THRESHOLD_MB) {
    return 2;
  }

  return 1;
};

const sortQueueItemsByAddedOrder = (items: ImportQueueItem[]) =>
  [...items].sort(
    (left, right) =>
      left.addedOn.localeCompare(right.addedOn) ||
      left.id.localeCompare(right.id)
  );

const uniqueQueueItemsById = (items: ImportQueueItem[]) => {
  const seenIds = new Set<string>();
  return items.filter((item) => {
    if (seenIds.has(item.id)) {
      return false;
    }

    seenIds.add(item.id);
    return true;
  });
};

const deriveOrderedQueueView = (items: ImportQueueItem[], now: number) => {
  const orderedItems = sortQueueItemsByAddedOrder(items);
  const activeItems = orderedItems.filter((item) => item.status === "uploading");
  const activeWeight = activeItems.reduce(
    (sum, item) => sum + getUploadWeight(getDisplayTotalBytes(item)),
    0
  );
  let remainingWeightBudget = Math.max(
    0,
    MAX_UPLOAD_CONCURRENCY_WEIGHT - activeWeight
  );

  const pausedResumableItems = orderedItems.filter(
    (item) =>
      (item.status === "queued" || item.status === "retry_wait") &&
      (isPausedCurrentSlotItem(item) || hasPartialUploadProgress(item))
  );
  const readyQueuedItems = orderedItems.filter(
    (item) =>
      (item.status === "queued" || item.status === "retry_wait") &&
      !isPausedCurrentSlotItem(item) &&
      !hasPartialUploadProgress(item)
  );
  const runnableCandidates = orderedItems.filter(
    (item) =>
      item.status === "queued" ||
      (item.status === "retry_wait" && (item.retryAt ?? 0) <= now)
  );
  const runnableItems: ImportQueueItem[] = [];
  for (const item of runnableCandidates) {
    if (!canStartUpload(item, now)) {
      continue;
    }

    const itemWeight = getUploadWeight(getDisplayTotalBytes(item));
    if (itemWeight > remainingWeightBudget) {
      break;
    }

    runnableItems.push(item);
    remainingWeightBudget -= itemWeight;

    if (remainingWeightBudget <= 0) {
      break;
    }
  }

  const visibleCurrentReadyItems = readyQueuedItems.slice(0, runnableItems.length);
  const visibleCurrentItems = uniqueQueueItemsById([
    ...activeItems,
    ...pausedResumableItems,
    ...visibleCurrentReadyItems,
  ]);
  const visibleCurrentItemIds = new Set(visibleCurrentItems.map((item) => item.id));
  const waitingItems = orderedItems.filter(
    (item) =>
      (item.status === "queued" || item.status === "retry_wait") &&
      !visibleCurrentItemIds.has(item.id)
  );

  return {
    orderedItems,
    runnableItems,
    activeItems,
    pausedResumableItems,
    readyQueuedItems,
    currentUploadItems: visibleCurrentItems,
    waitingUploadItems: waitingItems,
  };
};

const deriveOrderedQueueDisplayView = (items: ImportQueueItem[]) => {
  const orderedItems = sortQueueItemsByAddedOrder(items);
  const activeItems = orderedItems.filter((item) => item.status === "uploading");
  const pausedResumableItems = orderedItems.filter(
    (item) =>
      (item.status === "queued" || item.status === "retry_wait") &&
      (isPausedCurrentSlotItem(item) || hasPartialUploadProgress(item))
  );
  const currentSlotWeight = [...activeItems, ...pausedResumableItems].reduce(
    (sum, item) => sum + getUploadWeight(getDisplayTotalBytes(item)),
    0
  );
  let remainingWeightBudget = Math.max(
    0,
    MAX_UPLOAD_CONCURRENCY_WEIGHT - currentSlotWeight
  );

  const readyQueuedItems = orderedItems.filter(
    (item) =>
      (item.status === "queued" || item.status === "retry_wait") &&
      !isPausedCurrentSlotItem(item) &&
      !hasPartialUploadProgress(item)
  );
  const visibleCurrentReadyItems: ImportQueueItem[] = [];
  for (const item of readyQueuedItems) {
    const itemWeight = getUploadWeight(getDisplayTotalBytes(item));
    if (itemWeight > remainingWeightBudget) {
      break;
    }

    visibleCurrentReadyItems.push(item);
    remainingWeightBudget -= itemWeight;

    if (remainingWeightBudget <= 0) {
      break;
    }
  }

  const currentUploadItems = uniqueQueueItemsById([
    ...activeItems,
    ...pausedResumableItems,
    ...visibleCurrentReadyItems,
  ]);
  const currentItemIds = new Set(currentUploadItems.map((item) => item.id));

  return {
    orderedItems,
    currentUploadItems,
    waitingUploadItems: orderedItems.filter(
      (item) =>
        (item.status === "queued" || item.status === "retry_wait") &&
        !currentItemIds.has(item.id)
    ),
  };
};

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

const isAbortLikeError = (error: unknown) => {
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
  progress: getUploadDisplayState(item).displayPercent,
  retryCount: item.retryCount,
  retryAt: item.retryAt ?? null,
  lastError: item.lastError ?? null,
  associatedCave: item.associatedCave ?? null,
  failureCode: item.failureCode ?? null,
  isRetryable: item.isRetryable,
  requestId: item.requestId ?? null,
  result: item.result ?? null,
});

const waitForNextPaint = async () => {
  await new Promise<void>((resolve) => {
    window.requestAnimationFrame(() => resolve());
  });
};

const normalizeQueueItem = (item: ImportQueueItem): ImportQueueItem => {
  const normalizedSize = getPositiveNumber(item.size, item.totalBytes, item.file?.size);
  const normalizedTotalBytes = getPositiveNumber(
    item.totalBytes,
    item.size,
    item.file?.size
  );
  const normalizedUploadedBytes =
    typeof item.uploadedBytes === "number" && item.uploadedBytes > 0
      ? Math.min(item.uploadedBytes, normalizedTotalBytes)
      : 0;
  const normalizedLiveUploadedBytes =
    typeof item.liveUploadedBytes === "number" && item.liveUploadedBytes > 0
      ? Math.min(item.liveUploadedBytes, normalizedTotalBytes)
      : 0;
  const normalizedDisplayUploadedBytes =
    typeof item.displayUploadedBytes === "number" &&
      item.displayUploadedBytes > 0
      ? Math.min(item.displayUploadedBytes, normalizedTotalBytes)
      : 0;

  return {
    ...item,
    size: normalizedSize,
    totalBytes: normalizedTotalBytes,
    uploadedBytes: normalizedUploadedBytes,
    liveUploadedBytes: Math.max(normalizedUploadedBytes, normalizedLiveUploadedBytes),
    displayUploadedBytes: Math.max(
      normalizedUploadedBytes,
      normalizedDisplayUploadedBytes
    ),
  };
};

const getSafeTotalBytes = (item: ImportQueueItem, fallbackBytes: number) => {
  const normalizedBytes = getDisplayTotalBytes(item);
  if (normalizedBytes > 0) {
    return normalizedBytes;
  }

  return Math.max(0, fallbackBytes);
};

const toProgressPercent = (uploadedBytes: number, totalBytes: number) => {
  if (totalBytes <= 0) {
    return 0;
  }

  return Math.min(100, Math.max(0, Math.round((100 * uploadedBytes) / totalBytes)));
};

const getSmoothedDisplayBytes = (
  currentDisplayBytes: number,
  targetDisplayBytes: number,
  totalBytes: number
) => {
  if (totalBytes <= 0) {
    return 0;
  }

  const safeCurrent = Math.min(totalBytes, Math.max(0, currentDisplayBytes));
  const safeTarget = Math.min(totalBytes, Math.max(safeCurrent, targetDisplayBytes));
  const remainingBytes = safeTarget - safeCurrent;
  if (remainingBytes <= 0) {
    return safeCurrent;
  }

  const minimumStepBytes = Math.min(512 * 1024, Math.max(64 * 1024, totalBytes * 0.005));
  return Math.min(
    safeTarget,
    safeCurrent + Math.max(minimumStepBytes, remainingBytes * 0.45)
  );
};

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
  const [hasStartedUploadRun, setHasStartedUploadRun] = useState(false);
  const [isRestoring, setIsRestoring] = useState(false);
  const [hasHydratedQueueState, setHasHydratedQueueState] = useState(false);
  const [isResettingQueue, setIsResettingQueue] = useState(false);
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
  const isPausedRef = useRef(true);
  const uploadAbortControllersRef = useRef<Map<string, AbortController>>(
    new Map()
  );
  const filePreparationSessionRef = useRef(0);

  const accountId = AuthenticationService.GetAccountId() ?? "anonymous";
  const settingsStorageKey = useMemo(
    () => createSettingsStorageKey(accountId),
    [accountId]
  );
  const queueStorageKey = useMemo(
    () =>
      confirmedSettings ? createStorageKey(accountId, confirmedSettings) : null,
    [accountId, confirmedSettings]
  );

  useEffect(() => {
    const serializedSettings = localStorage.getItem(settingsStorageKey);
    if (!serializedSettings) {
      setHasHydratedQueueState(true);
      return;
    }

    try {
      const persistedSettings = JSON.parse(serializedSettings) as ImportSettings;
      setConfirmedSettings(persistedSettings);
      setInputsConfirmed(true);
      form.setFieldsValue({
        delimiter: persistedSettings.delimiter,
        idRegex: persistedSettings.idRegex,
        ignoreDuplicates: persistedSettings.ignoreDuplicates,
      });
    } catch {
      localStorage.removeItem(settingsStorageKey);
      setHasHydratedQueueState(true);
    }
  }, [form, settingsStorageKey]);

  useEffect(() => {
    if (!confirmedSettings || !inputsConfirmed) {
      return;
    }

    localStorage.setItem(settingsStorageKey, JSON.stringify(confirmedSettings));
  }, [confirmedSettings, inputsConfirmed, settingsStorageKey]);

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
    isPausedRef.current = isPaused;
  }, [isPaused]);

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
    (items: ImportQueueItem[], paused: boolean, started: boolean) => {
      if (!queueStorageKey) return;

      const payload: PersistedImportQueueState = {
        version: LOCAL_STORAGE_VERSION,
        isPaused: paused,
        hasStartedUploadRun: started,
        items: items.map(serializeQueueItem),
      };

      localStorage.setItem(queueStorageKey, JSON.stringify(payload));
    },
    [queueStorageKey]
  );

  useEffect(() => {
    if (!queueStorageKey || isRestoring || !hasHydratedQueueState) return;
    persistQueueState(queueItems, isPaused, hasStartedUploadRun);
  }, [
    hasHydratedQueueState,
    hasStartedUploadRun,
    isRestoring,
    isPaused,
    persistQueueState,
    queueItems,
    queueStorageKey,
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
        sessionIds.map((sessionId) =>
          AccountService.CancelImportFileUploadSession(sessionId)
        )
      );

      if (queueStorageKey) {
        localStorage.removeItem(queueStorageKey);
        await ImportQueueStorage.clearQueue(queueStorageKey);
      }
      localStorage.removeItem(settingsStorageKey);

      nextDispatchAllowedAtRef.current = 0;
      setQueueItems([]);
      setIsPaused(true);
      setHasStartedUploadRun(false);
      setHasHydratedQueueState(true);
      hasNotifiedCompletionRef.current = false;
      setInputsConfirmed(false);
      setConfirmedSettings(null);
      form.resetFields();
    } finally {
      setIsResettingQueue(false);
    }
  }, [form, queueStorageKey, settingsStorageKey]);

  const loadPersistedQueue = useCallback(async () => {
    if (!queueStorageKey) return;

    setHasHydratedQueueState(false);
    setIsRestoring(true);

    try {
      const serialized = localStorage.getItem(queueStorageKey);
      if (!serialized) {
        setQueueItems([]);
        setIsPaused(true);
        setHasStartedUploadRun(false);
        return;
      }

      const persisted = JSON.parse(serialized) as PersistedImportQueueState;
      const restoredItems = await Promise.all(
        (persisted.items ?? []).map(async (item) => {
          let file: File | null = null;
          const shouldRestoreFile = item.status !== "uploaded";

          if (shouldRestoreFile) {
            file = await ImportQueueStorage.getFile(queueStorageKey, item.id);
          }

          if (!file && shouldRestoreFile) {
            return normalizeQueueItem({
              ...item,
              status: "canceled" as const,
              progress: 0,
              retryAt: null,
              lastError:
                item.lastError ??
                "The queued file could not be restored after refresh. Re-add it to continue.",
              isRetryable: false,
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

          return normalizeQueueItem({
            ...item,
            status:
              item.status === "uploading"
                ? ("queued" as const)
                : item.status,
            progress:
              item.status === "uploading"
                ? 0
                : item.progress,
            retryAt: item.retryAt ?? null,
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

      setQueueItems(restoredItems);
      setIsPaused(persisted.isPaused ?? true);
      setHasStartedUploadRun(
        persisted.hasStartedUploadRun ??
        restoredItems.some((item) => item.status !== "queued")
      );
    } catch {
      setQueueItems([]);
      setIsPaused(true);
      setHasStartedUploadRun(false);
    } finally {
      setIsRestoring(false);
      setHasHydratedQueueState(true);
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

      const nextItem = normalizeQueueItem(update(current));
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
          isCurrentUploadSlot: false,
          file: null,
        }));
        startedUploadsRef.current.delete(itemId);
        return;
      }

      const fileToUpload = file;

      updateQueueItem(itemId, (current) => ({
        ...current,
        status: "uploading",
        progress: Math.max(getUploadDisplayState(current).displayPercent, 1),
        retryAt: null,
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
        transportStatus: current.transportStatus ?? "chunk_uploading",
      }));

      try {
        let currentItem =
          queueItemsRef.current.find((candidate) => candidate.id === itemId) ??
          item;
        let sessionId = currentItem.sessionId ?? null;
        let uploadedBytes = currentItem.uploadedBytes ?? 0;

        if (!sessionId) {
          const session = await AccountService.CreateImportFileUploadSession({
            fileName: fileToUpload.name,
            fileSize: fileToUpload.size,
            delimiterRegex: confirmedSettings.delimiter,
            idRegex: confirmedSettings.idRegex,
            ignoreDuplicates: confirmedSettings.ignoreDuplicates,
            requestId: itemId,
          });

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
              file: fileToUpload,
            }));
            return;
          }

          const offset = uploadedBytes;
          const chunkIndex = Math.floor(offset / FILE_UPLOAD_CHUNK_SIZE);
          const chunk = fileToUpload.slice(
            offset,
            Math.min(offset + FILE_UPLOAD_CHUNK_SIZE, fileToUpload.size)
          );
          let lastProgressUpdateAt = 0;
          const abortController = new AbortController();
          uploadAbortControllersRef.current.set(itemId, abortController);

          const chunkResult = await AccountService.UploadImportFileChunk(
            sessionId,
            chunk,
            chunkIndex,
            offset,
            (event) => {
              const now = Date.now();
              if (
                event.loaded < chunk.size &&
                now - lastProgressUpdateAt < 100
              ) {
                return;
              }

              lastProgressUpdateAt = now;
              const chunkBytes = chunk.size || 1;
              const loaded = Math.min(event.loaded, chunkBytes);
              const overallUploaded = Math.min(
                fileToUpload.size,
                offset + loaded
              );

              updateQueueItem(itemId, (current) => ({
                ...current,
                ...(() => {
                  const nextDisplayUploadedBytes = getSmoothedDisplayBytes(
                    current.displayUploadedBytes ?? current.uploadedBytes ?? 0,
                    overallUploaded,
                    fileToUpload.size
                  );

                  return {
                    liveUploadedBytes: Math.max(
                      current.uploadedBytes ?? 0,
                      overallUploaded
                    ),
                    displayUploadedBytes: nextDisplayUploadedBytes,
                    progress: toProgressPercent(
                      nextDisplayUploadedBytes,
                      fileToUpload.size
                    ),
                  };
                })(),
                totalBytes: getSafeTotalBytes(current, fileToUpload.size),
                isCurrentUploadSlot: true,
                transportStatus: "chunk_uploading",
              }));
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
        const result = await AccountService.FinalizeImportFileUploadSession(
          sessionId,
          finalizeAbortController.signal
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
          const preservedUploadedBytes = currentItem.uploadedBytes ?? 0;
          updateQueueItem(itemId, (current) => ({
            ...current,
            status: "queued",
            progress: getUploadDisplayState(current).displayPercent,
            retryAt: null,
            lastError: null,
            failureCode: null,
            isRetryable: false,
            requestId: null,
            result: null,
            sessionId: current.sessionId ?? null,
            uploadedBytes: preservedUploadedBytes,
            liveUploadedBytes: Math.max(
              preservedUploadedBytes,
              current.liveUploadedBytes ?? 0
            ),
            displayUploadedBytes: Math.max(
              preservedUploadedBytes,
              current.displayUploadedBytes ?? 0
            ),
            totalBytes: getSafeTotalBytes(current, fileToUpload.size),
            isCurrentUploadSlot: true,
            transportStatus: null,
            file: fileToUpload,
          }));
          return;
        }

        const failure = normalizeUploadError(error);
        const nextRetryCount = currentItem.retryCount + 1;

        if (
          failure.failureCode === ApiExceptionType.NotFound &&
          currentSessionId
        ) {
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
            sessionId: current.sessionId ?? null,
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
            isCurrentUploadSlot: false,
            transportStatus: null,
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
            sessionId: null,
            uploadedBytes: 0,
            liveUploadedBytes: 0,
            displayUploadedBytes: 0,
            totalBytes: fileToUpload.size,
            isCurrentUploadSlot: false,
            transportStatus: null,
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

    const now = Date.now();
    const { runnableItems } = deriveOrderedQueueView(queueItems, now);
    const waitForDispatch = nextDispatchAllowedAtRef.current - now;
    if (waitForDispatch > 0) {
      scheduleRunner(waitForDispatch);
      return;
    }

    const nextRetryAt = queueItems
      .filter((item) => item.status === "retry_wait" && item.retryAt)
      .map((item) => item.retryAt as number)
      .sort((left, right) => left - right)[0];

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
      const incomingFiles = Array.from(fileList);
      const validFiles = incomingFiles.filter((file) => {
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

      setIsPaused((current) =>
        current || queueItemsRef.current.every((item) => isTerminalStatus(item.status))
          ? true
          : current
      );

      const addedAt = Date.now();
      const newItems = validFiles.map<ImportQueueItem>((file, index) => ({
        id: createQueueItemId(),
        fileName: file.name,
        size: file.size,
        type: file.type,
        lastModified: file.lastModified,
        addedOn: new Date(addedAt + index).toISOString(),
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
        sessionId: null,
        uploadedBytes: 0,
        liveUploadedBytes: 0,
        displayUploadedBytes: 0,
        totalBytes: file.size,
        isCurrentUploadSlot: false,
        transportStatus: null,
        file,
      }));

      setQueueItems((items) => [...items, ...newItems]);
      hasNotifiedCompletionRef.current = false;

      if (!queueStorageKey) {
        return;
      }

      const preparationSession = filePreparationSessionRef.current + 1;
      filePreparationSessionRef.current = preparationSession;

      void (async () => {
        try {
          await waitForNextPaint();

          const storageKey = queueStorageKey;

          for (
            let startIndex = 0;
            startIndex < newItems.length;
            startIndex += FILE_PREPARATION_BATCH_SIZE
          ) {
            if (
              preparationSession !== filePreparationSessionRef.current
            ) {
              await ImportQueueStorage.clearQueue(storageKey);
              return;
            }

            const batch = newItems.slice(
              startIndex,
              startIndex + FILE_PREPARATION_BATCH_SIZE
            );

            await ImportQueueStorage.putFiles(
              storageKey,
              batch.map((item) => ({
                itemId: item.id,
                file: item.file as File,
              }))
            );

            if (
              preparationSession !== filePreparationSessionRef.current
            ) {
              await ImportQueueStorage.clearQueue(storageKey);
              return;
            }

            await waitForNextPaint();
          }
        } catch { }
      })();
    },
    [queueStorageKey]
  );

  const retryFailed = useCallback(() => {
    hasNotifiedCompletionRef.current = false;
    setIsPaused(true);
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
  }, [resetQueueState]);

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

  const orderedQueueView = useMemo(
    () => deriveOrderedQueueDisplayView(queueItems),
    [queueItems]
  );

  const currentUploadItems = useMemo(
    () => orderedQueueView.currentUploadItems.slice(0, RECENT_ACTIVITY_LIMIT),
    [orderedQueueView]
  );

  const waitingUploadItems = useMemo(
    () =>
      orderedQueueView.waitingUploadItems.slice(
        0,
        Math.max(0, RECENT_ACTIVITY_LIMIT - currentUploadItems.length)
      ),
    [currentUploadItems.length, orderedQueueView]
  );

  const uploadNowItems = useMemo(
    () => [...currentUploadItems, ...waitingUploadItems],
    [currentUploadItems, waitingUploadItems]
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

    const totalBytes = queueItems.reduce(
      (sum, item) => sum + getUploadDisplayState(item).totalBytes,
      0
    );
    if (totalBytes === 0) return 0;

    const completedBytes = queueItems.reduce((sum, item) => {
      const displayState = getUploadDisplayState(item);
      return sum + displayState.displayUploadedBytes;
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

  const startOrResumeUploads = useCallback(() => {
    setHasStartedUploadRun(true);
    setIsPaused(false);
  }, []);

  const uploadControl = useMemo(() => {
    const canPause = !isPaused && queueStats.remaining > 0;
    const canStart = isPaused && queueStats.queued > 0;

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
    isRestoring,
    pauseUploads,
    queueStats.queued,
    queueStats.remaining,
    startOrResumeUploads,
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
          style={{ position: "relative" }}
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
            disabled={isResettingQueue}
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
                  disabled={uploadControl.disabled || isResettingQueue}
                >
                  {uploadControl.label}
                </Button>
                <Button
                  className="import-files-dashboard__toolbar-secondary import-files-dashboard__toolbar-secondary--retry"
                  data-toolbar-action-index="2"
                  icon={<RedoOutlined />}
                  onClick={retryFailed}
                  disabled={isRestoring || isResettingQueue || failedItems.length === 0}
                >
                  Retry Failed
                </Button>
                <Button
                  className="import-files-dashboard__toolbar-secondary import-files-dashboard__toolbar-secondary--results"
                  data-toolbar-action-index="3"
                  icon={<EyeOutlined />}
                  onClick={() => setIsModalOpen(true)}
                  disabled={isResettingQueue || fileResults.length === 0}
                >
                  View Results
                </Button>
                <Button
                  className="import-files-dashboard__toolbar-secondary import-files-dashboard__toolbar-secondary--reset"
                  data-toolbar-action-index="4"
                  icon={<ClearOutlined />}
                  onClick={() => void handleResetSettings()}
                  disabled={isRestoring || isResettingQueue}
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
                            disabled: isRestoring || isResettingQueue,
                          },
                        ]
                        : []),
                      ...(collapsedToolbarActionsCount >= 4
                        ? [
                          {
                            key: "upload-control",
                            icon: uploadControl.icon,
                            label: uploadControl.label,
                            disabled: uploadControl.disabled || isResettingQueue,
                          },
                        ]
                        : []),
                      ...(collapsedToolbarActionsCount >= 3
                        ? [
                          {
                            key: "retry",
                            icon: <RedoOutlined />,
                            label: "Retry Failed",
                            disabled: isRestoring || isResettingQueue || failedItems.length === 0,
                          },
                        ]
                        : []),
                      ...(collapsedToolbarActionsCount >= 2
                        ? [
                          {
                            key: "results",
                            icon: <EyeOutlined />,
                            label: "View Results",
                            disabled: isResettingQueue || fileResults.length === 0,
                          },
                        ]
                        : []),
                      ...(collapsedToolbarActionsCount >= 1
                        ? [
                          {
                            key: "reset",
                            icon: <ClearOutlined />,
                            label: "Reset",
                            disabled: isRestoring || isResettingQueue,
                          },
                        ]
                        : []),
                    ],
                    onClick: ({ key }) => {
                      if (isResettingQueue) {
                        return;
                      }

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
                          const displayState = getUploadDisplayState(item);
                          const progressPercent = displayState.displayPercent;
                          const hasDisplayProgress = displayState.hasProgress;

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
                                {(isActive || hasDisplayProgress) && (
                                  <span
                                    className="import-files-dashboard__queue-progress-fill"
                                    style={{ width: `${progressPercent}%` }}
                                  />
                                )}
                                <span className="import-files-dashboard__queue-progress-content">
                                  <span>
                                    {isActive || hasDisplayProgress
                                      ? `${progressPercent}%`
                                      : displayState.sizeLabel}
                                  </span>
                                  {(isActive || hasDisplayProgress) && (
                                    <span>{displayState.sizeLabel}</span>
                                  )}
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
                              const displayState = getUploadDisplayState(item);

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
                                      <span>{displayState.sizeLabel}</span>
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
                            <span>{getUploadDisplayState(item).sizeLabel}</span>
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
