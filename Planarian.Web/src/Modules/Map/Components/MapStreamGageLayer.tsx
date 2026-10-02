import React, { useMemo } from "react";
import type { FeatureCollection, Point } from "geojson";
import { Layer, Source } from "react-map-gl/maplibre";
import type { StreamGageLocation } from "../Services/MapService";
import { useMapLayers } from "./MapLayerContext";

export const STREAM_GAGE_LAYER_SETTING_ID = "usgs-stream-gages";
export const STREAM_GAGE_POINT_LAYER_ID = "nearby-stream-gages";
export const STREAM_GAGE_LABEL_LAYER_ID = "nearby-stream-gage-labels";

type StreamGageProperties = {
  id: string;
  siteCode: string;
  siteName: string;
};

export const MapStreamGageLayer: React.FC<{
  gages: StreamGageLocation[];
}> = ({ gages }) => {
  const { isVisible, opacity } = useMapLayers();
  const visible = isVisible(STREAM_GAGE_LAYER_SETTING_ID);
  const layerOpacity = opacity(STREAM_GAGE_LAYER_SETTING_ID);
  const data = useMemo<FeatureCollection<Point, StreamGageProperties>>(
    () => ({
      type: "FeatureCollection",
      features: gages.map((gage) => ({
        type: "Feature",
        geometry: { type: "Point", coordinates: [gage.longitude, gage.latitude] },
        properties: {
          id: gage.id,
          siteCode: gage.siteCode,
          siteName: gage.siteName,
        },
      })),
    }),
    [gages]
  );

  return (
    <Source id="nearby-stream-gages-source" type="geojson" data={data} attribution="USGS Water Data">
      <Layer
        id={STREAM_GAGE_POINT_LAYER_ID}
        type="circle"
        minzoom={8}
        layout={{ visibility: visible ? "visible" : "none" }}
        paint={{
          "circle-color": "#722ed1",
          "circle-opacity": layerOpacity,
          "circle-radius": 6,
          "circle-stroke-color": "#ffffff",
          "circle-stroke-opacity": layerOpacity,
          "circle-stroke-width": 1.5,
        }}
      />
      <Layer
        id={STREAM_GAGE_LABEL_LAYER_ID}
        type="symbol"
        minzoom={8}
        layout={{
          visibility: visible ? "visible" : "none",
          "text-field": ["get", "siteName"],
          "text-size": 10,
          "text-anchor": "top",
          "text-offset": [0, 1],
          "text-allow-overlap": false,
          "text-optional": true,
        }}
        paint={{
          "text-color": "#3b0764",
          "text-opacity": layerOpacity,
          "text-halo-color": "rgba(255, 255, 255, 0.95)",
          "text-halo-width": 1.5,
        }}
      />
    </Source>
  );
};
