import React, { useState } from "react";
import { Alert, Button, Radio, Spin, Typography, message } from "antd";
import {
  CheckCircleOutlined,
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

const { Title, Paragraph, Text } = Typography;

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
                      Cave CSV
                    </div>
                    <Title level={4} style={{ margin: 0 }}>
                      Upload Cave Data
                    </Title>
                    <Paragraph style={{ marginBottom: 0 }}>
                      Start with the cave file. Entrances depend on these cave
                      records already existing.
                    </Paragraph>
                  </div>
                </div>
                <div className="import-step-panel__body import-step-panel__body--plain">
                  <UploadComponent
                    draggerMessage="Click or drag your caves CSV file to this area to upload."
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
                            Cave CSV uploaded
                          </Title>
                          <Paragraph>
                            Run a dry run before processing so you can review
                            inserts, updates, and deletes.
                          </Paragraph>
                          <Paragraph style={{ marginBottom: 0 }}>
                            {syncExisting
                              ? "Insert / Update mode will reconcile the cave records in the database to this CSV."
                              : "Insert Only mode will add only new caves and fail if a cave already exists."}
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
                        <EyeOutlined />
                      </span>
                      <div className="import-step-status__copy">
                        <Title level={4} style={{ marginTop: 0 }}>
                          Dry run complete
                        </Title>
                        <Paragraph>
                          Review the dry run output, then process the import if
                          the changes look correct.
                        </Paragraph>
                        <Paragraph style={{ marginBottom: 0 }}>
                          {dryRunData.length} change
                          {dryRunData.length === 1 ? "" : "s"} will be applied.
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
                          Processing cave import
                        </Title>
                        <Paragraph>
                          Keep this page open while Planarian processes the
                          uploaded cave data.
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
                          Cave import complete
                        </Title>
                        <Paragraph style={{ marginBottom: 0 }}>
                          Your cave CSV has been processed successfully. Continue
                          to the entrance import when you are ready.
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
                          Cave import needs attention
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
                    <Text>1. Upload caves</Text>
                    <Text>2. Dry run the changes</Text>
                    <Text>3. Process the import</Text>
                    <Text>4. Continue to entrances</Text>
                  </div>
                )}

                {isUploaded &&
                  !isProcessing &&
                  !isProcessed &&
                  !isDryRunComplete &&
                  !hasErrors && (
                    <div className="import-step-mode-card">
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
                        Create an archive first in{" "}
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
                      Open the dry run data if you need the full row-by-row
                      output, then process the import.
                    </Paragraph>
                    <div className="import-step-actions">
                      <Button onClick={showCSVModal} icon={<EyeOutlined />}>
                        View Dry Run Data
                      </Button>
                      <PlanarianButton
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
                    </div>
                  </div>
                )}

                {isProcessing && (
                  <Paragraph style={{ marginBottom: 0 }}>
                    Progress updates are shown live while the server processes
                    the import.
                  </Paragraph>
                )}

                {isProcessed && !isProcessing && (
                  <div className="import-step-actions">
                    <Button type="primary" onClick={onUploaded}>
                      Import Entrances
                    </Button>
                    <Button onClick={tryAgain} icon={<RedoOutlined />}>
                      Import Another Cave File
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
          header="Import Cave Errors"
          open={isModalOpen}
          onClose={handleOk}
          footer={null}
        >
          <Spin spinning={errorList.length === 0}>
            <CSVDisplay data={convertErrorListToCsv(errorList)} />
          </Spin>
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
          <CSVDisplay data={buildDryRunCsv(dryRunData)} />
        </PlanarianModal>
      )}
    </>
  );
};

export { ImportCaveComponent };
