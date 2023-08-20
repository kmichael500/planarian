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
  Button,
  Space,
} from "antd";
import { CaveService } from "../../Caves/Service/CaveService";
import { AxiosProgressEvent } from "axios";
import { RcFile } from "antd/lib/upload/interface";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { UploadRequestError } from "rc-upload/lib/interface";
import { FileVm } from "../Models/FileVm";
import { AddCaveVm } from "../../Caves/Models/AddCaveVm";
import { useEffect, useState } from "react";
import { nameof } from "../../../Shared/Helpers/StringHelpers";
import { TagSelectComponent } from "../../Tag/Components/TagSelectComponent";
import { TagType } from "../../Tag/Models/TagType";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { FileService } from "../Services/FileService";
import { CancelButtonComponent } from "../../../Shared/Components/Buttons/CancelButtonComponent";

interface AddCaveFilesForm {
  files: FileVm[];
}

const UploadComponent = ({ caveId, onClose }: UploadComponentProps) => {
  const [form] = Form.useForm<AddCaveFilesForm>();
  const [formChanged, setFormChanged] = useState(false); // Track form changes

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
      await FileService.UpdateFilesMetadata(values.files);
      message.success("Files successfully updated!");
      if (onClose) {
        onClose();
      }
    } catch (e) {
      const error = e as ApiErrorResponse;
      message.error(error.message);
    }
  };

  useEffect(() => {
    form.setFieldsValue({ files: [] });
  }, []);
  const props: UploadProps = {
    name: "file",
    multiple: true,

    itemRender: (originNode, file, currFileList) => {
      return file.status !== "done" ? originNode : null;
    },
    async customRequest(options) {
      const { onSuccess, onError, file, onProgress } = options;
      if (!caveId) throw new Error("CaveId is undefined");
      setNumberOfFilesToUpload((prevCount) => prevCount + 1);

      try {
        const result = await CaveService.AddCaveFile(
          file,
          caveId,
          (file as RcFile).uid,
          (event: AxiosProgressEvent) => {
            const percent = Math.round(
              (100 * event.loaded) / (event.total ?? 0)
            );

            if (onProgress) {
              onProgress({ percent });
            }
          }
        );
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
            <p className="ant-upload-text">
              Drag and drop files or click to select files
            </p>
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
                            name={[field.name, nameof<FileVm>("displayName")]}
                            key={field.key}
                            label={`Name`}
                            required
                          >
                            <Input />
                          </Form.Item>
                          <Form.Item
                            name={[field.name, nameof<FileVm>("fileTypeTagId")]}
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
        {uploadsInProgress() && (
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

interface UploadComponentProps {
  caveId?: string | undefined;
  onClose?: () => void;
}

export { UploadComponent };
