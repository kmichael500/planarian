import React, { useState } from "react";
import { Alert, Button, Radio, Typography, message } from "antd";
import {
  ApartmentOutlined,
  CheckCircleOutlined,
  DeliveredProcedureOutlined,
  EyeOutlined,
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

const { Title, Paragraph, Text } = Typography;

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

  return (
    <>
      <div className="import-step-workspace">
        {hasErrors && (
          <Alert
            className="import-step-banner"
            type="error"
            showIcon
            message={processError ?? "There were errors with this import."}
            description={
              errorList.length > 0
                ? `${errorList.length} row${errorList.length === 1 ? "" : "s"} need attention.`
                : "Please try uploading the file again."
            }
          />
        )}

        <div className="import-step-layout">
          <div className="import-step-main">
            {!isUploaded && !hasErrors && (
              <div className="import-step-surface import-step-card import-step-panel import-step-upload">
                <div className="import-step-panel__header">
                  <div>
                    <div className="import-step-kicker">
                      <UploadOutlined />
                      Entrance CSV
                    </div>
                    <Title level={4} style={{ margin: 0 }}>
                      Upload Entrance Data
                    </Title>
                    <Paragraph style={{ marginBottom: 0 }}>
                      Import entrances after the cave records are already in
                      place.
                    </Paragraph>
                  </div>
                </div>
                <div className="import-step-panel__body import-step-panel__body--plain">
                  <UploadComponent
                    draggerMessage="Click or drag your entrances CSV file to this area to upload."
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

            {isUploaded &&
              !isProcessing &&
              !isProcessed &&
              !isDryRunComplete &&
              !hasErrors && (
                <div className="import-step-surface import-step-card import-step-panel">
                  <div className="import-step-panel__body import-step-panel__body--plain">
                    <div className="import-step-status">
                      <div className="import-step-status__hero">
                        <span className="import-step-status__icon import-step-status__icon--success">
                          <CheckCircleOutlined />
                        </span>
                        <div className="import-step-status__copy">
                          <Title level={4} style={{ marginTop: 0 }}>
                            Entrance CSV uploaded
                          </Title>
                          <Paragraph>
                            Run a dry run before processing so you can preview
                            entrance changes.
                          </Paragraph>
                          <Paragraph style={{ marginBottom: 0 }}>
                            {syncExisting
                              ? "Replace Existing mode deletes the current entrances for matching caves and replaces them with the CSV."
                              : "Insert Only mode adds new entrances and leaves existing entrances unchanged."}
                          </Paragraph>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              )}

            {isDryRunComplete && !isProcessing && !isProcessed && !hasErrors && (
              <div className="import-step-surface import-step-card import-step-panel">
                <div className="import-step-panel__body import-step-panel__body--plain">
                  <div className="import-step-status">
                    <div className="import-step-status__hero">
                      <span className="import-step-status__icon import-step-status__icon--success">
                        <ApartmentOutlined />
                      </span>
                      <div className="import-step-status__copy">
                        <Title level={4} style={{ marginTop: 0 }}>
                          Dry run complete
                        </Title>
                        <Paragraph>
                          Review the output, then process the entrance import if
                          the changes look correct.
                        </Paragraph>
                        <Paragraph style={{ marginBottom: 0 }}>
                          {dryRunData.length} cave
                          {dryRunData.length === 1 ? "" : "s"} were included in
                          the dry run.
                        </Paragraph>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {isProcessing && (
              <div className="import-step-surface import-step-card import-step-panel">
                <div className="import-step-panel__body import-step-panel__body--plain">
                  <div className="import-step-status">
                    <div className="import-step-status__hero">
                      <span className="import-step-status__icon import-step-status__icon--processing">
                        <DeliveredProcedureOutlined />
                      </span>
                      <div className="import-step-status__copy">
                        <Title level={4} style={{ marginTop: 0 }}>
                          Processing entrance import
                        </Title>
                        <Paragraph>
                          Keep this page open while Planarian applies the
                          entrance data.
                        </Paragraph>
                      </div>
                    </div>
                    <SignalRProgressComponent
                      groupName={uploadResult?.id as string}
                      isLoading={isProcessing}
                    />
                  </div>
                </div>
              </div>
            )}

            {isProcessed && !isProcessing && (
              <div className="import-step-surface import-step-card import-step-panel">
                <div className="import-step-panel__body import-step-panel__body--plain">
                  <div className="import-step-status">
                    <div className="import-step-status__hero">
                      <span className="import-step-status__icon import-step-status__icon--success">
                        <CheckCircleOutlined />
                      </span>
                      <div className="import-step-status__copy">
                        <Title level={4} style={{ marginTop: 0 }}>
                          Entrance import complete
                        </Title>
                        <Paragraph style={{ marginBottom: 0 }}>
                          Your entrance CSV has been processed successfully.
                        </Paragraph>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {hasErrors && (
              <div className="import-step-surface import-step-card import-step-panel">
                <div className="import-step-panel__body import-step-panel__body--plain">
                  <div className="import-step-status">
                    <div className="import-step-status__hero">
                      <span className="import-step-status__icon import-step-status__icon--error">
                        <RedoOutlined />
                      </span>
                      <div className="import-step-status__copy">
                        <Title level={4} style={{ marginTop: 0 }}>
                          Entrance import needs attention
                        </Title>
                        <Paragraph style={{ marginBottom: 0 }}>
                          Review the errors, fix the CSV if needed, and try the
                          upload again.
                        </Paragraph>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>

          <div className="import-step-side">
            <div className="import-step-surface import-step-card import-step-panel">
              <div className="import-step-panel__header">
                <div>
                  <Title level={5} style={{ margin: 0 }}>
                    {isProcessed
                      ? "Next step"
                      : hasErrors
                      ? "Recovery"
                      : isProcessing
                      ? "Progress"
                      : isDryRunComplete
                      ? "Review"
                      : isUploaded
                      ? "Import mode"
                      : "Before you upload"}
                  </Title>
                </div>
              </div>
              <div className="import-step-panel__body">
                {!isUploaded && !hasErrors && (
                  <div className="import-step-mode-card">
                    <Text strong>Suggested order</Text>
                    <Text>1. Upload entrances</Text>
                    <Text>2. Dry run the changes</Text>
                    <Text>3. Process the import</Text>
                    <Text>4. Review the caves and files</Text>
                  </div>
                )}

                {isUploaded &&
                  !isProcessing &&
                  !isProcessed &&
                  !isDryRunComplete &&
                  !hasErrors && (
                    <div className="import-step-mode-card">
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
                        It is strongly recommended to create an archive first in{" "}
                        <Link to="/account/settings">Account Settings</Link>.
                      </Paragraph>
                      <div className="import-step-actions">
                        <PlanarianButton
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
                      </div>
                    </div>
                  )}

                {isDryRunComplete && !isProcessing && !isProcessed && !hasErrors && (
                  <div className="import-step-mode-card">
                    <Paragraph style={{ marginBottom: 0 }}>
                      A count change of 0 means the number of entrances is
                      unchanged, not that the entrance data itself is unchanged.
                    </Paragraph>
                    <div className="import-step-actions">
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
                    </div>
                  </div>
                )}

                {isProcessing && (
                  <Paragraph style={{ marginBottom: 0 }}>
                    Progress updates are shown live while the server processes
                    the entrance import.
                  </Paragraph>
                )}

                {isProcessed && !isProcessing && (
                  <div className="import-step-actions">
                    <Link to="/caves">
                      <Button type="primary" onClick={onUploaded}>
                        View Caves
                      </Button>
                    </Link>
                    <Button onClick={tryAgain} icon={<RedoOutlined />}>
                      Import Another Entrance File
                    </Button>
                  </div>
                )}

                {hasErrors && (
                  <div className="import-step-mode-card">
                    {errorList.length > 0 && (
                      <Button type="primary" onClick={showCSVModal}>
                        View Errors
                      </Button>
                    )}
                    <Button danger onClick={tryAgain}>
                      Try Again
                    </Button>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>

      {(errorList.length > 0 || processError !== null) && (
        <PlanarianModal
          fullScreen
          header="Import Entrance Errors"
          open={isModalOpen}
          onClose={handleOk}
          footer={null}
        >
          <CSVDisplay data={convertErrorListToCsv(errorList)} />
        </PlanarianModal>
      )}

      {dryRunData.length > 0 && (
        <PlanarianModal
          fullScreen
          header="Dry Run Data"
          open={isModalOpen}
          onClose={handleOk}
          footer={null}
        >
          <CSVDisplay data={Papa.unparse(dryRunData)} />
        </PlanarianModal>
      )}
    </>
  );
};

export { ImportEntrancesComponent };
