import React, { useState } from "react";
import { Card, Result, Button, Modal, message, Spin } from "antd";
import {
  DeliveredProcedureOutlined,
  CheckCircleOutlined,
  RedoOutlined,
} from "@ant-design/icons";
import Papa from "papaparse";

// Importing components and services
import { UploadComponent } from "../../Files/Components/UploadComponent";
import { CSVDisplay } from "../../Files/Components/CsvDisplayComponent";
import { CaveService } from "../../Caves/Service/CaveService";

// Importing models
import {
  FailedCaveRecord,
  ImportApiErrorResponse,
} from "../../../Shared/Models/ApiErrorResponse";
import { FileVm } from "../../Files/Models/FileVm";

// Importing styles
import "./ImportComponent.scss";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import NotificationComponent from "./NotificationComponent";

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
  const [errorList, setErrorList] = useState<FailedCaveRecord[]>([]);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [uploadResult, setUploadResult] = useState<FileVm | undefined>();
  const [isLoading, setIsLoading] = useState(false);

  // Handlers and functions
  const showCSVModal = () => setIsModalOpen(true);
  const handleOk = () => setIsModalOpen(false);
  const tryAgain = () => resetStates();
  const convertErrorListToCsv = (errorList: FailedCaveRecord[]): string =>
    Papa.unparse(
      errorList.map((error) => ({
        rowNumber: error.rowNumber,
        reason: error.reason,
        ...error.caveCsvModel,
      }))
    );
  const handleProcessClick = async () => {
    if (!uploadResult?.id) return message.error("File id not found");
    setIsLoading(true);
    try {
      await CaveService.ImportCavesFileProcess(uploadResult.id);
      setIsProcessed(true);
    } catch (e) {
      const error = e as ImportApiErrorResponse;
      setErrorList(error.data);
      setProcessError(error.message);
    } finally {
      setIsLoading(false);
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
              const result = await CaveService.ImportCavesFile(
                params.file,
                params.uid,
                params.onProgress
              );
              setUploadResult(result);
              setIsUploaded(true);
            } catch (e) {
              const error = e as ImportApiErrorResponse;

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

      {isUploaded && !isProcessed && processError === null && (
        <Card style={{ width: "100%" }}>
          <Spin spinning={isLoading}>
            <Result
              icon={<CheckCircleOutlined style={{ color: "#52c41a" }} />}
              title="Successfully Uploaded!"
              subTitle="Click the process button below to start the processing. If not, no caves will be imported."
              extra={[
                <PlanarianButton
                  onClick={handleProcessClick}
                  icon={<DeliveredProcedureOutlined />}
                  loading={isLoading}
                  type="primary"
                >
                  Process
                </PlanarianButton>,
                <Button onClick={tryAgain} icon={<RedoOutlined />}>
                  Try Again
                </Button>,
                <NotificationComponent
                  groupName={uploadResult?.id as string}
                />,
              ]}
            />
          </Spin>
        </Card>
      )}

      {isProcessed && (
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
          <Modal
            title="Import Cave Errors"
            open={isModalOpen}
            onOk={handleOk}
            onCancel={handleOk}
            footer={null}
          >
            <CSVDisplay data={convertErrorListToCsv(errorList)} />
          </Modal>
        </>
      )}
    </>
  );
};

export { ImportCaveComponent };