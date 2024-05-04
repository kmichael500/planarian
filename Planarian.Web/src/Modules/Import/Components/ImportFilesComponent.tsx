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
            example, if your county code is "AB" and your cave number follows
            it, using a delimiter like a dash would look like "AB-123". If you
            don't specify a delimter, it will be assumed that there is no
            delimter and that the county code and cave number are directly next
            to each other.
          </Typography.Paragraph>
          <Typography.Paragraph>
            The <strong>county code regex</strong> is a pattern that matches the
            format of your county codes. This is to tie the file with the
            correct cave. For instance, For example, the regex pattern '
            {`[A-Z]{2}`}' ensures each code is exactly two uppercase letters,
            such as "AB", "DB", or "BU" but it would not match "A", "ABC", or
            "Ab". You can use a site like{" "}
            <a href="https://regexr.com" target="_blank">
              https://regexr.com
            </a>{" "}
            to test your regex against your filenames to ensure it matches. The
            first match of the county code regex combined with the delimiter and
            followed by the cave number will be used to match the cave.
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
              label="County Code Regex"
              name={nameof<DelimiterFormFields>("countyCodeRegex")}
              rules={[
                {
                  required: true,
                  message: "Please input a county code regex!",
                },
              ]}
              initialValue=""
            >
              <Input placeholder="^\\d{3}$" />
            </Form.Item>
            <Form.Item>
              <Button type="primary" htmlType="submit">
                Confirm Settings
              </Button>
            </Form.Item>
          </Form>
        </Card>
      )}

      {inputsConfirmed && !uploadFailed && errorList.length === 0 && (
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
                form.getFieldValue("countyCodeRegex"),
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

interface DelimiterFormFields {
  delimiter: string;
  countyCodeRegex: string;
}
