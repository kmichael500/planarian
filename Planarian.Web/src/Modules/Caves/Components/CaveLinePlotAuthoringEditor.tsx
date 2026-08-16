import { DeleteOutlined, UploadOutlined } from "@ant-design/icons";
import { Button, Card, Col, Form, Input, message, Row, Space, Upload } from "antd";
import { FormInstance } from "antd/lib/form";
import { RcFile } from "antd/lib/upload";
import { AddCaveVm } from "../Models/AddCaveVm";
import { GeoJsonUploadVm } from "../Models/GeoJsonUploadVm";
import { parseCaveLinePlotFile } from "../../Map/Helpers/LinePlotFileParser";

const baseName = (name: string) => name.replace(/\.(zip|geojson|json)$/i, "");

export const CaveLinePlotAuthoringEditor = ({ form }: { form: FormInstance<AddCaveVm> }) => {
  const linePlots = Form.useWatch<GeoJsonUploadVm[]>("linePlots", form) ?? [];

  return <Form.List name="linePlots">
    {(fields, { add, remove }) => <>
      <Upload
        accept=".zip,.geojson,.json"
        multiple
        showUploadList={false}
        customRequest={async ({ file, onSuccess, onError }) => {
          const upload = file as RcFile;
          try {
            const collections = await parseCaveLinePlotFile(upload);
            collections.forEach((collection, index) => add({
              name: collections.length === 1 ? baseName(upload.name) : `${baseName(upload.name)} ${index + 1}`,
              geoJson: JSON.stringify(collection),
            }));
            onSuccess?.({});
          } catch (error) {
            message.error(error instanceof Error ? error.message : "The line plot could not be read.");
            onError?.(error as Error);
          }
        }}
      >
        <Button icon={<UploadOutlined />}>Add line plot</Button>
      </Upload>
      <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
        {fields.map((field) => {
          const linePlot = linePlots[field.name];
          return <Col xs={24} lg={12} key={field.key}>
            <Card size="small" title={linePlot?.name ?? "Line plot"}
              extra={<Button type="text" danger icon={<DeleteOutlined />} onClick={() => remove(field.name)} />}>
              <Form.Item name={[field.name, "id"]} hidden><Input /></Form.Item>
              <Form.Item name={[field.name, "geoJson"]} hidden
                rules={[{ required: true, message: "Line plot GeoJSON is required" }]}><Input /></Form.Item>
              <Form.Item label="Name" name={[field.name, "name"]}
                rules={[{ required: true, whitespace: true, message: "Please enter a name" }]}>
                <Input />
              </Form.Item>
              <Space>
                <Upload
                  accept=".zip,.geojson,.json"
                  maxCount={1}
                  showUploadList={false}
                  customRequest={async ({ file, onSuccess, onError }) => {
                    try {
                      const collections = await parseCaveLinePlotFile(file as RcFile);
                      if (collections.length !== 1) throw new Error("Replacing a line plot requires exactly one FeatureCollection.");
                      form.setFieldValue(["linePlots", field.name, "geoJson"], JSON.stringify(collections[0]));
                      onSuccess?.({});
                    } catch (error) {
                      message.error(error instanceof Error ? error.message : "The line plot could not be read.");
                      onError?.(error as Error);
                    }
                  }}
                >
                  <Button size="small" icon={<UploadOutlined />}>Replace content</Button>
                </Upload>
              </Space>
            </Card>
          </Col>;
        })}
      </Row>
    </>}
  </Form.List>;
};
