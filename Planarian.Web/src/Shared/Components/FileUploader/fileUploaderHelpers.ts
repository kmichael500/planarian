import {
  QueuedFileUploadItem,
  QueuedFileUploadStatus,
} from "./types";

export const VIRTUAL_LIST_OVERSCAN = 6;
export const VIRTUAL_QUEUE_ROW_HEIGHT = 132;
export const VIRTUAL_UPLOAD_QUEUE_ROW_HEIGHT = 104;

export const createQueueItemId = () => {
  if (typeof crypto !== "undefined" && crypto.randomUUID) {
    return crypto.randomUUID();
  }

  return `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
};

export const isTerminalStatus = (status: QueuedFileUploadStatus) =>
  status === "uploaded" ||
  status === "skipped" ||
  status === "failed" ||
  status === "canceled";

export const getPositiveNumber = (
  ...values: Array<number | undefined | null>
) =>
  values.find((value): value is number => typeof value === "number" && value > 0) ??
  0;

export const formatFileSize = (bytes: number) => {
  if (bytes <= 0) {
    return "Unknown size";
  }

  if (bytes < 1024) {
    return `${bytes} B`;
  }

  const units = ["KB", "MB", "GB", "TB"];
  let value = bytes / 1024;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }

  const precision = value >= 10 ? 1 : 2;
  return `${value.toFixed(precision)} ${units[unitIndex]}`;
};

export const toProgressPercent = (uploadedBytes: number, totalBytes: number) => {
  if (totalBytes <= 0) {
    return 0;
  }

  return Math.min(100, Math.max(0, Math.round((100 * uploadedBytes) / totalBytes)));
};

export const getDisplayTotalBytes = <TResult,>(
  item: QueuedFileUploadItem<TResult>
) => getPositiveNumber(item.totalBytes, item.size, item.file?.size);

export const getAcknowledgedUploadedBytes = <TResult,>(
  item: QueuedFileUploadItem<TResult>
) => {
  const totalBytes = getDisplayTotalBytes(item);
  if (totalBytes <= 0) {
    return 0;
  }

  if (typeof item.uploadedBytes !== "number" || item.uploadedBytes <= 0) {
    return 0;
  }

  return Math.min(item.uploadedBytes, totalBytes);
};

const getStoredVisualUploadedBytes = <TResult,>(
  item: QueuedFileUploadItem<TResult>,
  totalBytes: number
) => {
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

export const getUploadDisplayState = <TResult,>(
  item: QueuedFileUploadItem<TResult>
) => {
  const totalBytes = getDisplayTotalBytes(item);
  const acknowledgedBytes =
    item.status === "uploaded" || item.status === "skipped"
      ? totalBytes
      : getAcknowledgedUploadedBytes(item);
  const liveUploadedBytes =
    item.status === "uploaded" || item.status === "skipped"
      ? totalBytes
      : Math.max(acknowledgedBytes, getStoredVisualUploadedBytes(item, totalBytes));
  const displayUploadedBytes =
    item.status === "uploaded" || item.status === "skipped"
      ? totalBytes
      : Math.max(acknowledgedBytes, liveUploadedBytes);
  const acknowledgedPercent = toProgressPercent(acknowledgedBytes, totalBytes);
  const rawDisplayPercent = toProgressPercent(displayUploadedBytes, totalBytes);
  const displayPercent =
    item.status === "uploaded" || item.status === "skipped"
      ? 100
      : totalBytes > 0 && displayUploadedBytes >= totalBytes
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
    hasKnownSize: totalBytes > 0,
    sizeLabel: formatFileSize(totalBytes),
  };
};

export const getUploadWeight = (
  size: number,
  smallFileConcurrencyThresholdMb: number,
  largeFileConcurrencyThresholdMb: number
) => {
  const sizeInMb = size / 1024 / 1024;
  if (sizeInMb > largeFileConcurrencyThresholdMb) {
    return 3;
  }

  if (sizeInMb > smallFileConcurrencyThresholdMb) {
    return 2;
  }

  return 1;
};

export const sortQueueItemsByAddedOrder = <TResult,>(
  items: QueuedFileUploadItem<TResult>[]
) =>
  [...items].sort(
    (left, right) =>
      left.queueOrder - right.queueOrder ||
      left.addedOn.localeCompare(right.addedOn) ||
      left.id.localeCompare(right.id)
  );

export const canStartUpload = <TResult,>(
  item: QueuedFileUploadItem<TResult>,
  now: number
) =>
  (item.status === "queued" ||
    (item.status === "retry_wait" && (item.retryAt ?? 0) <= now)) &&
  !!item.file;

export const deriveOrderedQueueView = <TResult,>(
  items: QueuedFileUploadItem<TResult>[],
  now: number,
  maxConcurrencyWeight: number,
  smallFileConcurrencyThresholdMb: number,
  largeFileConcurrencyThresholdMb: number
) => {
  const orderedItems = sortQueueItemsByAddedOrder(items);
  const activeItems = orderedItems.filter((item) => item.status === "uploading");
  const activeWeight = activeItems.reduce(
    (sum, item) =>
      sum +
      getUploadWeight(
        getDisplayTotalBytes(item),
        smallFileConcurrencyThresholdMb,
        largeFileConcurrencyThresholdMb
      ),
    0
  );
  let remainingWeightBudget = Math.max(0, maxConcurrencyWeight - activeWeight);

  const runnableCandidates = orderedItems.filter(
    (item) =>
      item.status === "queued" ||
      (item.status === "retry_wait" && (item.retryAt ?? 0) <= now)
  );
  const runnableItems: QueuedFileUploadItem<TResult>[] = [];
  for (const item of runnableCandidates) {
    if (!canStartUpload(item, now)) {
      continue;
    }

    const itemWeight = getUploadWeight(
      getDisplayTotalBytes(item),
      smallFileConcurrencyThresholdMb,
      largeFileConcurrencyThresholdMb
    );
    if (itemWeight > remainingWeightBudget) {
      continue;
    }

    runnableItems.push(item);
    remainingWeightBudget -= itemWeight;

    if (remainingWeightBudget <= 0) {
      break;
    }
  }

  return {
    orderedItems,
    runnableItems,
    activeItems,
  };
};

export const getOrderedPendingQueueItems = <TResult,>(
  items: QueuedFileUploadItem<TResult>[]
) =>
  sortQueueItemsByAddedOrder(items).filter(
    (item) =>
      item.status === "queued" ||
      item.status === "retry_wait" ||
      item.status === "uploading"
  );

export const buildRetryDelayMs = (
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

export const waitForNextPaint = async () => {
  await new Promise<void>((resolve) => {
    window.requestAnimationFrame(() => resolve());
  });
};

export const normalizeQueueItem = <TResult,>(
  item: QueuedFileUploadItem<TResult>
): QueuedFileUploadItem<TResult> => {
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

export const getSafeTotalBytes = <TResult,>(
  item: QueuedFileUploadItem<TResult>,
  fallbackBytes: number
) => {
  const normalizedBytes = getDisplayTotalBytes(item);
  if (normalizedBytes > 0) {
    return normalizedBytes;
  }

  return Math.max(0, fallbackBytes);
};

export const getSmoothedDisplayBytes = (
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
