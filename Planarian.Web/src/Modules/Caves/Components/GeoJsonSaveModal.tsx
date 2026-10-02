// GeoJsonSaveModal.tsx
import React, { useState } from "react";
import { Modal, Button, message, Alert, Space, Form, Input } from "antd";
import { CaveService } from "../Service/CaveService";
import { CopyOutlined } from "@ant-design/icons";
import { GeoJsonUploadVm } from "../Models/GeoJsonUploadVm";
import { caveToForm } from "../Helpers/CaveFormMapper";

export interface GeoJsonSaveModalProps {
  isVisible: boolean;
  caveId: string;
  geoJson: string; // The raw GeoJSON string (or JSON if you prefer)
  onCancel: () => void;
  onSaved: () => void; // Callback when the save has succeeded (e.g., to trigger a refresh)
}

export const saveImportedLinePlots = async (
  caveId: string,
  geoJson: string,
  name: string
) => {
  const parsed = JSON.parse(geoJson);
  const featureCollections = Array.isArray(parsed) ? parsed : [parsed];
  const linePlots: GeoJsonUploadVm[] = featureCollections.map((collection) => ({
    geoJson: JSON.stringify(collection),
    name,
  }));

  const context = await CaveService.GetEditAuthoringContext(caveId);
  await CaveService.UpdateCave(caveToForm(context.cave, linePlots));
};

const GeoJsonSaveModal: React.FC<GeoJsonSaveModalProps> = ({
  isVisible,
  caveId,
  geoJson,
  onCancel,
  onSaved,
}) => {
  const [isSaving, setIsSaving] = useState(false);
  const [form] = Form.useForm<GeoJsonUploadVm>();

  const handleSave = async () => {
    try {
      const values = await form.validateFields();

      setIsSaving(true);
      try {
        await saveImportedLinePlots(caveId, geoJson, values.name);
        message.success("Line plot saved successfully!");
        onSaved();
      } catch (error) {
        console.error("Error saving line plot", error);
        message.error("Failed to save line plot. Please try again.");
      } finally {
        setIsSaving(false);
      }
    } catch (validationError) { }
  };

  const copyToClipboard = async () => {
    try {

      const sizeInMb = new Blob([geoJson]).size / (1024 * 1024);
      message.info(`GeoJSON size: ${sizeInMb.toFixed(2)} MB`);

      await navigator.clipboard.writeText(geoJson);
      message.success("Data copied to clipboard");
    } catch (error) {
      message.error("Failed to copy data");
    }
  };

  return (
    <Modal
      visible={isVisible}
      title="Save"
      onCancel={onCancel}
      footer={[
        <Button key="cancel" onClick={onCancel} disabled={isSaving}>
          Cancel
        </Button>,
        <Button
          key="save"
          type="primary"
          loading={isSaving}
          onClick={handleSave}
        >
          Save
        </Button>,
      ]}
    >
      <Space direction="vertical" style={{ width: "100%" }}>
        <Alert
          type="info"
          message="What will happen?"
          description="Saving this line plot will add it to the map and replace any existing line plots associated with this cave."
          showIcon
        />

        <Form form={form} layout="vertical">
          <Form.Item
            name="name"
            label="Line Plot Name"
            rules={[
              {
                required: true,
                message: "Please enter a name for this line plot",
              },
            ]}
          >
            <Input placeholder="Enter line plot name" />
          </Form.Item>
        </Form>

        <div style={{ textAlign: "right" }}>
          <Button icon={<CopyOutlined />} onClick={copyToClipboard}>
            Copy GeoJSON
          </Button>
        </div>
      </Space>
    </Modal>
  );
};

export { GeoJsonSaveModal };
