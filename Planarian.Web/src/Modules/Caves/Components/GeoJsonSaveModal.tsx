// GeoJsonSaveModal.tsx
import React, { useState } from "react";
import { Modal, Button, message, Alert, Space } from "antd";
import { CaveService } from "../Service/CaveService";
import { CopyOutlined } from "@ant-design/icons";

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

  const handleSave = async () => {
    setIsSaving(true);
    try {
      // The backend endpoint expects an array, so we wrap the object
      await CaveService.uploadCaveGeoJson(caveId, [{ geoJson }]);
      message.success("Shapefile saved successfully!");
      onSaved();
    } catch (error) {
      console.error("Error saving shapefile", error);
      message.error("Failed to save shapefile. Please try again.");
    } finally {
      setIsSaving(false);
    }
  };

  const copyToClipboard = () => {
    navigator.clipboard
      .writeText(geoJson)
      .then(() => message.success("Data copied to clipboard"))
      .catch(() => message.error("Failed to copy data"));
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
