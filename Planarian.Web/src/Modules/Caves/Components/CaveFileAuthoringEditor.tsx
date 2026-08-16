import { DeleteOutlined, InboxOutlined } from "@ant-design/icons";
import { Button, Card, Col, Form, Input, message, Row, Upload } from "antd";
import { FormInstance } from "antd/lib/form";
import { RcFile } from "antd/lib/upload";
import { AddCaveVm } from "../Models/AddCaveVm";
import { CaveService } from "../Service/CaveService";
import { EditFileMetadataVm } from "../../Files/Models/EditFileMetadataVm";
import { TagSelectComponent } from "../../Tag/Components/TagSelectComponent";
import { TagType } from "../../Tag/Models/TagType";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";

export const CaveFileAuthoringEditor = ({ form }: { form: FormInstance<AddCaveVm> }) => {
  const files = Form.useWatch<EditFileMetadataVm[]>("files", form) ?? [];

  return <Form.List name="files">
    {(fields, { add, remove }) => <>
      <Upload.Dragger
        multiple
        showUploadList={false}
        customRequest={async ({ file, onProgress, onSuccess, onError }) => {
          const upload = file as RcFile;
          try {
            const staged = await CaveService.StageAuthoringFile(upload, upload.uid, (event) => {
              const percent = event.total ? Math.round(event.loaded * 100 / event.total) : 0;
              onProgress?.({ percent });
            });
            add({
              id: staged.id,
              displayName: staged.displayName,
              fileTypeTagId: staged.fileTypeTagId,
              fileTypeKey: staged.fileTypeKey,
            });
            onSuccess?.({});
          } catch (error) {
            message.error((error as ApiErrorResponse).message ?? "The file could not be staged.");
            onError?.(error as Error);
          }
        }}
      >
        <p className="ant-upload-drag-icon"><InboxOutlined /></p>
        <p className="ant-upload-text">Add files to this Cave change</p>
        <p className="ant-upload-hint">Files are staged now and are not published until this form is saved or approved.</p>
      </Upload.Dragger>
      <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
        {fields.map((field) => {
          const file = files[field.name];
          return <Col xs={24} lg={12} key={field.key}>
            <Card size="small" title={file?.displayName ?? "File"}
              extra={<Button type="text" danger icon={<DeleteOutlined />} onClick={() => remove(field.name)} />}>
              <Form.Item name={[field.name, "id"]} hidden><Input /></Form.Item>
              <Form.Item name={[field.name, "fileTypeKey"]} hidden><Input /></Form.Item>
              <Form.Item label="Name" name={[field.name, "displayName"]}
                rules={[{ required: true, whitespace: true, message: "Please enter a name" }]}>
                <Input />
              </Form.Item>
              <Form.Item label="File Type" name={[field.name, "fileTypeTagId"]}
                rules={[{ required: true, message: "Please select a file type" }]}>
                <TagSelectComponent tagType={TagType.File} />
              </Form.Item>
            </Card>
          </Col>;
        })}
      </Row>
    </>}
  </Form.List>;
};
