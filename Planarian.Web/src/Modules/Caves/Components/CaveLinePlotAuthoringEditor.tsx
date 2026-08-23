import { DeleteOutlined, DownloadOutlined, EyeOutlined, UploadOutlined } from "@ant-design/icons";
import { Col, Form, Input, message, Row, Upload } from "antd";
import { FormInstance } from "antd/lib/form";
import { RcFile } from "antd/lib/upload";
import { useState } from "react";
import { AddCaveVm } from "../Models/AddCaveVm";
import { GeoJsonUploadVm } from "../Models/GeoJsonUploadVm";
import { parseCaveLinePlotFile } from "../../Map/Helpers/LinePlotFileParser";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { downloadFile } from "../../../Shared/Helpers/FileHelpers";
import { CaveLinePlotViewer } from "./CaveLinePlotViewer";
import { CaveAuthoringAssetCard } from "./CaveAuthoringAssetCard";

const baseName = (name: string) => name.replace(/\.(zip|geojson|json)$/i, "");

export const CaveLinePlotAuthoringEditor = ({ form, onAuthoringChange }: {
  form: FormInstance<AddCaveVm>;
  onAuthoringChange?: () => void;
}) => {
  const linePlots = Form.useWatch<GeoJsonUploadVm[]>("linePlots", form) ?? [];
  const [activeLinePlotIndex, setActiveLinePlotIndex] = useState<number>();
  const activeLinePlot = activeLinePlotIndex === undefined ? undefined : linePlots[activeLinePlotIndex];

  return <>
    <Form.List name="linePlots">
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
              onAuthoringChange?.();
              onSuccess?.({});
            } catch (error) {
              message.error(error instanceof Error ? error.message : "The line plot could not be read.");
              onError?.(error as Error);
            }
          }}
        >
          <PlanarianButton icon={<UploadOutlined />} alwaysShowChildren>Add line plot</PlanarianButton>
        </Upload>
        <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
          {fields.map((field) => {
            const linePlot = linePlots[field.name];
            return <Col xs={24} lg={12} key={field.key}>
              <CaveAuthoringAssetCard
                title={linePlot?.name ?? "Line plot"}
                ariaLabel={`${linePlot?.name ?? "Line plot"} line plot editor`}
                metadata="GeoJSON line plot"
                actions={<>
                  {linePlot && <PlanarianButton size="small" icon={<EyeOutlined />} alwaysShowChildren
                    onClick={() => setActiveLinePlotIndex(field.name)}>View</PlanarianButton>}
                  {linePlot && <PlanarianButton size="small" icon={<DownloadOutlined />} alwaysShowChildren
                    onClick={() => downloadFile(`${linePlot.name}.geojson`, linePlot.geoJson)}>Download</PlanarianButton>}
                  <Upload
                    accept=".zip,.geojson,.json"
                    maxCount={1}
                    showUploadList={false}
                    customRequest={async ({ file, onSuccess, onError }) => {
                      try {
                        const collections = await parseCaveLinePlotFile(file as RcFile);
                        if (collections.length !== 1)
                          throw new Error("Replacing a line plot requires exactly one FeatureCollection.");
                        form.setFieldValue(["linePlots", field.name, "geoJson"], JSON.stringify(collections[0]));
                        onAuthoringChange?.();
                        onSuccess?.({});
                      } catch (error) {
                        message.error(error instanceof Error ? error.message : "The line plot could not be read.");
                        onError?.(error as Error);
                      }
                    }}
                  >
                    <PlanarianButton size="small" icon={<UploadOutlined />} alwaysShowChildren>Replace content</PlanarianButton>
                  </Upload>
                  <PlanarianButton size="small" danger icon={<DeleteOutlined />} alwaysShowChildren
                    aria-label={`Remove ${linePlot?.name ?? "line plot"}`}
                    onClick={() => {
                      remove(field.name);
                      if (activeLinePlotIndex === field.name) setActiveLinePlotIndex(undefined);
                      onAuthoringChange?.();
                    }}>Remove</PlanarianButton>
                </>}
              >
                <Form.Item name={[field.name, "id"]} hidden><Input /></Form.Item>
                <Form.Item name={[field.name, "geoJson"]} hidden
                  rules={[{ required: true, message: "Line plot GeoJSON is required" }]}><Input /></Form.Item>
                <Form.Item label="Name" name={[field.name, "name"]}
                  rules={[{ required: true, whitespace: true, message: "Please enter a name" }]}>
                  <Input />
                </Form.Item>
              </CaveAuthoringAssetCard>
            </Col>;
          })}
        </Row>
      </>}
    </Form.List>
    <CaveLinePlotViewer linePlot={activeLinePlot} open={!!activeLinePlot}
      onClose={() => setActiveLinePlotIndex(undefined)} />
  </>;
};
