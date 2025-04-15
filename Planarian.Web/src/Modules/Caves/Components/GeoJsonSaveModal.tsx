// GeoJsonSaveModal.tsx
import React, { useState } from "react";
import { Modal, Button, message } from "antd";
import { CaveService } from "../Service/CaveService";

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
      message.success("GeoJSON saved successfully!");
      onSaved();
    } catch (error) {
      console.error("Error saving GeoJSON", error);
      message.error("Failed to save GeoJSON. Please try again.");
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <Modal
      visible={isVisible}
      title="Save GeoJSON"
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
          Save GeoJSON
        </Button>,
      ]}
    >
      <p>Would you like to save the uploaded GeoJSON?</p>
      <pre
        style={{
          maxHeight: "200px",
          overflow: "auto",
          background: "#f5f5f5",
          padding: "10px",
        }}
      >
        {geoJson}
      </pre>
    </Modal>
  );
};

export { GeoJsonSaveModal };
