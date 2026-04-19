import { Form, message } from "antd";
import axios from "axios";
import React, { useCallback, useEffect, useMemo, useState } from "react";
import "./ImportComponent.scss";
import {
  QueuedFileUploader,
} from "../../../Shared/Components/FileUploader/QueuedFileUploader";
import { useQueuedChunkedFileUploader } from "../../../Shared/Components/FileUploader/useQueuedChunkedFileUploader";
import {
  QueuedFileUploadFailureDetails,
  QueuedFileUploadItem,
} from "../../../Shared/Components/FileUploader/types";
import { ApiErrorResponse, ApiExceptionType } from "../../../Shared/Models/ApiErrorResponse";
import { AccountService } from "../../Account/Services/AccountService";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { getFileType } from "../../Files/Services/FileHelpers";
import { FileImportResult } from "../Models/FileUploadresult";
import {
  DelimiterFormFields,
  ImportFileSettings,
  ImportFileSettingsForm,
} from "./ImportFileSettingsForm";
import { ImportFileResultsModal } from "./ImportFileResultsModal";

const MAX_FILE_SIZE_MB = 500;
const LOCAL_STORAGE_PREFIX = "planarian-import-files-queue";
const SETTINGS_STORAGE_PREFIX = "planarian-import-files-settings";

interface ImportCaveComponentProps {
  onUploaded: () => void;
}

const createStorageKey = (accountId: string, settings: ImportFileSettings) =>
  [
    LOCAL_STORAGE_PREFIX,
    accountId,
    settings.delimiter || "__none__",
    settings.idRegex,
    settings.ignoreDuplicates ? "ignore-duplicates" : "keep-duplicates",
  ].join("::");

const createSettingsStorageKey = (accountId: string) =>
  `${SETTINGS_STORAGE_PREFIX}::${accountId}`;

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

const normalizeUploadError = (error: unknown): QueuedFileUploadFailureDetails => {
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

const createCsvRow = (
  item: QueuedFileUploadItem<FileImportResult>
): FileImportResult => ({
  fileName: item.fileName,
  isSuccessful: item.status === "uploaded" || item.status === "skipped",
  status:
    item.status === "uploaded"
      ? "Uploaded"
      : item.status === "skipped"
      ? "Skipped"
      : item.status === "canceled"
      ? "Canceled"
      : "Failed",
  associatedCave: item.result?.associatedCave ?? "",
  message: item.result?.message ?? item.lastError ?? "",
  failureCode: item.failureCode ?? item.result?.failureCode ?? null,
  isRetryable: item.isRetryable,
  requestId: item.requestId ?? item.result?.requestId ?? null,
});

export const ImportFilesComponent: React.FC<ImportCaveComponentProps> = ({
  onUploaded,
}) => {
  const [form] = Form.useForm<DelimiterFormFields>();
  const [confirmedSettings, setConfirmedSettings] =
    useState<ImportFileSettings | null>(null);
  const [inputsConfirmed, setInputsConfirmed] = useState(false);
  const [isModalOpen, setIsModalOpen] = useState(false);

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
      return;
    }

    try {
      const persistedSettings = JSON.parse(serializedSettings) as ImportFileSettings;
      setConfirmedSettings(persistedSettings);
      setInputsConfirmed(true);
      form.setFieldsValue({
        delimiter: persistedSettings.delimiter,
        idRegex: persistedSettings.idRegex,
        ignoreDuplicates: persistedSettings.ignoreDuplicates,
      });
    } catch {
      localStorage.removeItem(settingsStorageKey);
    }
  }, [form, settingsStorageKey]);

  useEffect(() => {
    if (!confirmedSettings || !inputsConfirmed) {
      return;
    }

    localStorage.setItem(settingsStorageKey, JSON.stringify(confirmedSettings));
  }, [confirmedSettings, inputsConfirmed, settingsStorageKey]);

  const validateFile = useCallback((file: File) => {
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
  }, []);

  const endpoints = useMemo(
    () => ({
      createSession: (file: File, requestId: string) => {
        if (!confirmedSettings) {
          throw new Error("File import settings have not been confirmed.");
        }

        return AccountService.CreateImportFileUploadSession({
          fileName: file.name,
          fileSize: file.size,
          delimiterRegex: confirmedSettings.delimiter,
          idRegex: confirmedSettings.idRegex,
          ignoreDuplicates: confirmedSettings.ignoreDuplicates,
          requestId,
        });
      },
      uploadChunk: AccountService.UploadImportFileChunk,
      finalizeSession: AccountService.FinalizeImportFileUploadSession,
      cancelSession: AccountService.CancelImportFileUploadSession,
    }),
    [confirmedSettings]
  );

  const normalizeFileImportError = useCallback(
    (error: unknown): QueuedFileUploadFailureDetails => {
      const normalized = normalizeUploadError(error);
      const shouldSkipDuplicate =
        confirmedSettings?.ignoreDuplicates === true &&
        normalized.failureCode === ApiExceptionType.Conflict;

      return shouldSkipDuplicate
        ? {
            ...normalized,
            isRetryable: false,
            terminalStatus: "skipped",
          }
        : normalized;
    },
    [confirmedSettings]
  );

  const handleCompleted = useCallback(() => {
    onUploaded();
  }, [onUploaded]);

  const queue = useQueuedChunkedFileUploader<FileImportResult>({
    storageKey: queueStorageKey,
    endpoints,
    normalizeError: normalizeFileImportError,
    isAbortError: isAbortLikeError,
    validateFile,
    onCompleted: handleCompleted,
  });

  const resetQueueAndSettings = useCallback(async () => {
    await queue.resetQueueState();
    localStorage.removeItem(settingsStorageKey);
    setInputsConfirmed(false);
    setConfirmedSettings(null);
    form.resetFields();
  }, [form, queue, settingsStorageKey]);

  const fileResults = useMemo(
    () => queue.fileResults.map(createCsvRow),
    [queue.fileResults]
  );

  const renderRecentActivityTooltip = useCallback(
    (item: QueuedFileUploadItem<FileImportResult>) => {
      const tooltipLines = [
        item.result?.associatedCave,
        item.status === "failed" || item.status === "canceled" || item.status === "skipped"
          ? item.lastError
          : null,
      ].filter(Boolean);

      if (tooltipLines.length === 0) {
        return undefined;
      }

      return (
        <div>
          {tooltipLines.map((line) => (
            <div key={line}>{line}</div>
          ))}
        </div>
      );
    },
    []
  );

  return (
    <>
      {!inputsConfirmed && (
        <ImportFileSettingsForm
          form={form}
          onConfirm={(settings) => {
            setConfirmedSettings(settings);
            setInputsConfirmed(true);
          }}
        />
      )}

      {inputsConfirmed && (
        <QueuedFileUploader
          queue={{
            ...queue,
            resetQueueState: resetQueueAndSettings,
          }}
          onViewResults={() => setIsModalOpen(true)}
          hasResults={fileResults.length > 0}
          renderRecentActivityTooltip={renderRecentActivityTooltip}
        />
      )}

      <ImportFileResultsModal
        open={isModalOpen}
        results={fileResults}
        onClose={() => setIsModalOpen(false)}
      />
    </>
  );
};
