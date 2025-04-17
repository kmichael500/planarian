// GeoJsonSaveModal.tsx
import React, { useState } from "react";
import { Modal, Button, message, Alert, Space, Form, Input } from "antd";
import { CaveService } from "../Service/CaveService";
import { CopyOutlined } from "@ant-design/icons";
import { GeoJsonUploadVm } from "../Models/GeoJsonUploadVm";

export interface GeoJsonSaveModalProps {
  isVisible: boolean;
  caveId: string;
  geoJson: string; // The raw GeoJSON string (or JSON if you prefer)
  onCancel: () => void;
  onSaved: () => void; // Callback when the save has succeeded (e.g., to trigger a refresh)
}

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
        await CaveService.uploadCaveGeoJson(caveId, [
          {
            geoJson,
            name: values.name,
          },
        ]);
        message.success("Shapefile saved successfully!");
        onSaved();
      } catch (error) {
        console.error("Error saving shapefile", error);
        message.error("Failed to save shapefile. Please try again.");
      } finally {
        setIsSaving(false);
      }
    } catch (validationError) {}
  };

  const copyToClipboard = async () => {
    try {
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
          description="Saving this shapefile will add it to the map and will overwrite any existing shapefiles that are associated with this cave."
          showIcon
        />

        <Form form={form} layout="vertical">
          <Form.Item
            name="name"
            label="Shapefile Name"
            rules={[
              {
                required: true,
                message: "Please enter a name for this shapefile",
              },
            ]}
          >
            <Input placeholder="Enter shapefile name" />
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
