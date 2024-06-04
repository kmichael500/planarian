import React, { useState } from "react";
import {
  Card,
  Result,
  Button,
  Modal,
  message,
  Input,
  Form,
  Typography,
} from "antd";
import { CheckCircleOutlined, RedoOutlined } from "@ant-design/icons";
import Papa from "papaparse";

import { UploadComponent } from "../../Files/Components/UploadComponent";
import { CSVDisplay } from "../../Files/Components/CsvDisplayComponent";

import {
  ApiErrorResponse,
  ImportApiErrorResponse,
} from "../../../Shared/Models/ApiErrorResponse";
import { CaveCsvModel } from "../Models/CaveCsvModel";
import { FileVm } from "../../Files/Models/FileVm";

import "./ImportComponent.scss";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { FullScreenModal } from "../../Files/Components/FileListItemComponent";
import { AccountService } from "../../Account/Services/AccountService";
import { nameof } from "../../../Shared/Helpers/StringHelpers";
import { FileImportResult } from "../Models/FileUploadresult";

interface ImportCaveComponentProps {
  onUploaded: () => void;
}

const ImportFilesComponent: React.FC<ImportCaveComponentProps> = ({
  onUploaded,
}) => {
  const [isUploaded, setIsUploaded] = useState(false);
  const [uploadFailed, setUploadFailed] = useState(false);
  const [partialUpload, setPartialUpload] = useState(false);

  const [uploadResults, setUploadResults] = useState<FileImportResult[]>([]);
  const [filesToUpload, setFilesToUpload] = useState(0);

  const [isModalOpen, setIsModalOpen] = useState(false);

  const [form] = Form.useForm<DelimiterFormFields>();
  const [inputsConfirmed, setInputsConfirmed] = useState(false);

  const showCSVModal = () => setIsModalOpen(true);
  const handleOk = () => setIsModalOpen(false);
  const tryAgain = () => resetStates();

  const convertResultsListToCsv = (
    uploadResults: FileImportResult[]
  ): string => {
    return Papa.unparse(uploadResults);
  };
  const resetStates = () => {
    setIsUploaded(false);
    setUploadFailed(false);
    setUploadResults([]);
    setPartialUpload(false);
  };

  const determineUploadStatus = () => {
    const successfulUploads = uploadResults.filter(
      (result) => result.isSuccessful
    );
    const failedUploads = uploadResults.filter(
      (result) => !result.isSuccessful
    );

    if (successfulUploads.length > 0 && failedUploads.length > 0) {
      setPartialUpload(true);
    } else if (failedUploads.length === uploadResults.length) {
      setUploadFailed(true);
    } else {
      setIsUploaded(true);
    }
  };

  const handleUpload = async (params: any) => {
    try {
      const result = await AccountService.ImportFile(
        params.file,
        params.uid,
        form.getFieldValue("delimiter"),
        form.getFieldValue("idRegex"),
        params.onProgress
      );

      setUploadResults((prevResults) => [...prevResults, result]);

      setFilesToUpload((prevCount) => prevCount - 1);
    } catch (e) {
      const error = e as ApiErrorResponse;
      setUploadFailed(true);
      message.error(error.message);
      setFilesToUpload((prevCount) => prevCount - 1);
    }
  };

  React.useEffect(() => {
    if (filesToUpload === 0 && uploadResults.length > 0) {
      determineUploadStatus();
    }
  }, [filesToUpload, uploadResults]);

  return (
    <>
      {!inputsConfirmed && (
        <Card style={{ width: "100%", marginBottom: "20px" }}>
          <Typography.Title level={4}>File Import Settings</Typography.Title>
          <Typography.Paragraph>
            A <strong>delimiter</strong> is a character that separates the
            county code and the county cave number in your file name. For
            example, if your county code is "1" and your cave number follows it,
            using a delimiter like a dash would look like "1-3". If you don't
            specify a delimiter, it will be assumed that there is no delimiter
            and that the county code and cave number are directly next to each
            other.
          </Typography.Paragraph>
          <Typography.Paragraph>
            The <strong>ID regex</strong> is a pattern that matches the entire
            ID format in your filenames. This includes both the county code and
            the cave number. For instance, the regex pattern '{`\\d+-\\d+`}'
            ensures it matches IDs like "1-3", "12-34", etc. You can use a site
            like{" "}
            <a href="https://regexr.com" target="_blank">
              https://regexr.com
            </a>{" "}
            to test your regex against your filenames to ensure it matches.
          </Typography.Paragraph>

          <Form
            form={form}
            layout="vertical"
            onFinish={(values) => {
              setInputsConfirmed(true);
            }}
          >
            <Form.Item
              label="Delimiter"
              name={nameof<DelimiterFormFields>("delimiter")}
              initialValue=""
            >
              <Input placeholder="-" />
            </Form.Item>
            <Form.Item
              label="ID Regex"
              name={nameof<DelimiterFormFields>("idRegex")}
              rules={[
                {
                  required: true,
                  message: "Please input an ID regex!",
                },
              ]}
              initialValue=""
            >
              <Input placeholder="\d+-\d+" />
            </Form.Item>
            <Form.Item>
              <Button type="primary" htmlType="submit">
                Confirm Settings
              </Button>
            </Form.Item>
          </Form>
        </Card>
      )}

      {!isUploaded && inputsConfirmed && !uploadFailed && !partialUpload && (
        <UploadComponent
          draggerMessage="Drag any files you would like to upload."
          draggerTitle="Import Cave Files"
          hideCancelButton
          style={{ display: "flex" }}
          uploadFunction={async (params): Promise<FileVm> => {
            setFilesToUpload((prevCount) => prevCount + 1);
            await handleUpload(params);
            return {} as FileVm;
          }}
          updateFunction={() => {
            throw new Error("Function not implemented intentionally.");
          }}
        />
      )}

      {isUploaded && (
        <Card style={{ width: "100%" }}>
          <Result
            icon={<CheckCircleOutlined style={{ color: "#52c41a" }} />}
            title="Successfully Uploaded!"
            subTitle="Your files have been successfully uploaded."
            extra={[
              <Button type="primary" onClick={showCSVModal}>
                View Results
              </Button>,
              <PlanarianButton
                type="primary"
                danger
                onClick={tryAgain}
                icon={<RedoOutlined />}
              >
                Upload More
              </PlanarianButton>,
              <PlanarianButton
                onClick={() => {
                  resetStates();
                  setInputsConfirmed(false);
                }}
                icon={<RedoOutlined />}
              >
                Reset
              </PlanarianButton>,
            ]}
          />
        </Card>
      )}

      {partialUpload && (
        <Card style={{ width: "100%" }}>
          <Result
            icon={<CheckCircleOutlined style={{ color: "#FFA500" }} />}
            title="Partially Uploaded!"
            subTitle="Your files have been partially uploaded. However, there were some errors. Please review the results."
            extra={[
              <Button type="primary" onClick={showCSVModal}>
                View Results
              </Button>,
              <PlanarianButton
                type="primary"
                danger
                onClick={tryAgain}
                icon={<RedoOutlined />}
              >
                Upload More
              </PlanarianButton>,
              <PlanarianButton
                onClick={() => {
                  resetStates();
                  setInputsConfirmed(false);
                }}
                icon={<RedoOutlined />}
              >
                Reset
              </PlanarianButton>,
            ]}
          />
        </Card>
      )}

      {uploadFailed && (
        <Card style={{ width: "100%" }}>
          <Result
            status="error"
            title="Upload Failed"
            subTitle="Please try uploading the files again."
            extra={[
              <Button type="primary" onClick={showCSVModal}>
                View Results
              </Button>,
              <PlanarianButton
                type="primary"
                danger
                onClick={tryAgain}
                icon={<RedoOutlined />}
              >
                Try Again
              </PlanarianButton>,
              <PlanarianButton
                onClick={() => {
                  resetStates();
                  setInputsConfirmed(false);
                }}
                icon={<RedoOutlined />}
              >
                Reset
              </PlanarianButton>,
            ]}
          />
        </Card>
      )}

      <FullScreenModal>
        <Modal
          title="File Import Results"
          open={isModalOpen}
          onOk={handleOk}
          onCancel={handleOk}
          footer={null}
        >
          <CSVDisplay data={convertResultsListToCsv(uploadResults)} />
        </Modal>
      </FullScreenModal>
    </>
  );
};

export { ImportFilesComponent };

interface DelimiterFormFields {
  delimiter: string;
  idRegex: string;
}
