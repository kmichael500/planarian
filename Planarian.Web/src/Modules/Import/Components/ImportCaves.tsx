import { Card, Result, Button, Modal, message, Spin } from "antd";
import React from "react";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import {
  DeliveredProcedureOutlined,
  CheckCircleOutlined,
} from "@ant-design/icons";
import { UploadComponent } from "../../Files/Components/UploadComponent";
import { CaveService } from "../../Caves/Service/CaveService";
import { CSVDisplay } from "../../Files/Components/CsvDisplayComponent";
import {
  FailedCaveRecord,
  ImportApiErrorResponse,
} from "../../../Shared/Models/ApiErrorResponse";
import { FileVm } from "../../Files/Models/FileVm";
import Papa from "papaparse";
import "./ImportComponent.scss";

export interface ImportCaveComponentProps {
  onUploaded: () => void;
}

const ImportCaveComponent = ({ onUploaded }: ImportCaveComponentProps) => {
  const [isUploaded, setIsUploaded] = React.useState(false);
  const [isProcessed, setIsProcessed] = React.useState(false);
  const [errorList, setErrorList] = React.useState<FailedCaveRecord[]>([]);
  const [isModalVisible, setIsModalVisible] = React.useState(false);
  const [uploadResult, setUploadResult] = React.useState<FileVm>();
  const [processError, setProcessError] = React.useState<string | null>(null);
  const [isLoading, setIsLoading] = React.useState(false);

  const showCSVModal = () => {
    setIsModalVisible(true);
  };

  const handleOk = () => {
    setIsModalVisible(false);
  };

  const tryAgain = () => {
    setIsUploaded(false);
    setIsProcessed(false);
    setProcessError(null);
    setErrorList([]);
    setUploadResult(undefined);
  };

  const convertErrorListToCsv = (errorList: FailedCaveRecord[]): string => {
    const csv = Papa.unparse(
      errorList.map((error) => ({
        rowNumber: error.rowNumber,
        reason: error.reason,
        ...error.caveCsvModel,
      }))
    );
    return csv;
  };

  const handleProcessClick = async () => {
    if (!uploadResult?.id) throw new Error("File id not found");
    try {
      setIsLoading(true);
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

  return (
    <>
      {!isUploaded && errorList.length <= 0 && (
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
              setErrorList(error.data);
              message.error(error.message);
            }
            return {} as FileVm;
          }}
          updateFunction={() => {
            throw new Error("Function not implemented intentionally.");
          }}
        />
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
                >
                  Process
                </PlanarianButton>,
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
              <Button key="buy" onClick={tryAgain}>
                Import Another File
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
                    View Error CSV
                  </Button>
                ),
                <Button type="primary" danger onClick={tryAgain}>
                  Try Again
                </Button>,
              ]}
            />
          </Card>
          <Modal
            title="Error CSV"
            visible={isModalVisible}
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
