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
import { UploadRequestError } from "rc-upload/lib/interface";
import { FileVm } from "../Models/FileVm";
import { useEffect, useState } from "react";
import { nameof } from "../../../Shared/Helpers/StringHelpers";
import { TagSelectComponent } from "../../Tag/Components/TagSelectComponent";
import { TagType } from "../../Tag/Models/TagType";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { CancelButtonComponent } from "../../../Shared/Components/Buttons/CancelButtonComponent";
import { EditFileMetadataVm } from "../Models/EditFileMetadataVm";
import { getFileType } from "../Services/FileHelpers";

interface AddCaveFilesForm {
  files: FileVm[];
}

interface UploadComponentProps {
  onClose?: () => void;
  uploadFunction: (params: UploadParams) => Promise<FileVm>;
  updateFunction: (files: FileVm[]) => Promise<void>;
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
  [key: string]: any; // This allows you to pass in any other parameters your upload function might need
}

const UploadComponent = ({
  onClose,
  uploadFunction,
  updateFunction,
  hideCancelButton,
  style,
  draggerMessage,
  draggerTitle,
  singleFile,
  allowedFileTypes,
}: UploadComponentProps) => {
  const [form] = Form.useForm<AddCaveFilesForm>();
  const [formChanged, setFormChanged] = useState(false); // Track form changes

  const [singleFileUploadPercent, setSingleFileUploadPercent] =
    useState<number>(0);

  if (!draggerMessage) {
    draggerMessage = "Drag and drop files or click to select files";
  }

  const [numberOfFilesToUpload, setNumberOfFilesToUpload] = useState(0);
  const [completedNumberOfUploads, setCompletedNumberOfUploads] = useState(0);

  useEffect(() => {
    // Check if all uploads are completed
    if (
      completedNumberOfUploads === numberOfFilesToUpload &&
      completedNumberOfUploads !== 0
    ) {
      // Display a success message
      message.success("All files have been uploaded successfully!");
    }
  }, [completedNumberOfUploads, numberOfFilesToUpload]);

  const uploadsInProgress = () => {
    return (
      numberOfFilesToUpload === 0 ||
      completedNumberOfUploads !== numberOfFilesToUpload
    );
  };

  const onSubmit = async (values: AddCaveFilesForm) => {
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
      return false; // Prevent file from being uploaded
    }
    const fileSizeInMB = file.size / 1024 / 1024;
    const maxSizeInMB = 50;

    if (fileSizeInMB > maxSizeInMB) {
      message.error(`File size should not exceed ${maxSizeInMB} MB.`);
      return false; // Prevent file from being uploaded
    }

    return true; // Allow file upload
  };

  useEffect(() => {
    form.setFieldsValue({ files: [] });
  }, []);
  const props: UploadProps = {
    name: "file",
    multiple: !singleFile,
    style: style,
    accept: allowedFileTypes?.join(","),
    beforeUpload,
    showUploadList: singleFile ? false : true,
    itemRender: (originNode, file, currFileList) => {
      return file.status !== "done" ? originNode : null;
    },
    async customRequest(options) {
      const { onSuccess, onError, file, onProgress } = options;
      setNumberOfFilesToUpload((prevCount) => prevCount + 1);

      try {
        const result = await uploadFunction({
          file,
          uid: (file as RcFile).uid,
          onProgress: (event: AxiosProgressEvent) => {
            const percent = Math.round(
              (100 * event.loaded) / (event.total ?? 0)
            );

            setSingleFileUploadPercent(percent);
            console.log(percent);

            if (onProgress) {
              onProgress({ percent });
            }
          },
        });
        form.setFieldsValue({
          files: [...form.getFieldValue("files"), result],
        });

        if (onSuccess) {
          onSuccess({});
        }
      } catch (e) {
        const errorMessage = e as ApiErrorResponse;
        message.error(errorMessage.message);
        if (onError) {
          onError({} as UploadRequestError);
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
              {(fields, { add, remove }, { errors }) => (
                <>
                  <Row gutter={16}>
                    {fields.map((field, index) => (
                      <Col>
                        <Card bordered style={{ height: "100%" }}>
                          <Form.Item
                            name={[
                              field.name,
                              nameof<EditFileMetadataVm>("displayName"),
                            ]}
                            key={field.key}
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
                            key={field.key}
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
