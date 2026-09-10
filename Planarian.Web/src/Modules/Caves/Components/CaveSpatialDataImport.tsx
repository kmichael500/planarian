import React, { useRef, useState } from "react";
import { CloudUploadOutlined } from "@ant-design/icons";
import { message } from "antd";
import type { FeatureCollection } from "geojson";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { GeoJsonSaveModal } from "./GeoJsonSaveModal";

interface CaveSpatialDataImportProps {
  caveId: string;
  disabled?: boolean;
  onSaved?: () => void;
}

export const CaveSpatialDataImport: React.FC<CaveSpatialDataImportProps> = ({ caveId, disabled, onSaved }) => {
  const inputRef = useRef<HTMLInputElement>(null);
  const [geoJson, setGeoJson] = useState<string | null>(null);

  const selectFile = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;

    try {
      const { default: shp } = await import("shpjs");
      const parsed = await shp(await file.arrayBuffer());
      const collections = (Array.isArray(parsed) ? parsed : [parsed]) as FeatureCollection[];
      if (!collections.some((collection) => collection.features?.length)) {
        throw new Error("No spatial features found.");
      }
      setGeoJson(JSON.stringify(collections, null, 2));
    } catch (error) {
      console.error("Unable to import shapefile", error);
      message.error("Unable to import shapefile. Select a valid zipped shapefile containing .shp and .dbf files.");
    }
  };

  return (
    <>
      <input ref={inputRef} type="file" accept=".zip,application/zip" hidden onChange={selectFile} />
      <PlanarianButton
        alwaysShowChildren
        permissionKey={PermissionKey.Manager}
        disabled={disabled}
        icon={<CloudUploadOutlined />}
        onClick={() => inputRef.current?.click()}
      >
        Import Line Plot
      </PlanarianButton>
      {geoJson && (
        <GeoJsonSaveModal
          isVisible
          caveId={caveId}
          geoJson={geoJson}
          onCancel={() => setGeoJson(null)}
          onSaved={() => {
            setGeoJson(null);
            onSaved?.();
          }}
        />
      )}
    </>
  );
};
