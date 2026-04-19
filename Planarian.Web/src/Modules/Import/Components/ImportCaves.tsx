import React, { useState } from "react";
import { Button, Radio, Spin, Typography, message } from "antd";
import {
  CheckCircleOutlined,
  CloudDownloadOutlined,
  DeliveredProcedureOutlined,
  EyeOutlined,
  RedoOutlined,
  UploadOutlined,
} from "@ant-design/icons";
import Papa from "papaparse";
import { Link } from "react-router-dom";
import { UploadComponent } from "../../Files/Components/UploadComponent";
import { CSVDisplay } from "../../Files/Components/CsvDisplayComponent";
import { ImportApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { CaveCsvModel } from "../Models/CaveCsvModel";
import { FileVm } from "../../Files/Models/FileVm";
import "./ImportComponent.scss";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { SignalRProgressComponent } from "../../../Shared/Components/SignalRProgress/SignalRProgressComponent";
import { AccountService } from "../../Account/Services/AccountService";
import { FailedCsvRecord } from "../Models/FailedCsvRecord";
import { CaveDryRunRecord } from "../Models/CaveDryRunRecord";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { AppOptions } from "../../../Shared/Services/AppService";
import { downloadFile } from "../../../Shared/Helpers/FileHelpers";

const { Title, Paragraph } = Typography;

interface ImportCaveComponentProps {
  onUploaded: () => void;
}

const buildDryRunCsv = (records: CaveDryRunRecord[]): string =>
  Papa.unparse(
    records
      .filter((record) => record.action !== "no change")
      .map((record) => ({
        countyCode: record.countyCode,
        countyCaveNumber: record.countyCaveNumber,
        caveName: record.caveName,
        changesSummary: record.changesSummary,
        action: record.action,
        state: record.state,
        countyName: record.countyName,
        alternateNames: record.alternateNames,
        caveLengthFeet: record.caveLengthFeet,
        caveDepthFeet: record.caveDepthFeet,
        maxPitDepthFeet: record.maxPitDepthFeet,
        numberOfPits: record.numberOfPits,
        reportedOnDate: record.reportedOnDate,
        isArchived: record.isArchived,
        geology: record.geology,
        reportedByNames: record.reportedByNames,
        biology: record.biology,
        archeology: record.archeology,
        cartographerNames: record.cartographerNames,
        geologicAges: record.geologicAges,
        physiographicProvinces: record.physiographicProvinces,
        otherTags: record.otherTags,
        mapStatuses: record.mapStatuses,
        narrative: record.narrative,
      }))
  );

const ImportCaveComponent: React.FC<ImportCaveComponentProps> = ({
  onUploaded,
}) => {
  const [isUploaded, setIsUploaded] = useState(false);
  const [uploadFailed, setUploadFailed] = useState(false);
  const [processError, setProcessError] = useState<string | null>(null);
  const [isProcessed, setIsProcessed] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const [errorList, setErrorList] = useState<FailedCsvRecord<CaveCsvModel>[]>(
    []
  );
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [uploadResult, setUploadResult] = useState<FileVm | undefined>();
  const [isLoading, setIsLoading] = useState(false);
  const [dryRunData, setDryRunData] = useState<CaveDryRunRecord[]>([]);
  const [isDryRunComplete, setIsDryRunComplete] = useState(false);
  const [syncExisting, setSyncExisting] = useState(false);

  const showCSVModal = () => setIsModalOpen(true);
  const handleOk = () => setIsModalOpen(false);
  const tryAgain = () => resetStates();
  const convertErrorListToCsv = (
    failedRecords: FailedCsvRecord<CaveCsvModel>[]
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
      const result = await AccountService.ImportCavesFileProcess(
        uploadResult.id,
        true,
        syncExisting
      );
      const changedRecords = result.filter(
        (record) => record.action !== "no change"
      );

      if (changedRecords.length === 0) {
        message.info("Dry run found no changes. The import has been reset.");
        resetStates();
        return;
      }

      setDryRunData(changedRecords);
      setIsDryRunComplete(true);
    } catch (e) {
      const error = e as ImportApiErrorResponse<CaveCsvModel>;

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
      await AccountService.ImportCavesFileProcess(
        uploadResult.id,
        false,
        syncExisting
      );
      setIsProcessed(true);
    } catch (e) {
      const error = e as ImportApiErrorResponse<CaveCsvModel>;

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
  const dryRunCsvData = buildDryRunCsv(dryRunData);

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
                      Cave CSV
                    </div>
                    <Title level={4} style={{ margin: 0 }}>
                      Cave Import
                    </Title>
                    <Paragraph style={{ marginBottom: 0 }}>
                      Upload your cave CSV to review it with a dry run before
                      any changes are applied. Uploading the file alone will
                      not import, update, or overwrite any cave records.
                    </Paragraph>
                  </div>
                </div>
                <div className="import-step-panel__body import-step-panel__body--plain">
                  <UploadComponent
                    draggerMessage="Choose a cave CSV or drag it here to upload."
                    draggerTitle="Import Cave File"
                    hideCancelButton
                    singleFile
                    allowedFileTypes={[".csv"]}
                    style={{ display: "flex", height: "100%" }}
                    uploadFunction={async (params): Promise<FileVm> => {
                      try {
                        const result = await AccountService.ImportCavesFile(
                          params.file,
                          params.uid,
                          params.onProgress
                        );
                        setUploadResult(result);
                        setIsUploaded(true);
                      } catch (e) {
                        const error = e as ImportApiErrorResponse<CaveCsvModel>;

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
                            <DeliveredProcedureOutlined />
                          ) : isReadyToProcess ? (
                            <EyeOutlined />
                          ) : (
                            <CheckCircleOutlined />
                          )}
                        </span>
                        <div className="import-step-status__copy">
                          <Title level={4} style={{ marginTop: 0 }}>
                            {isAwaitingDryRun
                              ? "Cave CSV uploaded"
                              : isReadyToProcess
                              ? "Dry run complete"
                              : isProcessingState
                              ? "Processing cave import"
                              : isCompleteState
                              ? "Cave import complete"
                              : "Cave import failed"}
                          </Title>
                          <Paragraph>
                            {isAwaitingDryRun
                              ? "Run a dry run before processing so you can review inserts, updates, and deletes."
                              : isReadyToProcess
                              ? "Review the dry run output, then process the import if the changes look correct."
                              : isProcessingState
                              ? "Keep this page open while Planarian processes the uploaded cave data."
                              : isCompleteState
                              ? "Your cave CSV has been processed successfully. Continue to the entrance import when you are ready."
                              : `Planarian should tell you why a row failed. If it does not, or you need help, contact ${
                                  supportContact ?? "support"
                                }.`}
                          </Paragraph>
                          {(isAwaitingDryRun || isReadyToProcess) && (
                            <Paragraph style={{ marginBottom: 0 }}>
                              {isAwaitingDryRun
                                ? syncExisting
                                  ? "Insert / Update mode will reconcile the cave records in the database to this CSV."
                                  : "Insert Only mode will add only new caves and fail if a cave already exists."
                                : `${dryRunData.length} change${
                                    dryRunData.length === 1 ? "" : "s"
                                  } will be applied.`}
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
                        isLoading={isProcessing}
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
                          value={syncExisting ? "upsert" : "insert"}
                          onChange={(event) =>
                            setSyncExisting(event.target.value === "upsert")
                          }
                          optionType="button"
                          buttonStyle="solid"
                        >
                          <Radio.Button value="insert">Insert Only</Radio.Button>
                          <Radio.Button value="upsert">Insert / Update</Radio.Button>
                        </Radio.Group>
                        <Paragraph className="import-step-note" style={{ marginBottom: 0 }}>
                          {syncExisting
                            ? "Matching caves will be updated, new caves inserted, and caves missing from the CSV deleted."
                            : "Only new caves will be inserted. Existing caves in the CSV will cause the import to fail."}
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
                          Open the dry run data if you need the full row-by-row
                          output, then process the import.
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
                          <Button onClick={showCSVModal} icon={<EyeOutlined />}>
                            View Dry Run Data
                          </Button>
                          <PlanarianButton
                            alwaysShowChildren
                            onClick={handleProcessClick}
                            icon={<DeliveredProcedureOutlined />}
                            loading={isLoading}
                            type="primary"
                          >
                            Process
                          </PlanarianButton>
                          <Button onClick={tryAgain} icon={<RedoOutlined />}>
                            Reset
                          </Button>
                        </>
                      )}

                      {isCompleteState && (
                        <>
                          <Button type="primary" onClick={onUploaded}>
                            Import Entrances
                          </Button>
                          <Button onClick={tryAgain} icon={<RedoOutlined />}>
                            Import Another Cave File
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
            "Import Cave Errors",
            <PlanarianButton
              key="download-cave-errors"
              alwaysShowChildren
              icon={<CloudDownloadOutlined />}
              onClick={() => downloadFile("cave_import_errors.csv", errorCsvData)}
            >
              Download CSV
            </PlanarianButton>,
          ]}
          open={isModalOpen}
          onClose={handleOk}
          footer={null}
        >
          <Spin spinning={errorList.length === 0}>
            <CSVDisplay data={errorCsvData} />
          </Spin>
        </PlanarianModal>
      )}

      {dryRunData.length > 0 && (
        <PlanarianModal
          fullScreen
          header={[
            "Dry Run Data",
            <PlanarianButton
              key="download-cave-dry-run"
              alwaysShowChildren
              icon={<CloudDownloadOutlined />}
              onClick={() => downloadFile("cave_dry_run.csv", dryRunCsvData)}
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

export { ImportCaveComponent };
