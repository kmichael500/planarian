import React, { useState } from "react";
import { Card, Result, Button, Modal, message, Spin } from "antd";
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
import { CaveCsvModel } from "../Models/CaveCsvModel";
import { FileVm } from "../../Files/Models/FileVm";

// Importing styles
import "./ImportComponent.scss";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { NotificationComponent } from "./NotificationComponent";
import { AccountService } from "../../Account/Services/AccountService";
import { FailedCsvRecord } from "../Models/FailedCsvRecord";
import { CaveDryRunRecord } from "../Models/CaveDryRunRecord";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";

interface ImportCaveComponentProps {
  onUploaded: () => void;
}

const ImportCaveComponent: React.FC<ImportCaveComponentProps> = ({
  onUploaded,
}) => {
  // Initializing states
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

  // Handlers and functions
  const showCSVModal = () => setIsModalOpen(true);
  const handleOk = () => setIsModalOpen(false);
  const tryAgain = () => resetStates();
  const convertErrorListToCsv = (
    errorList: FailedCsvRecord<CaveCsvModel>[]
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
      const result = await AccountService.ImportCavesFileProcess(
        uploadResult.id,
        true
      );
      setDryRunData(result);
      setIsDryRunComplete(true);
    } catch (e) {
      const error = e as ImportApiErrorResponse<CaveCsvModel>;

      if (error.data) {
        setErrorList(error.data);
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
      await AccountService.ImportCavesFileProcess(uploadResult.id);
      setIsProcessed(true);
    } catch (e) {
      const error = e as ImportApiErrorResponse<CaveCsvModel>;

      if (error.data) {
        setErrorList(error.data);
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
  };

  return (
    <>
      {!isUploaded && !uploadFailed && errorList.length === 0 && (
        <UploadComponent
          draggerMessage="Click or drag your caves CSV file to this area to upload."
          draggerTitle="Import Cave File"
          hideCancelButton
          singleFile
          allowedFileTypes={[".csv"]}
          style={{ display: "flex" }}
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
      )}

      {uploadFailed && (
        <Card style={{ width: "100%" }}>
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
          <Card style={{ width: "100%" }}>
            <Result
              icon={<CheckCircleOutlined style={{ color: "#52c41a" }} />}
              title="Successfully Uploaded!"
              subTitle="Click the dry run button below to preview the changes. If not, no caves will be imported."
              extra={[
                <PlanarianButton
                  onClick={handleDryRunClick}
                  icon={<EyeOutlined />}
                  loading={isLoading}
                  type="primary"
                >
                  Dry Run
                </PlanarianButton>,
                <PlanarianButton onClick={tryAgain} icon={<RedoOutlined />}>
                  Reset
                </PlanarianButton>,
              ]}
            />
          </Card>
        )}

      {isDryRunComplete && !isProcessing && !isProcessed && (
        <Card style={{ width: "100%" }}>
          <Result
            icon={<CheckCircleOutlined style={{ color: "#52c41a" }} />}
            title="Dry Run Complete!"
            subTitle="Review the changes below. If everything looks good, proceed with processing."
            extra={[
              <Button onClick={showCSVModal} icon={<EyeOutlined />}>
                View Dry Run Data
              </Button>,
              <PlanarianButton
                onClick={handleProcessClick}
                icon={<DeliveredProcedureOutlined />}
                loading={isLoading}
                type="primary"
              >
                Process
              </PlanarianButton>,
              <Button onClick={tryAgain} icon={<RedoOutlined />}>
                Reset
              </Button>,
            ]}
          />
        </Card>
      )}

      {isProcessing && (
        <Card style={{ width: "100%" }}>
          <Result
            icon={<DeliveredProcedureOutlined style={{ color: "#1890ff" }} />}
            title="Processing..."
            subTitle="Please wait while your data is being processed."
            extra={[
              <NotificationComponent
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
            subTitle="Your cave CSV file has been successfully processed."
            extra={[
              <Button type="primary" key="console" onClick={onUploaded}>
                Import Entrances
              </Button>,
              <Button key="buy" onClick={tryAgain} icon={<RedoOutlined />}>
                Import Another Cave File
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
            header="Import Cave Errors"
            open={isModalOpen}
            onClose={handleOk}
            footer={null}
          >
            <Spin spinning={errorList.length === 0}>
              <CSVDisplay data={convertErrorListToCsv(errorList)} />
            </Spin>
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

export { ImportCaveComponent };
