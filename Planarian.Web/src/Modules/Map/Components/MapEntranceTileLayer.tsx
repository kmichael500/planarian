import React, { useContext, useMemo } from "react";
import { Layer, Source } from "react-map-gl/maplibre";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AppOptions } from "../../../Shared/Services/AppService";
import { entranceVisibleMinZoom } from "../Helpers/MapHitTesting";
import {
  ENTRANCE_LABEL_TEXT_FIELD,
  MAP_ENTRANCE_LAYER_SETTING_ID,
} from "./MapEntranceHelpers";
import { useMapLayers } from "./MapLayerContext";

export const MAP_ENTRANCE_LAYER_ID = "entrances";

export const MapEntranceTileLayer: React.FC<{
  filterQuery?: string;
}> = ({ filterQuery = "" }) => {
  const { currentAccountId, currentAccountName } = useContext(AppContext);
  const { isVisible, opacity } = useMapLayers();
  const visible = isVisible(MAP_ENTRANCE_LAYER_SETTING_ID);
  const layerOpacity = opacity(MAP_ENTRANCE_LAYER_SETTING_ID);
  const sourceKey = `${currentAccountId ?? ""}|${filterQuery}`;
  const tiles = useMemo(() => {
    if (!AppOptions.apiBaseUrl) return [];
    const params = new URLSearchParams();
    if (currentAccountId) params.set("account_id", currentAccountId);
    new URLSearchParams(filterQuery).forEach((value, key) => params.set(key, value));
    return [`${AppOptions.apiBaseUrl}/api/map/{z}/{x}/{y}.mvt?${params}`];
  }, [currentAccountId, filterQuery]);

  return (
    <Source
      key={sourceKey}
      id="entrances"
      type="vector"
      tiles={tiles}
      attribution={currentAccountName ? `© ${currentAccountName}` : undefined}
    >
      <Layer
        source-layer="entrances"
        id={MAP_ENTRANCE_LAYER_ID}
        type="circle"
        minzoom={entranceVisibleMinZoom}
        layout={{ visibility: visible ? "visible" : "none" }}
        paint={{
          "circle-radius": ["interpolate", ["linear"], ["zoom"], 5, 2, 10, 4, 15, 8],
          "circle-opacity": ["step", ["zoom"], 0, entranceVisibleMinZoom, 0.6 * layerOpacity],
          "circle-color": ["case", ["boolean", ["get", "IsFavorite"], false], "#FFC107", "#00008B"],
          "circle-stroke-color": ["step", ["zoom"], "rgba(0, 0, 0, 0)", 9, "#000"],
          "circle-stroke-opacity": layerOpacity,
          "circle-stroke-width": ["step", ["zoom"], 0, 9, 1],
        }}
      />
      <Layer
        source-layer="entrances"
        id="entrances-heatmap"
        type="heatmap"
        layout={{ visibility: visible ? "visible" : "none" }}
        paint={{
          "heatmap-radius": ["interpolate", ["linear"], ["zoom"], 0, 0.000001, 22, 30],
          "heatmap-opacity": ["step", ["zoom"], 0.6 * layerOpacity, 9, 0],
        }}
      />
      <Layer
        source-layer="entrances"
        id="entrance-labels"
        type="symbol"
        layout={{
          visibility: visible ? "visible" : "none",
          "text-font": ["Open Sans Regular"],
          "text-field": ENTRANCE_LABEL_TEXT_FIELD,
          "text-size": ["interpolate", ["linear"], ["zoom"], 5, 0, 10, 8, 15, 12],
          "text-offset": [0, 1.5],
          "text-anchor": "top",
          "text-allow-overlap": false,
        }}
        paint={{
          "text-color": "#0f1720",
          "text-opacity": ["step", ["zoom"], 0, 9, 0.8 * layerOpacity, 15, layerOpacity],
        }}
      />
    </Source>
  );
};
