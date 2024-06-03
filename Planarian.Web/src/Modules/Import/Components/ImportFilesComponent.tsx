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
import { FullScreenModal } from "../../Files/Components/FileListItemComponent";
import { AccountService } from "../../Account/Services/AccountService";
import { FailedCsvRecord } from "../Models/FailedCsvRecord";
import { nameof } from "../../../Shared/Helpers/StringHelpers";

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

  const [form] = Form.useForm<DelimiterFormFields>();
  const [inputsConfirmed, setInputsConfirmed] = useState(false);

  // Handlers and functions
  const showCSVModal = () => setIsModalOpen(true);
  const handleOk = () => setIsModalOpen(false);
  const tryAgain = () => resetStates();
  const confirmInputs = () => {
    form.submit();
  };

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

      {!isUploaded &&
        inputsConfirmed &&
        !uploadFailed &&
        errorList.length === 0 && (
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
                  form.getFieldValue("delimiter"),
                  form.getFieldValue("idRegex"),
                  params.onProgress
                );
                setUploadResult(result);
                setIsUploaded(true);
              } catch (e) {
                const error = e as ImportApiErrorResponse<CaveCsvModel>;
                setUploadFailed(true);
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

      {isUploaded && (
        <Card style={{ width: "100%" }}>
          <Result
            icon={<CheckCircleOutlined style={{ color: "#52c41a" }} />}
            title="Successfully Uploaded!"
            subTitle="Your files have been successfully uploaded."
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

interface DelimiterFormFields {
  delimiter: string;
  idRegex: string;
}
