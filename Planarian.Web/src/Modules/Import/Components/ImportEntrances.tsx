import React, { useState } from "react";
import { Button, Radio, Typography, message } from "antd";
import {
  ApartmentOutlined,
  CheckCircleOutlined,
  CloudDownloadOutlined,
  DeliveredProcedureOutlined,
  EyeOutlined,
  LoadingOutlined,
  RedoOutlined,
  UploadOutlined,
} from "@ant-design/icons";
import Papa from "papaparse";
import { UploadComponent } from "../../Files/Components/UploadComponent";
import { CSVDisplay } from "../../Files/Components/CsvDisplayComponent";
import { ImportApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { FileVm } from "../../Files/Models/FileVm";
import "./ImportComponent.scss";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { SignalRProgressComponent } from "../../../Shared/Components/SignalRProgress/SignalRProgressComponent";
import { Link } from "react-router-dom";
import { AccountService } from "../../Account/Services/AccountService";
import { EntranceCsvModel } from "../Models/EntranceCsvModel";
import { FailedCsvRecord } from "../Models/FailedCsvRecord";
import { EntranceDryRun } from "../Models/EntranceDryRun";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { AppOptions } from "../../../Shared/Services/AppService";
import { downloadFile } from "../../../Shared/Helpers/FileHelpers";

const { Title, Paragraph } = Typography;

interface ImportEntrancesComponentProps {
  onUploaded: () => void;
}

const ImportEntrancesComponent: React.FC<ImportEntrancesComponentProps> = ({
  onUploaded,
}) => {
  const [isUploaded, setIsUploaded] = useState(false);
  const [uploadFailed, setUploadFailed] = useState(false);
  const [processError, setProcessError] = useState<string | null>(null);
  const [isProcessed, setIsProcessed] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const [errorList, setErrorList] = useState<
    FailedCsvRecord<EntranceCsvModel>[]
  >([]);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [uploadResult, setUploadResult] = useState<FileVm | undefined>();
  const [isLoading, setIsLoading] = useState(false);
  const [dryRunData, setDryRunData] = useState<EntranceDryRun[]>([]);
  const [isDryRunComplete, setIsDryRunComplete] = useState(false);
  const [syncExisting, setSyncExisting] = useState(false);

  const showCSVModal = () => setIsModalOpen(true);
  const handleOk = () => setIsModalOpen(false);
  const tryAgain = () => resetStates();
  const convertErrorListToCsv = (
    failedRecords: FailedCsvRecord<EntranceCsvModel>[]
  ): string =>
    Papa.unparse(
      failedRecords.map((error) => ({
        rowNumber: error.rowNumber,
        reason: error.reason,
        ...error.caveCsvModel,
      }))
    );

  const handleDryRunClick = async () => {
    if (!uploadResult?.id) return message.error("File id not found");
    setIsLoading(true);
    setIsProcessing(true);
    try {
      const result = await AccountService.ImportEntrancesProcess(
        uploadResult.id,
        true,
        syncExisting
      );
      setDryRunData(result);
      setIsDryRunComplete(true);
    } catch (e) {
      const error = e as ImportApiErrorResponse<EntranceCsvModel>;

      if (error.data) {
        setErrorList(error.data);
      }
      setProcessError(error.message);
    } finally {
      setIsLoading(false);
      setIsProcessing(false);
    }
  };

  const handleProcessClick = async () => {
    if (!uploadResult?.id) return message.error("File id not found");
    setIsLoading(true);
    setIsProcessing(true);
    try {
      await AccountService.ImportEntrancesProcess(
        uploadResult.id,
        false,
        syncExisting
      );
      setIsProcessed(true);
    } catch (e) {
      const error = e as ImportApiErrorResponse<EntranceCsvModel>;

      if (error.data) {
        setErrorList(error.data);
      }
      setProcessError(error.message);
    } finally {
      setIsLoading(false);
      setIsProcessing(false);
    }
  };

  const resetStates = () => {
    setIsUploaded(false);
    setIsProcessed(false);
    setUploadFailed(false);
    setProcessError(null);
    setErrorList([]);
    setUploadResult(undefined);
    setDryRunData([]);
    setIsDryRunComplete(false);
    setSyncExisting(false);
  };

  const hasErrors = uploadFailed || errorList.length > 0 || processError !== null;
  const isInitialUploadState = !isUploaded && !hasErrors;
  const isAwaitingDryRun =
    isUploaded && !isProcessing && !isProcessed && !isDryRunComplete && !hasErrors;
  const isReadyToProcess =
    isDryRunComplete && !isProcessing && !isProcessed && !hasErrors;
  const isProcessingState = isProcessing;
  const isCompleteState = isProcessed && !isProcessing;
  const isErrorState = hasErrors;
  const isPostUploadState = !isInitialUploadState;
  const supportContact =
    AppOptions?.supportName && AppOptions?.supportEmail
      ? `${AppOptions.supportName} at ${AppOptions.supportEmail}`
      : null;
  const errorCsvData = convertErrorListToCsv(errorList);
  const dryRunCsvData = Papa.unparse(dryRunData);

  return (
    <>
      <div className="import-step-workspace">
        <div
          className={`import-step-layout${
            isInitialUploadState ? " import-step-layout--upload-focus" : ""
          }${
            isPostUploadState ? " import-step-layout--review-focus" : ""
          }`}
        >
          <div className="import-step-main">
            {isInitialUploadState && (
              <div className="import-step-surface import-step-card import-step-card--elevated import-step-panel import-step-upload">
                <div className="import-step-panel__header">
                  <div>
                    <div className="import-step-kicker">
                      <UploadOutlined />
                      Entrance CSV
                    </div>
                    <Title level={4} style={{ margin: 0 }}>
                      Entrance Import
                    </Title>
                    <Paragraph style={{ marginBottom: 0 }}>
                      Cave records must be added first before entrance data can
                      be imported. Upload your entrance CSV to review it with a
                      dry run before any changes are applied.
                    </Paragraph>
                  </div>
                </div>
                <div className="import-step-panel__body import-step-panel__body--plain">
                  <UploadComponent
                    draggerMessage="Choose an entrance CSV or drag it here to upload."
                    draggerTitle="Import Entrance File"
                    hideCancelButton
                    singleFile
                    allowedFileTypes={[".csv"]}
                    style={{ display: "flex", height: "100%" }}
                    uploadFunction={async (params): Promise<FileVm> => {
                      try {
                        const result = await AccountService.ImportEntrancesFile(
                          params.file,
                          params.uid,
                          params.onProgress
                        );
                        setUploadResult(result);
                        setIsUploaded(true);
                      } catch (e) {
                        const error = e as ImportApiErrorResponse<EntranceCsvModel>;

                        if (error.data) {
                          setErrorList(error.data);
                        } else {
                          setUploadFailed(true);
                        }

                        message.error(error.message);
                      }
                      return {} as FileVm;
                    }}
                    updateFunction={() => {
                      throw new Error("Function not implemented intentionally.");
                    }}
                  />
                </div>
              </div>
            )}

            {isPostUploadState && (
              <div className="import-step-surface import-step-card import-step-card--elevated import-step-panel import-step-review-card">
                <div className="import-step-panel__body import-step-panel__body--plain import-step-review-card__body">
                  <div className="import-step-review-card__status">
                    <div className="import-step-status">
                      <div className="import-step-status__hero">
                        <span
                          className={`import-step-status__icon ${
                            isErrorState
                              ? "import-step-status__icon--error"
                              : isProcessingState
                              ? "import-step-status__icon--processing"
                              : "import-step-status__icon--success"
                          }`}
                        >
                          {isErrorState ? (
                            <RedoOutlined />
                          ) : isProcessingState ? (
                            <LoadingOutlined />
                          ) : isReadyToProcess ? (
                            <ApartmentOutlined />
                          ) : (
                            <CheckCircleOutlined />
                          )}
                        </span>
                        <div className="import-step-status__copy">
                          <Title level={4} style={{ marginTop: 0 }}>
                            {isAwaitingDryRun
                              ? "Entrance CSV uploaded"
                              : isReadyToProcess
                              ? "Dry run complete"
                              : isProcessingState
                              ? "Processing entrance import"
                              : isCompleteState
                              ? "Entrance import complete"
                              : "Entrance import failed"}
                          </Title>
                          <Paragraph>
                            {isAwaitingDryRun
                              ? "Run a dry run before processing so you can preview entrance changes."
                              : isReadyToProcess
                              ? "Review the output, then process the entrance import if the changes look correct."
                              : isProcessingState
                              ? "Keep this page open while Planarian applies the entrance data."
                              : isCompleteState
                              ? "Your entrance CSV has been processed successfully."
                              : `Planarian should tell you why a row failed. If it does not, or you need help, contact ${
                                  supportContact ?? "support"
                                }.`}
                          </Paragraph>
                          {(isAwaitingDryRun || isReadyToProcess) && (
                            <Paragraph style={{ marginBottom: 0 }}>
                              {isAwaitingDryRun
                                ? syncExisting
                                  ? "Replace Existing mode deletes the current entrances for matching caves and replaces them with the CSV."
                                  : "Insert Only mode adds new entrances and leaves existing entrances unchanged."
                                : `${dryRunData.length} cave${
                                    dryRunData.length === 1 ? "" : "s"
                                  } were included in the dry run.`}
                            </Paragraph>
                          )}
                          {isErrorState && processError && (
                            <Paragraph style={{ display: "none", marginBottom: 0 }}>
                              {processError}
                            </Paragraph>
                          )}
                        </div>
                      </div>
                    </div>
                    {isProcessingState && (
                      <SignalRProgressComponent
                        groupName={uploadResult?.id as string}
                        isLoading={false}
                      />
                    )}
                  </div>

                  <div className="import-step-review-card__controls">
                    {isAwaitingDryRun && (
                      <div className="import-step-mode-card">
                        <Title level={5} style={{ margin: 0 }}>
                          Import Mode
                        </Title>
                        <Radio.Group
                          value={syncExisting ? "replace" : "insert"}
                          onChange={(event) =>
                            setSyncExisting(event.target.value === "replace")
                          }
                          optionType="button"
                          buttonStyle="solid"
                        >
                          <Radio.Button value="insert">Insert Only</Radio.Button>
                          <Radio.Button value="replace">
                            Replace Existing
                          </Radio.Button>
                        </Radio.Group>
                        <Paragraph className="import-step-note" style={{ marginBottom: 0 }}>
                          {syncExisting
                            ? "Existing entrances for included caves will be deleted and replaced with the entrances in this CSV."
                            : "Only new entrances will be inserted. Existing entrances stay unchanged."}
                        </Paragraph>
                        <Paragraph className="import-step-note" style={{ marginBottom: 0 }}>
                          It is <strong>strongly recommended</strong> to create
                          an archive first in{" "}
                          <Link to="/account/settings">Account Settings</Link>{" "}
                          before running this import.
                        </Paragraph>
                      </div>
                    )}

                    {isReadyToProcess && (
                      <div className="import-step-mode-card">
                        <Title level={5} style={{ margin: 0 }}>
                          Next Step
                        </Title>
                        <Paragraph style={{ marginBottom: 0 }}>
                          A count change of 0 means the number of entrances is
                          unchanged, not that the entrance data itself is unchanged.
                        </Paragraph>
                      </div>
                    )}

                    <div className="import-step-actions import-step-actions--stable">
                      {isAwaitingDryRun && (
                        <>
                          <PlanarianButton
                            alwaysShowChildren
                            onClick={handleDryRunClick}
                            icon={<EyeOutlined />}
                            loading={isLoading}
                            type="primary"
                          >
                            Dry Run
                          </PlanarianButton>
                          <Button onClick={tryAgain} icon={<RedoOutlined />}>
                            Reset
                          </Button>
                        </>
                      )}

                      {isReadyToProcess && (
                        <>
                          <PlanarianButton
                            alwaysShowChildren
                            onClick={showCSVModal}
                            icon={<EyeOutlined />}
                          >
                            View Dry Run Data
                          </PlanarianButton>
                          <PlanarianButton
                            alwaysShowChildren
                            onClick={handleProcessClick}
                            icon={<DeliveredProcedureOutlined />}
                            loading={isLoading}
                            type="primary"
                          >
                            Process
                          </PlanarianButton>
                          <PlanarianButton
                            alwaysShowChildren
                            onClick={tryAgain}
                            icon={<RedoOutlined />}
                          >
                            Reset
                          </PlanarianButton>
                        </>
                      )}

                      {isCompleteState && (
                        <>
                          <Link to="/caves">
                            <Button type="primary" onClick={onUploaded}>
                              View Caves
                            </Button>
                          </Link>
                          <Button onClick={tryAgain} icon={<RedoOutlined />}>
                            Import Another Entrance File
                          </Button>
                        </>
                      )}

                      {isErrorState && (
                        <>
                          {errorList.length > 0 && (
                            <Button
                              type="primary"
                              onClick={showCSVModal}
                              icon={<EyeOutlined />}
                            >
                              View Errors
                            </Button>
                          )}
                          <Button danger onClick={tryAgain} icon={<RedoOutlined />}>
                            Reset
                          </Button>
                        </>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      {(errorList.length > 0 || processError !== null) && (
        <PlanarianModal
          fullScreen
          header={[
            "Import Entrance Errors",
            <PlanarianButton
              key="download-entrance-errors"
              alwaysShowChildren
              icon={<CloudDownloadOutlined />}
              onClick={() =>
                downloadFile("entrance_import_errors.csv", errorCsvData)
              }
            >
              Download CSV
            </PlanarianButton>,
          ]}
          open={isModalOpen}
          onClose={handleOk}
          footer={null}
        >
          <CSVDisplay data={errorCsvData} />
        </PlanarianModal>
      )}

      {dryRunData.length > 0 && (
        <PlanarianModal
          fullScreen
          header={[
            "Dry Run Data",
            <PlanarianButton
              key="download-entrance-dry-run"
              alwaysShowChildren
              icon={<CloudDownloadOutlined />}
              onClick={() =>
                downloadFile("entrance_dry_run.csv", dryRunCsvData)
              }
            >
              Download CSV
            </PlanarianButton>,
          ]}
          open={isModalOpen}
          onClose={handleOk}
          footer={null}
        >
          <CSVDisplay data={dryRunCsvData} />
        </PlanarianModal>
      )}
    </>
  );
};

export { ImportEntrancesComponent };
