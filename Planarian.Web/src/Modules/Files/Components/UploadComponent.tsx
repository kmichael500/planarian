import Dragger from "antd/lib/upload/Dragger";
import { InboxOutlined } from "@ant-design/icons";
import {
  Form,
  UploadProps,
  message,
  Input,
  Card,
  Row,
  Col,
  Space,
  Progress,
} from "antd";
import { AxiosProgressEvent } from "axios";
import { RcFile } from "antd/lib/upload/interface";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { useEffect, useState } from "react";
import { nameof } from "../../../Shared/Helpers/StringHelpers";
import { TagSelectComponent } from "../../Tag/Components/TagSelectComponent";
import { TagType } from "../../Tag/Models/TagType";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { CancelButtonComponent } from "../../../Shared/Components/Buttons/CancelButtonComponent";
import { EditFileMetadataVm } from "../Models/EditFileMetadataVm";
import { getFileType } from "../Services/FileHelpers";

interface UploadFilesForm<TUpload> {
  files: TUpload[];
}

interface UploadComponentProps<TUpload> {
  onClose?: () => void;
  uploadFunction: (params: UploadParams) => Promise<TUpload>;
  updateFunction: (files: TUpload[]) => Promise<void>;
  allowedFileTypes?: string[];
  hideCancelButton?: boolean;
  style?: React.CSSProperties;
  draggerMessage?: string;
  draggerTitle?: string;
  singleFile?: boolean;
}

export interface UploadParams {
  file: any;
  uid: string;
  onProgress: (event: AxiosProgressEvent) => void;
  [key: string]: any;
}

const UploadComponent = <TUpload,>({
  onClose,
  uploadFunction,
  updateFunction,
  hideCancelButton,
  style,
  draggerMessage,
  draggerTitle,
  singleFile,
  allowedFileTypes,
}: UploadComponentProps<TUpload>) => {
  const [form] = Form.useForm<UploadFilesForm<TUpload>>();
  const [uploadedFiles, setUploadedFiles] = useState<TUpload[]>([]);
  const [formChanged, setFormChanged] = useState(false);

  const [singleFileUploadPercent, setSingleFileUploadPercent] =
    useState<number>(0);

  if (!draggerMessage) {
    draggerMessage = "Drag and drop files or click to select files";
  }

  const [numberOfFilesToUpload, setNumberOfFilesToUpload] = useState(0);
  const [completedNumberOfUploads, setCompletedNumberOfUploads] = useState(0);
  const [successfulNumberOfUploads, setSuccessfulNumberOfUploads] = useState(0);
  const [uploadProgress, setUploadProgress] = useState<
    Record<string, { progress: number; name: string }>
  >({});

  useEffect(() => {
    if (
      completedNumberOfUploads === numberOfFilesToUpload &&
      successfulNumberOfUploads === numberOfFilesToUpload &&
      numberOfFilesToUpload !== 0
    ) {
      message.success("All files have been uploaded successfully!");
    }
  }, [completedNumberOfUploads, numberOfFilesToUpload, successfulNumberOfUploads]);

  const uploadsInProgress = () => {
    return (
      numberOfFilesToUpload === 0 ||
      completedNumberOfUploads !== numberOfFilesToUpload ||
      successfulNumberOfUploads === 0
    );
  };

  const onSubmit = async (values: UploadFilesForm<TUpload>) => {
    try {
      await updateFunction(values.files);
      message.success("Files successfully updated!");
      if (onClose) {
        onClose();
      }
    } catch (e) {
      const error = e as ApiErrorResponse;
      message.error(error.message);
    }
  };

  const beforeUpload = (file: File) => {
    var fileType = getFileType(file.name);
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
      console.error(file);
      return false;
    }
    const fileSizeInMB = file.size / 1024 / 1024;
    const maxSizeInMB = 50;

    if (fileSizeInMB > maxSizeInMB) {
      message.error(`File size should not exceed ${maxSizeInMB} MB.`);
      return false;
    }

    return true;
  };

  const showUploadForm =
    numberOfFilesToUpload > 0 &&
    completedNumberOfUploads === numberOfFilesToUpload &&
    successfulNumberOfUploads > 0;

  // Keep upload results locally until the edit form is mounted. Writing to an
  // unmounted Ant Design Form instance is unreliable and produces a warning.
  useEffect(() => {
    if (showUploadForm) {
      form.setFieldValue("files", uploadedFiles);
    }
  }, [form, showUploadForm, uploadedFiles]);

  const props: UploadProps = {
    name: "file",
    multiple: !singleFile,
    style: style,
    accept: allowedFileTypes?.join(","),
    beforeUpload,
    showUploadList: singleFile ? false : true,
    itemRender: (originNode, file) => {
      return file.status !== "done" ? originNode : null;
    },
    async customRequest(options) {
      const { onSuccess, onError, file, onProgress } = options;
      setNumberOfFilesToUpload((prevCount) => prevCount + 1);

      try {
        setUploadProgress((prevProgress) => ({
          ...prevProgress,
          [(file as RcFile).uid]: {
            progress: 0,
            name: (file as RcFile).name,
          },
        }));

        const result = await uploadFunction({
          file,
          uid: (file as RcFile).uid,
          onProgress: (event: AxiosProgressEvent) => {
            // Browsers may omit the upload total. Never pass NaN or Infinity
            // to Ant Design's Progress component or its callback.
            const percent = event.total && event.total > 0
              ? Math.min(100, Math.max(0, Math.round((100 * event.loaded) / event.total)))
              : 0;

            setSingleFileUploadPercent(percent);

            setUploadProgress((prevProgress) => ({
              ...prevProgress,
              [(file as RcFile).uid]: {
                progress: percent,
                name: (file as RcFile).name,
              },
            }));

            if (onProgress) {
              onProgress({ percent });
            }
          },
        });
        setUploadedFiles((previous) => [...previous, result]);

        setSuccessfulNumberOfUploads((previous) => previous + 1);
        if (onSuccess) {
          onSuccess({});
        }
      } catch (e) {
        const error = e instanceof Error
          ? e
          : new Error((e as ApiErrorResponse)?.message || "Upload failed.");
        message.error(error.message);
        if (onError) {
          onError(error);
        }
      } finally {
        setCompletedNumberOfUploads((prevCount) => prevCount + 1);
      }
    },
  };
  return (
    <>
      {uploadsInProgress() && (
        <>
          <Dragger {...props}>
            <p className="ant-upload-drag-icon">
              <InboxOutlined />
            </p>
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
            {singleFile && singleFileUploadPercent > 0 && (
              <Progress percent={singleFileUploadPercent} />
            )}
            {!singleFile && (
              <div style={{ maxHeight: "200px", overflowY: "scroll" }}>
                {Object.entries(uploadProgress).map(([key, value]) => (
                  <div key={key} style={{ marginBottom: "10px" }}>
                    <Progress
                      percent={value.progress}
                    />
                    <div>
                      {value.name} - {value.progress?.toFixed(2)}%
                    </div>
                  </div>
                ))}
              </div>
            )}
          </Dragger>
          <br />
        </>
      )}
      {!uploadsInProgress() && (
        <>
          <Form
            form={form}
            layout="vertical"
            onFinish={onSubmit}
            onChange={() => {
              setFormChanged(true);
            }}
          >
            <Form.List name="files">
              {(fields) => (
                <>
                  <Row gutter={16}>
                    {fields.map((field) => (
                      <Col key={field.key}>
                        <Card variant="outlined" style={{ height: "100%" }}>
                          <Form.Item
                            name={[
                              field.name,
                              nameof<EditFileMetadataVm>("name"),
                            ]}
                            label={`Name`}
                            required
                          >
                            <Input />
                          </Form.Item>
                          <Form.Item
                            name={[
                              field.name,
                              nameof<EditFileMetadataVm>("fileTypeTagId"),
                            ]}
                            label={`File Type`}
                            required
                          >
                            <TagSelectComponent
                              onChange={() => {
                                setFormChanged(true);
                              }}
                              tagType={TagType.File}
                            />
                          </Form.Item>
                        </Card>
                      </Col>
                    ))}
                  </Row>
                </>
              )}
            </Form.List>
          </Form>
        </>
      )}

      <br />
      <Space direction="horizontal">
        {uploadsInProgress() && !hideCancelButton && (
          <CancelButtonComponent
            onClick={() => {
              if (onClose) {
                onClose();
              }
            }}
          />
        )}
        {!uploadsInProgress() && (
          <>
            <PlanarianButton
              alwaysShowChildren
              icon={undefined}
              onClick={() => {
                if (onClose) {
                  onClose();
                }
              }}
            >
              Back
            </PlanarianButton>
            <PlanarianButton
              alwaysShowChildren
              icon={undefined}
              onClick={() => {
                form.submit();
              }}
              disabled={uploadsInProgress() || !formChanged}
            >
              Save Changes
            </PlanarianButton>
          </>
        )}
      </Space>
    </>
  );
};

export { UploadComponent };
