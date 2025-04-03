import { Checkbox, InputNumber, Slider, Space } from "antd";
import React, { useState } from "react";
import { Source, Layer, useMap } from "react-map-gl/maplibre";
import styled from "styled-components";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { BuildOutlined } from "@ant-design/icons";
import type { DataDrivenPropertyValueSpecification } from "maplibre-gl";
import { PublicAccessLegend } from "./PublicAccessLegend";
import { PUBLIC_ACCESS_INFO } from "./ProtectedAreaDetails";

// Extend the interface to include an optional legend property.
interface PlanarianMapLayer {
  displayName: string;
  isActive: boolean;
  opacity: number;
  id: string;
  type: string;
  attribution?: string;
  source: {
    layerName?: string; // For vector layers, specify the source-layer name.
    type: string;
    tiles: string[];
    tileSize?: number;
  };
  paint?: {
    "raster-opacity"?: number;
  };
  // Optional configuration for a fill layer in vector sources.
  fillLayer?: {
    id: string;
    sourceLayer: string;
    layout: { [key: string]: any };
    paint: { [key: string]: any };
  };
  // Optional configuration for a text-only sublayer.
  secondaryLayer?: {
    type:
      | "symbol"
      | "raster"
      | "circle"
      | "line"
      | "background"
      | "fill"
      | "fill-extrusion"
      | "heatmap"
      | "hillshade";
    id: string;
    minzoom?: number;
    maxzoom?: number;
    source: {
      id: string;
      type: string;
      layerName: string;
      tiles: string[];
      tileSize?: number;
    };
    layout: (isActive: boolean) => { [key: string]: any };
    paint: (opacity: number) => { [key: string]: any };
  };
  legend?: React.ReactNode;
}

const publicAccessColorExpression: DataDrivenPropertyValueSpecification<string> =
  [
    "match",
    ["get", "Pub_Access"],
    ...Object.entries(PUBLIC_ACCESS_INFO).flatMap(([code, info]) => [
      code,
      info.color,
    ]),
    "#cccccc", // fallback
  ] as unknown as DataDrivenPropertyValueSpecification<string>;

const LAYERS: PlanarianMapLayer[] = [
  {
    id: "open street map",
    displayName: "Street",
    type: "raster",
    source: {
      type: "raster",
      tiles: [
        "https://a.tile.openstreetmap.org/{z}/{x}/{y}.png",
        "https://b.tile.openstreetmap.org/{z}/{x}/{y}.png",
        "https://c.tile.openstreetmap.org/{z}/{x}/{y}.png",
      ],
      tileSize: 256,
    },
    isActive: true,
    opacity: 1,
    attribution: "© OpenStreetMap contributors",
  },
  {
    id: "open-topo",
    displayName: "Topo",
    type: "raster",
    source: {
      type: "raster",
      tiles: ["https://tile.opentopomap.org/{z}/{x}/{y}.png"],
      tileSize: 256,
    },
    isActive: false,
    opacity: 1,
    attribution: "© OpenStreetMap contributors",
  },
  {
    id: "esri-world-imagery",
    displayName: "Satellite (ESRI)",
    type: "raster",
    source: {
      type: "raster",
      tiles: [
        "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
      ],
      tileSize: 256,
    },
    isActive: false,
    opacity: 1,
    attribution: "© ESRI",
  },
  {
    id: "3-dep-hillshade-usgs",
    displayName: "Hillshade",
    type: "raster",
    source: {
      type: "raster",
      tiles: [
        "https://elevation.nationalmap.gov/arcgis/services/3DEPElevation/ImageServer/WMSServer?bbox={bbox-epsg-3857}&format=image/png&service=WMS&version=1.1.1&request=GetMap&srs=EPSG:3857&transparent=true&width=256&height=256&layers=3DEPElevation:Hillshade Gray",
      ],
    },
    isActive: false,
    opacity: 1,
    attribution: "USGS 3DEP Elevation Program",
  },
  {
    id: "macrostrat",
    displayName: "Geology",
    type: "raster",
    source: {
      type: "raster",
      tiles: ["https://tiles.macrostrat.org/carto/{z}/{x}/{y}.png"],
      tileSize: 256,
    },
    isActive: false,
    opacity: 1,
    attribution: "Macrostrat",
  },
  {
    id: "usgs-hydro",
    displayName: "Hydrology",
    type: "raster",
    source: {
      type: "raster",
      tiles: [
        "https://hydro.nationalmap.gov/arcgis/services/nhd/MapServer/WMSServer?bbox={bbox-epsg-3857}&format=image/png&service=WMS&version=1.1.1&request=GetMap&srs=EPSG:3857&transparent=true&width=256&height=256&layers=0,1,2,3,4,5,6,7,8,9,10,11,12&styles=",
      ],
      tileSize: 256,
    },
    paint: { "raster-opacity": 1 },
    isActive: false,
    opacity: 1,
    attribution: "USGS",
  },
  {
    id: "usgs-drainage-basins-16digit",
    displayName: "Watershed Boundary",
    type: "raster",
    source: {
      type: "raster",
      tiles: [
        "https://hydro.nationalmap.gov/arcgis/services/wbd/MapServer/WMSServer?bbox={bbox-epsg-3857}&format=image/png&service=WMS&version=1.1.1&request=GetMap&srs=EPSG:3857&transparent=true&width=256&height=256&layers=8&styles=",
      ],
      tileSize: 256,
    },
    isActive: false,
    opacity: 1,
    attribution: "USGS Watershed Boundary Dataset",
  },
  {
    id: "arcgis-public-access",
    displayName: "Public Land",
    type: "vector",
    source: {
      type: "vector",
      // Define the source-layer name here (used as a default if not overridden)
      layerName: "PADUS",
      tiles: [
        "https://tiles.arcgis.com/tiles/v01gqwM5QqNysAAi/arcgis/rest/services/PADUS4_0VectorAnalysis_National_WebMerc_PA/VectorTileServer/tile/{z}/{y}/{x}.pbf",
      ],
      tileSize: 512,
    },
    isActive: false,
    opacity: 0.8,
    attribution: "PADUS 4.0",
    // Fill layer configuration defined in the layer.
    fillLayer: {
      id: "arcgis-public-access-fill",
      sourceLayer: "PADUS",
      layout: {},
      paint: {
        "fill-color": publicAccessColorExpression,
        "fill-outline-color": "#000000",
        // This value will be overridden by the layer's opacity state.
        "fill-opacity": 0.8,
      },
    },
    // Text layer configuration defined in the layer.
    secondaryLayer: {
      id: "arcgis-public-access-text",
      minzoom: 8, // Layer appears from zoom level 8 onward.
      maxzoom: 15, // Layer is hidden past zoom level 15.
      source: {
        id: "management-owner",
        layerName: "PADUS",
        type: "vector",
        tiles: [
          "https://tiles.arcgis.com/tiles/v01gqwM5QqNysAAi/arcgis/rest/services/PADUS_Management_Areas_Manager_Type/VectorTileServer/tile/{z}/{y}/{x}.pbf",
        ],
        tileSize: 512,
      },
      layout: (isActive: boolean) => ({
        "text-field": ["get", "MngTp_Desc"],
        visibility: isActive ? "visible" : "none",
        "text-size": [
          "interpolate",
          ["linear"],
          ["zoom"],
          8,
          0, // At zoom 8, text is hidden.
          10,
          8, // At zoom 10, text size is 8.
          13,
          12, // At zoom 13, text size grows to 12.
          15,
          16, // Past zoom 13, text size increases to 16 by zoom 15.
        ],
        "text-anchor": "center",
        "text-offset": [0, 0],
        "text-allow-overlap": false,
        "symbol-placement": "point",
        "text-optional": true,
      }),
      type: "symbol",
      paint: (opacity: number) => ({
        "text-color": "#000000",
        "text-opacity": [
          "interpolate",
          ["linear"],
          ["zoom"],
          8,
          0.5 * opacity,
          13,
          0.7 * opacity,
          15,
          0.9 * opacity,
        ],
      }),
    },
    legend: <PublicAccessLegend />,
  },
];

const LayerControl: React.FC = () => {
  const [mapLayers, setMapLayers] = useState<PlanarianMapLayer[]>(LAYERS);
  const [isTerrainActive, setIsTerrainActive] = useState(false);
  const [terrainExaggeration, setTerrainExaggeration] = useState(1.5);
  const { current: map } = useMap();

  const onLayerChecked = (layer: PlanarianMapLayer) => {
    const newLayers = mapLayers.map((l) =>
      l.id === layer.id ? { ...l, isActive: !l.isActive } : l
    );
    setMapLayers(newLayers);
  };

  const onLayerOpacityChanged = (layer: PlanarianMapLayer, opacity: number) => {
    const newLayers = mapLayers.map((l) =>
      l.id === layer.id ? { ...l, opacity } : l
    );
    setMapLayers(newLayers);
  };

  const onTerrainToggle = () => {
    setIsTerrainActive((prevState) => !prevState);
    if (isTerrainActive) {
      map?.getMap().setTerrain(null);
    } else {
      map?.getMap().setTerrain({
        source: "terrainLayer",
        exaggeration: terrainExaggeration,
      });
    }
  };

  return (
    <>
      <Source
        id="terrainLayer"
        type="raster-dem"
        tiles={[
          "https://api.maptiler.com/tiles/terrain-rgb-v2/{z}/{x}/{y}.webp?key=G0kZR1vCDJukD1MigCcI",
        ]}
        tileSize={256}
      />
      {mapLayers.map((layer) => {
        if (layer.type === "raster") {
          return (
            <Source
              key={layer.id}
              id={layer.id}
              type="raster"
              tiles={layer.source.tiles}
              attribution={layer.attribution}
            >
              <Layer
                key={layer.id}
                source={layer.id}
                paint={{ "raster-opacity": layer.opacity }}
                type="raster"
                layout={{ visibility: layer.isActive ? "visible" : "none" }}
              />
            </Source>
          );
        } else if (layer.type === "vector") {
          return (
            <React.Fragment key={layer.id}>
              <Source
                id={layer.id}
                type="vector"
                tiles={layer.source.tiles}
                attribution={layer.attribution}
              >
                {layer.fillLayer && (
                  <Layer
                    id={layer.fillLayer.id}
                    source={layer.id}
                    // Use the fill layer sourceLayer defined in the configuration.
                    source-layer={
                      layer.fillLayer.sourceLayer || layer.source.layerName
                    }
                    type="fill"
                    layout={{
                      ...layer.fillLayer.layout,
                      visibility: layer.isActive ? "visible" : "none",
                    }}
                    paint={{
                      ...layer.fillLayer.paint,
                      "fill-opacity": layer.opacity,
                    }}
                  />
                )}
              </Source>
              {layer.secondaryLayer && (
                <Source
                  id={layer.secondaryLayer.id}
                  type="vector"
                  tiles={layer.secondaryLayer.source.tiles}
                >
                  <Layer
                    id={layer.secondaryLayer.id}
                    source={layer.secondaryLayer.id}
                    source-layer={layer.secondaryLayer.source.layerName}
                    type={layer.secondaryLayer.type}
                    layout={layer.secondaryLayer.layout(layer.isActive)}
                    paint={layer.secondaryLayer.paint(layer.opacity)}
                    {...(layer.secondaryLayer.minzoom !== undefined && {
                      minzoom: layer.secondaryLayer.minzoom,
                    })}
                    {...(layer.secondaryLayer.maxzoom !== undefined && {
                      maxzoom: layer.secondaryLayer.maxzoom,
                    })}
                  />
                </Source>
              )}
            </React.Fragment>
          );
        }
        return null;
      })}
      <ControlPanel style={{ zIndex: 11 }}>
        <HoverIcon style={{ zIndex: 200 }}>
          <svg
            xmlns="http://www.w3.org/2000/svg"
            height="24px"
            viewBox="0 0 576 512"
          >
            <path d="M264.5 5.2c14.9-6.9 32.1-6.9 47 0l218.6 101c8.5 3.9 13.9 12.4 13.9 21.8s-5.4 17.9-13.9 21.8l-218.6 101c-14.9 6.9-32.1 6.9-47 0L45.9 149.8C37.4 145.8 32 137.3 32 128s5.4-17.9 13.9-21.8L264.5 5.2zM476.9 209.6l53.2 24.6c8.5 3.9 13.9 12.4 13.9 21.8s-5.4 17.9-13.9 21.8l-218.6 101c-14.9 6.9-32.1 6.9-47 0L45.9 277.8C37.4 273.8 32 265.3 32 256s5.4-17.9 13.9-21.8l53.2-24.6 152 70.2c23.4 10.8 50.4 10.8 73.8 0l152-70.2zm-152 198.2l152-70.2 53.2 24.6c8.5 3.9 13.9 12.4 13.9 21.8s-5.4 17.9-13.9 21.8l-218.6 101c-14.9 6.9-32.1 6.9-47 0L45.9 405.8C37.4 401.8 32 393.3 32 384s5.4-17.9 13.9-21.8l53.2-24.6 152 70.2c23.4 10.8 50.4 10.8 73.8 0z" />
          </svg>
        </HoverIcon>
        <ContentWrapper>
          Layers
          <div
            style={{
              maxHeight: "55vh",
              overflowY: "auto",
              overflowX: "hidden",
              marginBottom: "10px",
            }}
          >
            {mapLayers.map((layer) => (
              <div key={layer.id} style={{ marginBottom: "15px" }}>
                <Checkbox
                  onChange={() => onLayerChecked(layer)}
                  checked={layer.isActive}
                >
                  {layer.displayName}
                </Checkbox>
                <Slider
                  min={0}
                  max={1}
                  step={0.1}
                  value={layer.opacity}
                  onChange={(value: number) =>
                    onLayerOpacityChanged(layer, value)
                  }
                />
              </div>
            ))}
          </div>
          <Space direction="vertical">
            <PlanarianButton
              alwaysShowChildren
              onClick={onTerrainToggle}
              icon={<BuildOutlined />}
            >
              {isTerrainActive ? "Disable 3D Terrain" : "Enable 3D Terrain"}
            </PlanarianButton>
            {isTerrainActive && (
              <div>
                Exaggeration:
                <InputNumber
                  min={0}
                  max={10}
                  step={0.1}
                  value={terrainExaggeration}
                  onChange={(value) => {
                    if (value !== null) {
                      setTerrainExaggeration(value);
                      map?.getMap().setTerrain({
                        source: "terrainLayer",
                        exaggeration: value,
                      });
                    }
                  }}
                />
              </div>
            )}
          </Space>
        </ContentWrapper>
      </ControlPanel>

      <div>
        {mapLayers
          .filter((layer) => layer.isActive)
          .map((layer) =>
            layer.legend ? (
              <LegendPanel>
                <HoverIcon style={{ zIndex: 300 }}>
                  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">
                    <path d="M256 512A256 256 0 1 0 256 0a256 256 0 1 0 0 512zM216 336l24 0 0-64-24 0c-13.3 0-24-10.7-24-24s10.7-24 24-24l48 0c13.3 0 24 10.7 24 24l0 88 8 0c13.3 0 24 10.7 24 24s-10.7 24-24 24l-80 0c-13.3 0-24-10.7-24-24s10.7-24 24-24zm40-208a32 32 0 1 1 0 64 32 32 0 1 1 0-64z" />
                  </svg>
                </HoverIcon>
                <ContentWrapper>
                  <div key={`legend-${layer.id}`}>{layer.legend}</div>
                </ContentWrapper>
              </LegendPanel>
            ) : null
          )}
      </div>
    </>
  );
};

const ControlPanel = styled.div`
  position: absolute;
  top: 50px;
  right: 0;
  background: white;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.3);
  padding: 8px;
  margin: 20px;
  font-size: 13px;
  line-height: 2;
  color: #6b6b76;
  border-radius: 8px;
  outline: none;
  transition: all 0.3s ease;
  &:hover {
    max-width: none;
  }
`;

const LegendPanel = styled.div`
  position: absolute;
  bottom: -10px;
  left: -10px;
  border-radius: 8px;
  background: white;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.3);
  padding: 8px;
  margin: 20px;
  font-size: 13px;
  line-height: 2;
  color: #6b6b76;
  z-index: 12;
  transition: all 0.3s ease;
  &:hover {
    max-width: none;
  }
`;

const HoverIcon = styled.div`
  display: block;
  width: 28px;
  height: 24px;
  cursor: pointer;
  ${ControlPanel}:hover &,
  ${LegendPanel}:hover & {
    display: none;
  }
`;

const ContentWrapper = styled.div`
  display: none;
  ${ControlPanel}:hover &,
  ${LegendPanel}:hover & {
    display: block;
  }
`;

const LayerMemoComponent = React.memo(LayerControl);

export { LayerMemoComponent as LayerControl };
