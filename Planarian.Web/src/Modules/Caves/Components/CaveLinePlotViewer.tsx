import { Result } from "antd";
import type { FeatureCollection } from "geojson";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { PlanarianTag } from "../../../Shared/Components/Display/PlanarianTag";
import { VectorDatasetViewer } from "../../Files/Components/VectorDatasetViewer";
import { GeoJsonUploadVm } from "../Models/GeoJsonUploadVm";

export const CaveLinePlotViewer = ({ linePlot, open, onClose }: {
  linePlot?: GeoJsonUploadVm;
  open: boolean;
  onClose: () => void;
}) => {
  let collection: FeatureCollection | undefined;
  let parseError: string | undefined;

  if (linePlot) {
    try {
      const parsed = JSON.parse(linePlot.geoJson) as FeatureCollection;
      if (parsed?.type !== "FeatureCollection" || !Array.isArray(parsed.features))
        throw new Error("Line plot is not a GeoJSON FeatureCollection.");
      collection = parsed;
    } catch (error) {
      parseError = error instanceof Error ? error.message : "Line plot GeoJSON could not be read.";
    }
  }

  return <PlanarianModal
    open={open}
    fullScreen
    header={linePlot ? <>{linePlot.name} <PlanarianTag style={{ marginLeft: 8 }}>GeoJSON</PlanarianTag></> : "Line plot"}
    onClose={onClose}
  >
    {parseError
      ? <Result status="warning" title="Line plot could not be displayed" subTitle={parseError} />
      : collection
        ? <VectorDatasetViewer embedUrl="" fileType="geojson" data={collection} />
        : null}
  </PlanarianModal>;
};
