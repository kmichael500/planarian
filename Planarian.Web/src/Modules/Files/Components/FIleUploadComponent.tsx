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

export interface AddCaveFilesForm {
  files: FileVm[];
}

const UploadComponent = ({ caveId }: UploadComponentProps) => {
  const [form] = Form.useForm<AddCaveFilesForm>();

  const [numberOfFilesToUpload, setNumberOfFilesToUpload] = useState(0);
  const [completedNumberOfUploads, setCompletedNumberOfUploads] = useState(0);

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
        const test = form.getFieldValue("files");
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
          <Form form={form} layout="vertical" onFinish={onSubmit}>
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
                            <TagSelectComponent tagType={TagType.File} />
                          </Form.Item>
                        </Card>
                      </Col>
                    ))}
                  </Row>
                </>
              )}
            </Form.List>
          </Form>{" "}
        </>
      )}

      <br />
      <Space direction="horizontal">
        <CancelButtonComponent />
        <PlanarianButton
          icon={undefined}
          onClick={() => {
            form.submit();
          }}
        >
          Save Changes
        </PlanarianButton>
      </Space>
    </>
  );
};

interface UploadComponentProps {
  caveId?: string | undefined;
}

export { UploadComponent };
