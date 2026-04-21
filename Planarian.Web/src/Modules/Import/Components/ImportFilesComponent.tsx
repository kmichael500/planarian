import { Form, message } from "antd";
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
import { ApiExceptionType } from "../../../Shared/Models/ApiErrorResponse";
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
      cancelAllSessions: AccountService.CancelActiveImportFileUploadSessions,
    }),
    [confirmedSettings]
  );

  const mapFileImportFailure = useCallback(
    (failure: QueuedFileUploadFailureDetails): QueuedFileUploadFailureDetails => {
      const shouldSkipDuplicate =
        confirmedSettings?.ignoreDuplicates === true &&
        failure.failureCode === ApiExceptionType.Conflict;

      return shouldSkipDuplicate
        ? {
            ...failure,
            terminalStatus: "skipped",
          }
        : failure;
    },
    [confirmedSettings]
  );

  const handleCompleted = useCallback(() => {
    onUploaded();
  }, [onUploaded]);

  const queue = useQueuedChunkedFileUploader<FileImportResult>({
    storageKey: queueStorageKey,
    endpoints,
    mapFailure: mapFileImportFailure,
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
