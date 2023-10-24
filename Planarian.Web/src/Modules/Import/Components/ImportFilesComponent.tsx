import React, { useState } from "react";
import { Card, Result, Button, Modal, message } from "antd";
import {
  DeliveredProcedureOutlined,
  CheckCircleOutlined,
  RedoOutlined,
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
import { FullScreenModal } from "../../Files/Components/FileListItemComponent";
import { AccountService } from "../../Account/Services/AccountService";
import { FailedCsvRecord } from "../Models/FailedCsvRecord";

interface ImportCaveComponentProps {
  onUploaded: () => void;
}

const ImportFilesComponent: React.FC<ImportCaveComponentProps> = ({
  onUploaded,
}) => {
  // Initializing states
  const [isUploaded, setIsUploaded] = useState(false);
  const [uploadFailed, setUploadFailed] = useState(false);
  const [errorList, setErrorList] = useState<FailedCsvRecord<CaveCsvModel>[]>(
    []
  );
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [uploadResult, setUploadResult] = useState<FileVm | undefined>();
  const [isLoading, setIsLoading] = useState(false);

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

  // Helper function to reset states
  const resetStates = () => {
    setIsUploaded(false);
    setUploadFailed(false);
    setErrorList([]);
    setUploadResult(undefined);
  };

  return (
    <>
      {!isUploaded && !uploadFailed && errorList.length === 0 && (
        <UploadComponent
          draggerMessage="Drag any files you would like to upload."
          draggerTitle="Import Cave Files"
          hideCancelButton
          style={{ display: "flex" }}
          uploadFunction={async (params): Promise<FileVm> => {
            try {
              const result = await AccountService.ImportFile(
                params.file,
                params.uid,
                params.onProgress
              );
              // setUploadResult(result);
              // setIsUploaded(true);
            } catch (e) {
              const error = e as ImportApiErrorResponse<CaveCsvModel>;

              console.log("Failed", params.file, error);
              // setUploadFailed(true);

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

      {isUploaded && (
        <Card style={{ width: "100%" }}>
          <Result
            icon={<CheckCircleOutlined style={{ color: "#52c41a" }} />}
            title="Successfully Uploaded!"
            subTitle="Click the process button below to start processing. If not, no caves will be imported."
            extra={[
              <Button onClick={tryAgain} icon={<RedoOutlined />}>
                Reset
              </Button>,
            ]}
          />
        </Card>
      )}

      {errorList.length > 0 && (
        <>
          <Card style={{ width: "100%" }}>
            <Result
              status="error"
              title="There were errors."
              subTitle={
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
          <FullScreenModal>
            <Modal
              title="Import Cave Files Errors"
              open={isModalOpen}
              onOk={handleOk}
              onCancel={handleOk}
              footer={null}
            >
              <CSVDisplay data={convertErrorListToCsv(errorList)} />
            </Modal>
          </FullScreenModal>
        </>
      )}
    </>
  );
};

export { ImportFilesComponent };
