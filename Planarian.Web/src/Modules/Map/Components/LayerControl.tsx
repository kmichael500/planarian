import { Checkbox, InputNumber, Slider, Space } from "antd";
import React, { useState } from "react";
import { Source, Layer, useMap } from "react-map-gl/maplibre";
import styled from "styled-components";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { BuildOutlined } from "@ant-design/icons";
import type { DataDrivenPropertyValueSpecification } from "maplibre-gl";
import { PublicAccessLegend } from "./PublicAccessLegend";
import { PUBLIC_ACCESS_INFO } from "./PublicAccesDetails";

const MAPBOX_ACCESS_TOKEN =
  "pk.eyJ1IjoibWljaGFlbGtldHpuZXIiLCJhIjoiY2xvODFyN3lqMDl3bzJxbm56d3lzOTBkNyJ9.9_UNmt2gelLuQ-BPQjPiCQ";

interface PlanarianMapLayer {
  displayName: string;
  isActive: boolean;
  opacity: number;
  id: string;
  type: "raster" | "vector" | "group" | string; // Added "group"
  attribution?: string;
  source?: {
    layerName?: string;
    type: string;
    tiles: string[];
    tileSize?: number;
  };
  paint?: {
    "raster-opacity"?: number;
  };
  fillLayer?: {
    id: string;
    sourceLayer: string;
    layout: { [key: string]: any };
    paint: { [key: string]: any };
  };
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
  memberLayerIds?: string[]; // For group layers
  isGroupMember?: boolean; // For layers part of a group
}

interface LayerControlProps {
  position?: {
    top?: string;
    right?: string;
    left?: string;
    bottom?: string;
  };
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
    id: "mapbox-street",
    displayName: "Street",
    type: "raster",
    source: {
      type: "raster",
      tiles: [
        `https://api.mapbox.com/styles/v1/mapbox/streets-v11/tiles/512/{z}/{x}/{y}?access_token=${MAPBOX_ACCESS_TOKEN}`,
      ],
      tileSize: 512,
    },
    isActive: true,
    opacity: 1,
    attribution: "© Mapbox",
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
    id: "mapbox-satellite",
    displayName: "Satellite",
    type: "raster",
    source: {
      type: "raster",
      tiles: [
        `https://api.mapbox.com/styles/v1/mapbox/satellite-v9/tiles/256/{z}/{x}/{y}?access_token=${MAPBOX_ACCESS_TOKEN}`,
      ],
      tileSize: 256,
    },
    isActive: false,
    opacity: 1,
    attribution: "© Mapbox",
  },

  {
    id: "3-dep-hillshade-usgs",
    displayName: "Hillshade",
    type: "raster",
    source: {
      type: "raster",
      tiles: [
        "https://elevation.nationalmap.gov/arcgis/services/3DEPElevation/ImageServer/WMSServer?bbox={bbox-epsg-3857}&format=image/png&service=WMS&version=1.1.1&request=GetMap&srs=EPSG:3857&transparent=true&width=256&height=256&layers=3DEPElevation:Hillshade Multidirectional",
      ],
    },
    isActive: false,
    opacity: 1,
    attribution: "USGS 3DEP Elevation Program",
  },
  {
    id: "arcgis-public-access",
    displayName: "Public Land",
    type: "vector",
    source: {
      type: "vector",
      layerName: "PADUS",
      tiles: [
        "https://tiles.arcgis.com/tiles/v01gqwM5QqNysAAi/arcgis/rest/services/PADUS4_0VectorAnalysis_National_WebMerc_PA/VectorTileServer/tile/{z}/{y}/{x}.pbf",
      ],
      tileSize: 512,
    },
    isActive: false,
    opacity: 0.8,
    attribution: "PADUS 4.0",
    fillLayer: {
      id: "arcgis-public-access-fill",
      sourceLayer: "PADUS",
      layout: {},
      paint: {
        "fill-color": publicAccessColorExpression,
        "fill-outline-color": "#000000",
        "fill-opacity": 0.8,
      },
    },
    secondaryLayer: {
      id: "arcgis-public-access-text",
      minzoom: 8,
      maxzoom: 15,
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
          0,
          10,
          8,
          13,
          12,
          15,
          16,
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
    id: "ngmdb-geology-group",
    displayName: "NGMDB Geology",
    type: "group",
    isActive: false,
    opacity: 1,
    memberLayerIds: [
      "usgs-500k-geology",
      "usgs-250k-geology",
      "usgs-125k-geology",
      "usgs-100k-geology",
      "usgs-62k-geology",
      "usgs-48k-geology",
      "usgs-24k-geology",
    ],
    attribution: "NGMDB Map Viewer",
  },
  {
    id: "usgs-500k-geology",
    displayName: "500K",
    type: "raster",
    source: {
      type: "raster",
      tiles: [
        "https://ngmdb.usgs.gov/arcgis/rest/services/mvCaches/mvCache500K/ImageServer/tile/{z}/{y}/{x}",
      ],
      tileSize: 256,
    },
    isActive: false,
    opacity: 1,
    isGroupMember: true,
    attribution: "NGMDB Map Viewer",
  },
  {
    id: "usgs-250k-geology",
    displayName: "250K",
    type: "raster",
    source: {
      type: "raster",
      tiles: [
        "https://ngmdb.usgs.gov/arcgis/rest/services/mvCaches/mvCache250K/ImageServer/tile/{z}/{y}/{x}",
      ],
      tileSize: 256,
    },
    isActive: false,
    opacity: 1,
    isGroupMember: true,
    attribution: "NGMDB Map Viewer",
  },
  {
    id: "usgs-125k-geology",
    displayName: "125K",
    type: "raster",
    source: {
      type: "raster",
      tiles: [
        "https://ngmdb.usgs.gov/arcgis/rest/services/mvCaches/mvCache125K/ImageServer/tile/{z}/{y}/{x}",
      ],
      tileSize: 256,
    },
    isActive: false,
    opacity: 1,
    isGroupMember: true,
    attribution: "NGMDB Map Viewer",
  },
  {
    id: "usgs-100k-geology",
    displayName: "100K",
    type: "raster",
    source: {
      type: "raster",
      tiles: [
        "https://ngmdb.usgs.gov/arcgis/rest/services/mvCaches/mvCache100K/ImageServer/tile/{z}/{y}/{x}",
      ],
      tileSize: 256,
    },
    isActive: false,
    opacity: 1,
    isGroupMember: true,
    attribution: "NGMDB Map Viewer",
  },
  {
    id: "usgs-62k-geology",
    displayName: "62K",
    type: "raster",
    source: {
      type: "raster",
      tiles: [
        "https://ngmdb.usgs.gov/arcgis/rest/services/mvCaches/mvCache62K/ImageServer/tile/{z}/{y}/{x}",
      ],
      tileSize: 256,
    },
    isActive: false,
    opacity: 1,
    isGroupMember: true,
    attribution: "NGMDB Map Viewer",
  },
  {
    id: "usgs-48k-geology",
    displayName: "48K",
    type: "raster",
    source: {
      type: "raster",
      tiles: [
        "https://ngmdb.usgs.gov/arcgis/rest/services/mvCaches/mvCache48K/ImageServer/tile/{z}/{y}/{x}",
      ],
      tileSize: 256,
    },
    isActive: false,
    opacity: 1,
    isGroupMember: true,
    attribution: "NGMDB Map Viewer",
  },
  {
    id: "usgs-24k-geology",
    displayName: "24K",
    type: "raster",
    source: {
      type: "raster",
      tiles: [
        "https://ngmdb.usgs.gov/arcgis/rest/services/mvCaches/mvCache24K/ImageServer/tile/{z}/{y}/{x}",
      ],
      tileSize: 256,
    },
    isActive: false,
    opacity: 1,
    isGroupMember: true,
    attribution: "NGMDB Map Viewer",
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
];

const LayerControl: React.FC<LayerControlProps> = ({ position }) => {
  const [mapLayers, setMapLayers] = useState<PlanarianMapLayer[]>(LAYERS);
  const [isTerrainActive, setIsTerrainActive] = useState(false);
  const [terrainExaggeration, setTerrainExaggeration] = useState(1.5);
  const { current: map } = useMap();
  const [expandedGroups, setExpandedGroups] = useState<string[]>([]);

  const onLayerChecked = (layer: PlanarianMapLayer) => {
    let newLayers = [...mapLayers];
    const layerIndex = newLayers.findIndex((l) => l.id === layer.id);
    if (layerIndex === -1) return;

    const newActiveState = !newLayers[layerIndex].isActive;
    newLayers[layerIndex] = {
      ...newLayers[layerIndex],
      isActive: newActiveState,
    };

    if (layer.type === "group" && layer.memberLayerIds) {
      // Toggle all member layers
      newLayers = newLayers.map((l) =>
        layer.memberLayerIds?.includes(l.id)
          ? { ...l, isActive: newActiveState }
          : l
      );
    } else if (layer.isGroupMember) {
      // Check if all members of the parent group are now in the same state
      const parentGroup = newLayers.find(
        (g) => g.type === "group" && g.memberLayerIds?.includes(layer.id)
      );
      if (parentGroup && parentGroup.memberLayerIds) {
        const allMembersActive = parentGroup.memberLayerIds.every(
          (memberId) => newLayers.find((l) => l.id === memberId)?.isActive
        );
        const allMembersInactive = parentGroup.memberLayerIds.every(
          (memberId) => !newLayers.find((l) => l.id === memberId)?.isActive
        );

        if (allMembersActive) {
          const parentGroupIndex = newLayers.findIndex(
            (g) => g.id === parentGroup.id
          );
          if (parentGroupIndex !== -1) {
            newLayers[parentGroupIndex] = {
              ...newLayers[parentGroupIndex],
              isActive: true,
            };
          }
        } else if (allMembersInactive) {
          const parentGroupIndex = newLayers.findIndex(
            (g) => g.id === parentGroup.id
          );
          if (parentGroupIndex !== -1) {
            newLayers[parentGroupIndex] = {
              ...newLayers[parentGroupIndex],
              isActive: false,
            };
          }
        } else {
          // If members are in mixed states, set parent group to a "mixed" or indeterminate state if desired,
          // or simply reflect that not all are active by setting isActive to false.
          // For now, we'll ensure it's not fully "active" unless all members are.
          const parentGroupIndex = newLayers.findIndex(
            (g) => g.id === parentGroup.id
          );
          if (parentGroupIndex !== -1 && newLayers[parentGroupIndex].isActive) {
            // If the group was active, but now not all members are, set group to inactive.
            // Or, introduce an indeterminate state for the group checkbox if your UI library supports it.
            // For simplicity here, we'll just ensure it's not marked as fully active.
            // A more sophisticated approach might involve a tri-state checkbox for the group.
          }
        }
      }
    }
    setMapLayers(newLayers);
  };

  const onLayerOpacityChanged = (layer: PlanarianMapLayer, opacity: number) => {
    let newLayers = mapLayers.map((l) =>
      l.id === layer.id ? { ...l, opacity } : l
    );

    if (layer.type === "group" && layer.memberLayerIds) {
      newLayers = newLayers.map((l) =>
        layer.memberLayerIds?.includes(l.id) ? { ...l, opacity } : l
      );
    }
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

  const legendPosition = {
    top:
      position && position.top ? `${parseInt(position.top) + 50}px` : "100px",
    right: position?.right || "0",
    left: position?.left || "auto",
    bottom: position?.bottom || "auto",
  };

  const toggleGroupExpansion = (groupId: string) => {
    setExpandedGroups((prev) =>
      prev.includes(groupId)
        ? prev.filter((id) => id !== groupId)
        : [...prev, groupId]
    );
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
              tiles={layer.source!.tiles}
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
        } else if (layer.type === "vector" && layer.source) {
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
      <ControlPanel
        style={{
          zIndex: 200,
          ...(position
            ? {
                top: position.top || "50px",
                right: position.right || "0",
                left: position.left || "auto",
                bottom: position.bottom || "auto",
              }
            : {}),
        }}
      >
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
            {mapLayers
              .filter((layer) => !layer.isGroupMember)
              .map((layer) => (
                <div key={layer.id} style={{ marginBottom: "15px" }}>
                  <Checkbox
                    onChange={() => {
                      onLayerChecked(layer);
                      toggleGroupExpansion(layer.id);
                    }}
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
                  {layer.type === "group" &&
                    expandedGroups.includes(layer.id) &&
                    layer.memberLayerIds && (
                      <div style={{ marginLeft: "20px", marginTop: "5px" }}>
                        {mapLayers
                          .filter((member) =>
                            layer.memberLayerIds?.includes(member.id)
                          )
                          .map((memberLayer) => (
                            <div
                              key={memberLayer.id}
                              style={{ marginBottom: "5px" }}
                            >
                              <Checkbox
                                onChange={() => onLayerChecked(memberLayer)}
                                checked={memberLayer.isActive}
                              >
                                {memberLayer.displayName}
                              </Checkbox>
                            </div>
                          ))}
                      </div>
                    )}
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
              <LegendPanel style={legendPosition}>
                <HoverIcon style={{ zIndex: 0 }}>
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
  top: 100px;
  right: 0;
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
