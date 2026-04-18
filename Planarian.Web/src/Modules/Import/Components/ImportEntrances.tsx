import React, { useState } from "react";
import { Card, Result, Button, Radio, message } from "antd";
import {
  DeliveredProcedureOutlined,
  CheckCircleOutlined,
  RedoOutlined,
  EyeOutlined,
} from "@ant-design/icons";
import Papa from "papaparse";

// Importing components and services
import { UploadComponent } from "../../Files/Components/UploadComponent";
import { CSVDisplay } from "../../Files/Components/CsvDisplayComponent";

// Importing models
import { ImportApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { FileVm } from "../../Files/Models/FileVm";

// Importing styles
import "./ImportComponent.scss";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { SignalRProgressComponent } from "../../../Shared/Components/SignalRProgress/SignalRProgressComponent";
import { Link } from "react-router-dom";
import { AccountService } from "../../Account/Services/AccountService";
import { EntranceCsvModel } from "../Models/EntranceCsvModel";
import { FailedCsvRecord } from "../Models/FailedCsvRecord";
import { EntranceDryRun } from "../Models/EntranceDryRun";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";

interface ImportEntrancesComponentProps {
  onUploaded: () => void;
}

const ImportEntrancesComponent: React.FC<ImportEntrancesComponentProps> = ({
  onUploaded,
}) => {
  // Initializing states
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

  // Handlers and functions
  const showCSVModal = () => setIsModalOpen(true);
  const handleOk = () => setIsModalOpen(false);
  const tryAgain = () => resetStates();
  const convertErrorListToCsv = (
    errorList: FailedCsvRecord<EntranceCsvModel>[]
  ): string =>
    Papa.unparse(
      errorList.map((error) => ({
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

  // Helper function to reset states
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

  return (
    <>
      {!isUploaded && !uploadFailed && errorList.length === 0 && (
        <UploadComponent
          draggerMessage="Click or drag your entrances CSV file to this area to upload."
          draggerTitle="Import Entrance File"
          hideCancelButton
          singleFile
          allowedFileTypes={[".csv"]}
          style={{ display: "flex" }}
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
      )}

      {uploadFailed && (
        <Card className="planarian-import-result-card" style={{ width: "100%" }}>
          <Result
            status="error"
            title="Upload Failed"
            subTitle="Please try uploading the file again."
            extra={[
              <PlanarianButton
                type="primary"
                danger
                onClick={tryAgain}
                icon={<RedoOutlined />}
              >
                Try Again
              </PlanarianButton>,
            ]}
          />
        </Card>
      )}

      {isUploaded &&
        !isProcessed &&
        !isProcessing &&
        processError === null &&
        !isDryRunComplete && (
          <Card className="planarian-import-result-card" style={{ width: "100%" }}>
            <Result
              icon={<CheckCircleOutlined style={{ color: "#52c41a" }} />}
              title="Successfully Uploaded!"
              subTitle="Run a dry run to preview exactly what will happen before applying the entrance import."
              extra={[
                <div
                  key="sync-actions"
                  style={{
                    display: "flex",
                    flexDirection: "column",
                    alignItems: "center",
                    gap: 12,
                  }}
                >
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
                  <div
                    style={{
                      maxWidth: 560,
                      textAlign: "center",
                    }}
                  >
                    {syncExisting
                      ? "For caves included in this CSV, existing entrances will be deleted and replaced with the entrances in the file. "
                      : "Only new entrances will be inserted. Existing entrances are left unchanged. "}
                    <strong>It is strongly recommended</strong> to create an
                    archive before running either mode. You can create one in{" "}
                    <Link to="/account/settings">Account Settings</Link>.
                  </div>
                  <div
                    style={{
                      display: "flex",
                      justifyContent: "center",
                      gap: 8,
                      flexWrap: "wrap",
                    }}
                  >
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
                </div>,
              ]}
            />
          </Card>
        )}

      {isDryRunComplete && !isProcessing && !isProcessed && (
        <Card className="planarian-import-result-card" style={{ width: "100%" }}>
          <Result
            icon={<CheckCircleOutlined style={{ color: "#52c41a" }} />}
            title="Dry Run Complete!"
            subTitle="Review the changes below. A count change of 0 means the number of entrances is unchanged, not that the entrance data itself is unchanged."
            extra={[
              <PlanarianButton
                alwaysShowChildren
                onClick={showCSVModal}
                icon={<EyeOutlined />}
              >
                View Dry Run Data
              </PlanarianButton>,
              <PlanarianButton
                alwaysShowChildren
                onClick={handleProcessClick}
                icon={<DeliveredProcedureOutlined />}
                loading={isLoading}
                type="primary"
              >
                Process
              </PlanarianButton>,
              <PlanarianButton
                alwaysShowChildren
                onClick={tryAgain}
                icon={<RedoOutlined />}
              >
                Reset
              </PlanarianButton>,
            ]}
          />
        </Card>
      )}

      {isProcessing && (
        <Card className="planarian-import-result-card" style={{ width: "100%" }}>
          <Result
            icon={<DeliveredProcedureOutlined style={{ color: "#1890ff" }} />}
            title="Processing..."
            subTitle="Please wait while your data is being processed."
            extra={[
              <SignalRProgressComponent
                groupName={uploadResult?.id as string}
                isLoading={isProcessing}
              />,
            ]}
          />
        </Card>
      )}

      {isProcessed && !isProcessing && (
        <Card style={{ width: "100%" }}>
          <Result
            icon={<CheckCircleOutlined style={{ color: "#52c41a" }} />}
            title="Successfully Processed!"
            subTitle="Your entrance CSV file has been successfully processed."
            extra={[
              <Link to="/caves">
                <Button type="primary" key="console" onClick={onUploaded}>
                  View Caves
                </Button>
              </Link>,
              <Button key="buy" onClick={tryAgain} icon={<RedoOutlined />}>
                Import Another Entrance File
              </Button>,
            ]}
          />
        </Card>
      )}

      {(errorList.length > 0 || processError !== null) && (
        <>
          <Card style={{ width: "100%" }}>
            <Result
              status="error"
              title="There were errors."
              subTitle={
                processError ||
                "Please click the button below to view the upload errors."
              }
              extra={[
                errorList.length > 0 && (
                  <Button type="primary" onClick={showCSVModal}>
                    View Errors
                  </Button>
                ),
                <Button type="primary" danger onClick={tryAgain}>
                  Try Again
                </Button>,
              ]}
            />
          </Card>
          <PlanarianModal
            fullScreen
            header="Import Entrance Errors"
            open={isModalOpen}
            onClose={handleOk}
            footer={null}
          >
            <CSVDisplay data={convertErrorListToCsv(errorList)} />
          </PlanarianModal>
        </>
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
