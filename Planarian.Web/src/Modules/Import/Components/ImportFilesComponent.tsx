import {
  Alert,
  Button,
  Card,
  Checkbox,
  Col,
  Form,
  Input,
  Progress,
  Result,
  Row,
  Space,
  Statistic,
  Tag,
  Typography,
  message,
} from "antd";
import {
  CheckCircleOutlined,
  ClockCircleOutlined,
  InboxOutlined,
  PauseCircleOutlined,
  PlayCircleOutlined,
  RedoOutlined,
  StopOutlined,
} from "@ant-design/icons";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import Papa from "papaparse";
import "./ImportComponent.scss";
import { CSVDisplay } from "../../Files/Components/CsvDisplayComponent";
import { getFileType } from "../../Files/Services/FileHelpers";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
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

interface PersistedImportQueueItem extends Omit<ImportQueueItem, "file"> {}

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

const getStatusTag = (status: ImportQueueItemStatus) => {
  switch (status) {
    case "uploaded":
      return <Tag color="success">Uploaded</Tag>;
    case "uploading":
      return <Tag color="processing">Uploading</Tag>;
    case "retry_wait":
      return <Tag color="warning">Retrying</Tag>;
    case "failed":
      return <Tag color="error">Failed</Tag>;
    case "canceled":
      return <Tag color="default">Needs File</Tag>;
    default:
      return <Tag>Queued</Tag>;
  }
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

const normalizeUploadError = (error: unknown): UploadFailureDetails => {
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
  const [runnerTick, setRunnerTick] = useState(0);
  const [storageWarning, setStorageWarning] = useState<string | null>(null);
  const [restoreMessage, setRestoreMessage] = useState<string | null>(null);
  const queueItemsRef = useRef<ImportQueueItem[]>([]);
  const startedUploadsRef = useRef<Set<string>>(new Set());
  const nextDispatchAllowedAtRef = useRef<number>(0);
  const runnerTimerRef = useRef<number | null>(null);
  const hasNotifiedCompletionRef = useRef(false);

  const accountId = AuthenticationService.GetAccountId() ?? "anonymous";
  const queueStorageKey = useMemo(
    () =>
      confirmedSettings ? createStorageKey(accountId, confirmedSettings) : null,
    [accountId, confirmedSettings]
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
    return () => {
      if (runnerTimerRef.current !== null) {
        window.clearTimeout(runnerTimerRef.current);
      }
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
    nextDispatchAllowedAtRef.current = 0;
    setQueueItems([]);
    setIsPaused(true);
    hasNotifiedCompletionRef.current = false;
    setStorageWarning(null);
    setRestoreMessage(null);
  }, [queueStorageKey]);

  const loadPersistedQueue = useCallback(async () => {
    if (!queueStorageKey) return;

    setIsRestoring(true);
    setStorageWarning(null);
    setRestoreMessage(null);

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
      if (restoredItems.length > 0) {
        setRestoreMessage("Your previous upload was restored.");
      }
    } catch {
      setQueueItems([]);
      setIsPaused(true);
      setStorageWarning(
        "The saved upload queue could not be restored. You can continue with a fresh queue."
      );
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
          }
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
        setStorageWarning(
          "The queue could not persist every file for resume-after-refresh. Uploads can still continue in this tab."
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

  const handleDrop = async (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    if (!event.dataTransfer.files) return;
    await addFilesToQueue(event.dataTransfer.files);
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

    return { uploaded, failed, canceled, uploading, queued };
  }, [queueItems]);

  const activeItems = useMemo(
    () =>
      queueItems.filter(
        (item) => item.status === "uploading" || item.status === "retry_wait"
      ),
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
  const hasPartialFailures = failedItems.length > 0;

  useEffect(() => {
    if (!allWorkComplete || !hasSuccessfulUploads || hasNotifiedCompletionRef.current) {
      return;
    }

    hasNotifiedCompletionRef.current = true;
    onUploaded();
  }, [allWorkComplete, hasSuccessfulUploads, onUploaded]);

  return (
    <>
      {!inputsConfirmed && (
        <Card
          className="planarian-import-info-card"
          style={{ width: "100%", marginBottom: "20px" }}
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
        <>
          {restoreMessage && (
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: "16px" }}
              message={restoreMessage}
            />
          )}
          {storageWarning && (
            <Alert
              type="warning"
              showIcon
              style={{ marginBottom: "16px" }}
              message={storageWarning}
            />
          )}

          <Card
            className="planarian-upload-card"
            onDrop={(event) => {
              void handleDrop(event);
            }}
            onDragOver={(event) => event.preventDefault()}
            style={{
              width: "100%",
              marginBottom: "20px",
              textAlign: "center",
              cursor: "pointer",
            }}
          >
            <InboxOutlined style={{ fontSize: "48px", color: "#40a9ff" }} />
            <Typography.Title level={3}>Import Cave Files</Typography.Title>
            <Typography.Paragraph>
              Drag files here to build your upload. Start the upload when you are
              ready, then check back later if you need to.
            </Typography.Paragraph>

            <input
              id="import-files-input"
              type="file"
              multiple
              style={{ display: "none" }}
              onChange={(event) => {
                void handleFileSelect(event);
              }}
            />

            <Space wrap style={{ marginBottom: "16px" }}>
              <Button
                onClick={() =>
                  document.getElementById("import-files-input")?.click()
                }
              >
                Add Files
              </Button>
              <Button
                type="primary"
                icon={<PlayCircleOutlined />}
                onClick={() => setIsPaused(false)}
                disabled={isRestoring || queueItems.length === 0 || !isPaused}
              >
                {queueStats.uploaded > 0 || failedItems.length > 0
                  ? "Resume"
                  : "Start Upload"}
              </Button>
              <Button
                icon={<PauseCircleOutlined />}
                onClick={() => setIsPaused(true)}
                disabled={isRestoring || isPaused || queueStats.uploading === 0}
              >
                Pause
              </Button>
              <Button
                icon={<RedoOutlined />}
                onClick={retryFailed}
                disabled={isRestoring || failedItems.length === 0}
              >
                Review Problems
              </Button>
              <Button
                danger
                icon={<StopOutlined />}
                onClick={() => void resetQueueState()}
                disabled={isRestoring || queueItems.length === 0}
              >
                Start Over
              </Button>
              <Button onClick={() => setIsModalOpen(true)} disabled={fileResults.length === 0}>
                Export Results
              </Button>
              <Button onClick={() => void handleResetSettings()}>Reset Settings</Button>
            </Space>

            {failedItems.length > 0 && (
              <Alert
                type="warning"
                showIcon
                style={{ marginBottom: "16px", textAlign: "left" }}
                message={`${failedItems.length} file${failedItems.length === 1 ? "" : "s"} need attention.`}
                description="Open Review Problems to see the file names and messages."
              />
            )}

            <Card style={{ marginBottom: "16px", textAlign: "left" }}>
              <Typography.Title level={4}>Upload Progress</Typography.Title>
              <Progress percent={aggregateProgress} />
              <Row gutter={16} style={{ marginTop: "16px" }}>
                <Col span={6}>
                  <Statistic title="Files Added" value={queueItems.length} />
                </Col>
                <Col span={6}>
                  <Statistic title="Uploaded" value={queueStats.uploaded} />
                </Col>
                <Col span={6}>
                  <Statistic title="In Progress" value={queueStats.uploading} />
                </Col>
                <Col span={6}>
                  <Statistic title="Needs Attention" value={failedItems.length} />
                </Col>
              </Row>
            </Card>

            <Row gutter={16} style={{ textAlign: "left" }}>
              <Col xs={24} lg={12}>
                <Card style={{ marginBottom: "16px" }}>
                  <Typography.Title level={4}>Uploading Now</Typography.Title>
                  {activeItems.length === 0 ? (
                    <Typography.Paragraph type="secondary">
                      {isPaused
                        ? "Uploads are paused."
                        : queueStats.queued > 0
                        ? "Waiting to continue with the next file."
                        : "No files are uploading right now."}
                    </Typography.Paragraph>
                  ) : (
                    activeItems.map((item) => (
                      <div key={item.id} style={{ marginBottom: "14px" }}>
                        <Space
                          style={{
                            width: "100%",
                            justifyContent: "space-between",
                            display: "flex",
                          }}
                        >
                          <Typography.Text strong ellipsis style={{ maxWidth: "78%" }}>
                            {item.fileName}
                          </Typography.Text>
                          {getStatusTag(item.status)}
                        </Space>
                        <Progress
                          percent={item.status === "retry_wait" ? 0 : item.progress}
                          status={item.status === "retry_wait" ? "normal" : "active"}
                          size="small"
                        />
                        <Typography.Text type="secondary">
                          {formatFileSize(item.size)}
                        </Typography.Text>
                        {item.status === "retry_wait" && item.retryAt && (
                          <div>
                            <Typography.Text type="secondary">
                              <ClockCircleOutlined /> Trying again at{" "}
                              {new Date(item.retryAt).toLocaleTimeString()}
                            </Typography.Text>
                          </div>
                        )}
                      </div>
                    ))
                  )}
                </Card>
              </Col>
              <Col xs={24} lg={12}>
                <Card style={{ marginBottom: "16px" }}>
                  <Typography.Title level={4}>Recent Activity</Typography.Title>
                  {recentActivity.length === 0 ? (
                    <Typography.Paragraph type="secondary">
                      Finished files and problem files will appear here as the upload runs.
                    </Typography.Paragraph>
                  ) : (
                    recentActivity.map((item) => (
                      <div key={item.id} style={{ marginBottom: "12px" }}>
                        <Space
                          style={{
                            width: "100%",
                            justifyContent: "space-between",
                            display: "flex",
                          }}
                        >
                          <Typography.Text ellipsis style={{ maxWidth: "78%" }}>
                            {item.fileName}
                          </Typography.Text>
                          {getStatusTag(item.status)}
                        </Space>
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
                </Card>
              </Col>
            </Row>
          </Card>

          {allWorkComplete && hasSuccessfulUploads && !hasPartialFailures && (
            <Card className="planarian-import-result-card" style={{ width: "100%" }}>
              <Result
                icon={<CheckCircleOutlined style={{ color: "#52c41a" }} />}
                title="Successfully Uploaded!"
                subTitle={`${queueStats.uploaded} file${queueStats.uploaded === 1 ? "" : "s"} finished successfully.`}
              />
            </Card>
          )}

          {allWorkComplete && hasSuccessfulUploads && hasPartialFailures && (
            <Card className="planarian-import-result-card" style={{ width: "100%" }}>
              <Result
                icon={<CheckCircleOutlined style={{ color: "#FFA500" }} />}
                title="Partially Uploaded"
                subTitle={`${queueStats.uploaded} uploaded, ${failedItems.length} still need attention.`}
              />
            </Card>
          )}

          {allWorkComplete && !hasSuccessfulUploads && hasPartialFailures && (
            <Card className="planarian-import-result-card" style={{ width: "100%" }}>
              <Result
                status="error"
                title="Upload Failed"
                subTitle="No files were uploaded successfully. Review the problem files and try again."
              />
            </Card>
          )}
        </>
      )}

      <PlanarianModal
        fullScreen
        header="File Import Results"
        open={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        footer={null}
      >
        {failedItems.length > 0 && (
          <Alert
            type="warning"
            showIcon
            style={{ marginBottom: "16px" }}
            message={`${failedItems.length} file${failedItems.length === 1 ? "" : "s"} need attention.`}
          />
        )}
        {recentActivity.length > 0 && (
          <Card style={{ marginBottom: "16px" }}>
            <Typography.Title level={5}>Recent Activity</Typography.Title>
            {recentActivity.map((item) => (
              <div key={item.id} style={{ marginBottom: "12px" }}>
                <Space>
                  {getStatusTag(item.status)}
                  <Typography.Text>{item.fileName}</Typography.Text>
                </Space>
                {item.lastError && (
                  <div>
                    <Typography.Text type="danger">{item.lastError}</Typography.Text>
                  </div>
                )}
              </div>
            ))}
          </Card>
        )}
        <CSVDisplay data={Papa.unparse(fileResults)} />
      </PlanarianModal>
    </>
  );
};
