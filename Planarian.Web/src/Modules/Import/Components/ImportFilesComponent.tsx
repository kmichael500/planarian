import {
  message,
  Progress,
  Space,
  Button,
  Card,
  Typography,
  Checkbox,
  Col,
  Form,
  Input,
  Modal,
  Result,
  Row,
} from "antd";
import { CancelButtonComponent } from "../../../Shared/Components/Buttons/CancelButtonComponent";
import { FileVm } from "../../Files/Models/FileVm";
import { getFileType } from "../../Files/Services/FileHelpers";
import {
  CheckCircleOutlined,
  InboxOutlined,
  RedoOutlined,
} from "@ant-design/icons";
import { useState, useEffect, useRef } from "react";
import Papa from "papaparse";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { nameof } from "../../../Shared/Helpers/StringHelpers";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { AccountService } from "../../Account/Services/AccountService";
import { CSVDisplay } from "../../Files/Components/CsvDisplayComponent";
import { FullScreenModal } from "../../Files/Components/FileListItemComponent";
import { FileImportResult } from "../Models/FileUploadresult";

export interface UploadParams {
  file: any;
  uid: string;
  onProgress: (event: { loaded: number; total: number }) => void;
  [key: string]: any;
}

export interface UploadComponentProps {
  onClose?: () => void;
  uploadFunction: (params: UploadParams) => Promise<FileVm>;
  allowedFileTypes?: string[];
  hideCancelButton?: boolean;
  draggerMessage?: string;
  draggerTitle?: string;
  singleFile?: boolean;

  uploadConcurrency?: number;
}

interface QueuedFile {
  file: File;
  uid: string;
  progress: number;
  active: boolean;
}

export const UploadComponent: React.FC<UploadComponentProps> = ({
  onClose,
  uploadFunction,
  hideCancelButton,
  draggerMessage = "Drag and drop files or click to select files",
  draggerTitle = "Upload Files",
  singleFile,
  allowedFileTypes,
  uploadConcurrency = 3,
}) => {
  const [queuedFiles, setQueuedFiles] = useState<QueuedFile[]>([]);
  const [uploading, setUploading] = useState(false);
  const [uploaded, setUploaded] = useState(false);

  // Validate file type and size
  const beforeUpload = (file: File): boolean => {
    const fileType = getFileType(file.name);
    const processedAllowedFileTypes = allowedFileTypes?.map((type) =>
      type.startsWith(".") ? type.substr(1).toLowerCase() : type.toLowerCase()
    );
    if (
      fileType &&
      processedAllowedFileTypes &&
      !processedAllowedFileTypes.includes(fileType.toLowerCase())
    ) {
      message.error(
        `File type ${fileType} is not allowed. Allowed file types are ${processedAllowedFileTypes.join(
          ", "
        )}.`
      );
      return false;
    }
    const fileSizeInMB = file.size / 1024 / 1024;
    const maxSizeInMB = 500;
    if (fileSizeInMB > maxSizeInMB) {
      message.error(`File size should not exceed ${maxSizeInMB} MB.`);
      return false;
    }
    return true;
  };

  // Process files from drag/drop or file input
  const processFiles = (fileList: FileList) => {
    const newFiles: QueuedFile[] = [];
    Array.from(fileList).forEach((file, index) => {
      if (beforeUpload(file)) {
        newFiles.push({
          file,
          uid: `${file.name}-${Date.now()}-${index}`,
          progress: 0,
          active: false,
        });
      }
    });
    if (singleFile && newFiles.length > 0) {
      setQueuedFiles([newFiles[0]]);
    } else {
      setQueuedFiles((prev) => [...prev, ...newFiles]);
    }
  };

  // Handle drop and drag over events
  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    if (e.dataTransfer.files) {
      processFiles(e.dataTransfer.files);
    }
  };

  const handleDragOver = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
  };

  // File input change event
  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) {
      processFiles(e.target.files);
    }
  };

  // Helper to run tasks concurrently with a concurrency limit
  const runWithConcurrency = async (
    tasks: (() => Promise<void>)[],
    concurrency: number
  ) => {
    let index = 0;
    const next = async () => {
      if (index >= tasks.length) return;
      const currentTask = tasks[index];
      index++;
      await currentTask();
      await next();
    };
    const workers = [];
    for (let i = 0; i < Math.min(concurrency, tasks.length); i++) {
      workers.push(next());
    }
    await Promise.all(workers);
  };

  // Trigger file uploads with concurrent control
  const handleUpload = async () => {
    if (queuedFiles.length === 0) {
      message.error("No files selected for upload.");
      return;
    }
    setUploading(true);

    const tasks = queuedFiles.map((queued) => async () => {
      // Mark the file as active once its upload starts
      setQueuedFiles((prev) =>
        prev.map((f) => (f.uid === queued.uid ? { ...f, active: true } : f))
      );
      try {
        await uploadFunction({
          file: queued.file,
          uid: queued.uid,
          onProgress: (event: { loaded: number; total: number }) => {
            const percent = Math.round(
              (100 * event.loaded) / (event.total || 1)
            );
            setQueuedFiles((prev) =>
              prev.map((f) =>
                f.uid === queued.uid ? { ...f, progress: percent } : f
              )
            );
          },
        });
      } catch (error) {
        message.error(`Error uploading file ${queued.file.name}`);
      }
    });

    await runWithConcurrency(tasks, uploadConcurrency);
    setUploaded(true);
    setUploading(false);
  };

  // Determine which files to show: before upload, show all queued;
  // once uploading, show only files that have been marked as active and are not yet complete.
  const filesToShow = queuedFiles;

  // Compute remaining files count from the displayed files
  const remainingFiles = filesToShow.filter((f) => f.progress < 100).length;
  const totalFiles = filesToShow.length;

  return (
    <>
      {!uploaded ? (
        <Card
          onDrop={handleDrop}
          onDragOver={handleDragOver}
          style={{
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            justifyContent: "center",
            width: "100%",
            marginBottom: "20px",
            border: "2px dashed #ccc",
            padding: "20px",
            textAlign: "center",
            cursor: "pointer",
          }}
        >
          <InboxOutlined style={{ fontSize: "48px", color: "#40a9ff" }} />
          <p
            className="ant-upload-title"
            style={{
              fontSize: "24px",
              fontWeight: "bold",
              marginBottom: "8px",
            }}
          >
            {draggerTitle}
          </p>
          <p className="ant-upload-text">{draggerMessage}</p>
          <input
            id="fileInput"
            type="file"
            multiple={!singleFile}
            style={{ display: "none" }}
            onChange={handleFileSelect}
          />
          {filesToShow.length > 0 && (
            <>
              {/* Display remaining files indicator */}
              <div
                style={{
                  width: "100%",
                  textAlign: "right",
                  marginBottom: "5px",
                  fontSize: "12px",
                  color: "#888",
                }}
              >
                {remainingFiles} of {totalFiles} actively uploading
              </div>
              <div
                style={{
                  maxHeight: "200px",
                  overflowY: "scroll",
                  marginBottom: "20px",
                }}
              >
                {filesToShow.map((f) => (
                  <div key={f.uid} style={{ marginBottom: "10px" }}>
                    <div>{f.file.name}</div>
                    <Progress percent={f.progress} size="small" />
                  </div>
                ))}
              </div>
            </>
          )}
          <Space>
            <Button
              onClick={() => document.getElementById("fileInput")?.click()}
            >
              Choose Files
            </Button>
            <Button
              type="primary"
              onClick={handleUpload}
              disabled={uploading || queuedFiles.length === 0}
            >
              {uploading ? "Uploading..." : "Upload Files"}
            </Button>
            {!uploading && !hideCancelButton && (
              <CancelButtonComponent onClick={onClose} />
            )}
          </Space>
        </Card>
      ) : (
        <Card style={{ textAlign: "center", marginBottom: "20px" }}>
          <Typography.Title level={3}>Upload Complete!</Typography.Title>
          <Button type="primary" onClick={onClose}>
            Close
          </Button>
        </Card>
      )}
    </>
  );
};

interface DelimiterFormFields {
  delimiter: string;
  idRegex: string;
  ignoreDuplicates: boolean;
}

interface ImportCaveComponentProps {
  onUploaded: () => void;
}

export const ImportFilesComponent: React.FC<ImportCaveComponentProps> = ({
  onUploaded,
}) => {
  const [isUploaded, setIsUploaded] = useState(false);
  const [uploadFailed, setUploadFailed] = useState(false);
  const [partialUpload, setPartialUpload] = useState(false);
  const [uploadResults, setUploadResults] = useState<FileImportResult[]>([]);
  const [filesToUpload, setFilesToUpload] = useState(0);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [inputsConfirmed, setInputsConfirmed] = useState(false);
  const [form] = Form.useForm<DelimiterFormFields>();

  const showCSVModal = () => setIsModalOpen(true);
  const handleOk = () => setIsModalOpen(false);

  const resetStates = () => {
    setIsUploaded(false);
    setUploadFailed(false);
    setPartialUpload(false);
    setUploadResults([]);
  };

  const convertResultsListToCsv = (results: FileImportResult[]): string => {
    return Papa.unparse(results);
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
    setFilesToUpload((prev) => prev + 1);
    try {
      const result = await AccountService.ImportFile(
        params.file,
        params.uid,
        form.getFieldValue("delimiter"),
        form.getFieldValue("idRegex"),
        form.getFieldValue("ignoreDuplicates"),
        params.onProgress
      );
      setUploadResults((prevResults) => [...prevResults, result]);
    } catch (e) {
      const error = e as ApiErrorResponse;

      // add result to list of results
      setUploadResults((prevResults) => [
        ...prevResults,
        {
          fileName: params.file.name,
          isSuccessful: false,
          error: error.message,
          associatedCave: "",
          message: error.message,
        },
      ]);
    } finally {
      setFilesToUpload((prev) => prev - 1);
    }
  };

  useEffect(() => {
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
            county code and the cave number in your file name. For example, if
            your county code is "1" and your cave number follows it, using a
            delimiter like a dash would look like "1-3". If you don't specify a
            delimiter, it will be assumed that there is no delimiter.
          </Typography.Paragraph>
          <Typography.Paragraph>
            The <strong>ID regex</strong> is a pattern that matches the entire
            ID format in your filenames. For instance, the regex pattern{" "}
            {"{\\d+-\\d+}"} ensures it matches IDs like "1-3", "12-34", etc.
          </Typography.Paragraph>
          <Form
            form={form}
            layout="vertical"
            onFinish={() => setInputsConfirmed(true)}
          >
            <Row gutter={16}>
              <Col span={8}>
                <Form.Item
                  label="Delimiter"
                  name={nameof<DelimiterFormFields>("delimiter")}
                  initialValue=""
                >
                  <Input placeholder="-" />
                </Form.Item>
              </Col>
              <Col span={8}>
                <Form.Item
                  label="ID Regex"
                  name={nameof<DelimiterFormFields>("idRegex")}
                  rules={[
                    { required: true, message: "Please input an ID regex!" },
                  ]}
                >
                  <Input placeholder="\\d+-\\d+" />
                </Form.Item>
              </Col>
              <Col span={8}>
                <Form.Item
                  name={nameof<DelimiterFormFields>("ignoreDuplicates")}
                  label="Ignore Duplicates"
                  valuePropName="checked"
                  initialValue={true}
                >
                  <Checkbox />
                </Form.Item>
              </Col>
            </Row>
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
          uploadFunction={async (params): Promise<any> => {
            await handleUpload(params);
            return {};
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
              <Button type="primary" onClick={showCSVModal} key="view">
                View Results
              </Button>,
              <PlanarianButton
                type="dashed"
                onClick={resetStates}
                icon={<RedoOutlined />}
                key="uploadMore"
              >
                Upload More
              </PlanarianButton>,
              <PlanarianButton
                onClick={() => {
                  resetStates();
                  setInputsConfirmed(false);
                }}
                icon={<RedoOutlined />}
                key="reset"
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
              <Button type="primary" onClick={showCSVModal} key="view">
                View Results
              </Button>,
              <PlanarianButton
                type="dashed"
                onClick={resetStates}
                icon={<RedoOutlined />}
                key="uploadMore"
              >
                Upload More
              </PlanarianButton>,
              <PlanarianButton
                onClick={() => {
                  resetStates();
                  setInputsConfirmed(false);
                }}
                icon={<RedoOutlined />}
                key="reset"
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
              <Button type="primary" onClick={showCSVModal} key="view">
                View Results
              </Button>,
              <PlanarianButton
                type="primary"
                danger
                onClick={resetStates}
                icon={<RedoOutlined />}
                key="tryAgain"
              >
                Try Again
              </PlanarianButton>,
              <PlanarianButton
                onClick={() => {
                  resetStates();
                  setInputsConfirmed(false);
                }}
                icon={<RedoOutlined />}
                key="reset"
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
