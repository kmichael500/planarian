import { Checkbox, InputNumber, Slider, Space } from "antd";
import React, { useState, useEffect } from "react";
import { Source, Layer, useMap } from "react-map-gl/maplibre";
import styled from "styled-components";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { BuildOutlined } from "@ant-design/icons";
import type { DataDrivenPropertyValueSpecification } from "maplibre-gl";
import { PublicAccessLegend } from "./PublicAccessLegend";
import { PUBLIC_ACCESS_INFO } from "./PublicAccesDetails";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";

interface PlanarianMapLayer {
  displayName: string;
  isActive: boolean;
  opacity: number;
  id: string;
  type: "raster" | "vector" | "group" | string;
  attribution?: string;
  source?: {
    layerName?: string;
    type: string;
    tiles: string[];
    tileSize?: number;
  };
  paint?: { "raster-opacity"?: number };
  fillLayer?: {
    id: string;
    sourceLayer: string;
    layout: Record<string, any>;
    paint: Record<string, any>;
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
    layout: (isActive: boolean) => Record<string, any>;
    paint: (opacity: number) => Record<string, any>;
  };
  legend?: React.ReactNode;
  memberLayerIds?: string[];
  isGroupMember?: boolean;
}

const MAPBOX_ACCESS_TOKEN =
  "pk.eyJ1IjoibWljaGFlbGtldHpuZXIiLCJhIjoiY2xvODFyN3lqMDl3bzJxbm56d3lzOTBkNyJ9.9_UNmt2gelLuQ-BPQjPiCQ";

const publicAccessColorExpression: DataDrivenPropertyValueSpecification<string> =
  [
    "match",
    ["get", "Pub_Access"],
    ...Object.entries(PUBLIC_ACCESS_INFO).flatMap(([code, info]) => [
      code,
      info.color,
    ]),
    "#cccccc",
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
    displayName: "Macrostrat Geology",
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

const LayerControl: React.FC<{
  position?: Partial<Record<"top" | "right" | "left" | "bottom", string>>;
}> = ({ position }) => {
  const accountId = AuthenticationService.GetAccountId();
  const LOCAL_STORAGE_LAYER_STATES_KEY = `planarianMapLayerStates-${accountId}`;
  const LOCAL_STORAGE_PREV_GROUP_MEMBERS_KEY = `planarianPrevGroupMembers-${accountId}`;
  const LOCAL_STORAGE_MACROSTRAT_DISCLAIMER_KEY = `planarianMacrostratDisclaimerShown-${accountId}`;

  const [mapLayers, setMapLayers] = useState<PlanarianMapLayer[]>(() => {
    const savedLayerStatesString = localStorage.getItem(
      LOCAL_STORAGE_LAYER_STATES_KEY
    );
    let initialLayers = LAYERS.map((layer) => ({ ...layer }));

    if (savedLayerStatesString) {
      try {
        const savedStates: Array<{
          id: string;
          isActive: boolean;
          opacity: number;
        }> = JSON.parse(savedLayerStatesString);
        initialLayers = initialLayers.map((defaultLayer) => {
          const savedState = savedStates.find((s) => s.id === defaultLayer.id);
          if (savedState) {
            return {
              ...defaultLayer,
              isActive: savedState.isActive,
              opacity: savedState.opacity,
            };
          }
          return defaultLayer;
        });
      } catch (e) {
        console.error(
          "Failed to parse saved layer states from localStorage",
          e
        );
      }
    }
    return initialLayers;
  });

  const [prevGroupMembers, setPrevGroupMembers] = useState<
    Record<string, string[]>
  >(() => {
    const savedPrevGroupMembersString = localStorage.getItem(
      LOCAL_STORAGE_PREV_GROUP_MEMBERS_KEY
    );
    if (savedPrevGroupMembersString) {
      try {
        return JSON.parse(savedPrevGroupMembersString);
      } catch (e) {
        console.error(
          "Failed to parse saved prevGroupMembers from localStorage",
          e
        );
      }
    }
    return {};
  });

  const [isTerrainActive, setIsTerrainActive] = useState(false);
  const [terrainExaggeration, setTerrainExaggeration] = useState(1);
  const [showMacrostratDisclaimer, setShowMacrostratDisclaimer] =
    useState(false);

  const { current: map } = useMap();

  useEffect(() => {
    const statesToSave = mapLayers.map((l) => ({
      id: l.id,
      isActive: l.isActive,
      opacity: l.opacity,
    }));
    localStorage.setItem(
      LOCAL_STORAGE_LAYER_STATES_KEY,
      JSON.stringify(statesToSave)
    );
  }, [mapLayers, LOCAL_STORAGE_LAYER_STATES_KEY]);

  useEffect(() => {
    localStorage.setItem(
      LOCAL_STORAGE_PREV_GROUP_MEMBERS_KEY,
      JSON.stringify(prevGroupMembers)
    );
  }, [prevGroupMembers, LOCAL_STORAGE_PREV_GROUP_MEMBERS_KEY]);

  useEffect(() => {
    if (map) {
      if (isTerrainActive) {
        map.getMap().setTerrain({
          source: "terrainLayer",
          exaggeration: terrainExaggeration,
        });
      } else {
        map.getMap().setTerrain(null);
      }
    }
  }, [map, isTerrainActive, terrainExaggeration]);

  const handleNormalToggle = (layer: PlanarianMapLayer) => {
    const newIsActive = !layer.isActive;
    if (
      layer.id === "macrostrat" &&
      newIsActive &&
      !localStorage.getItem(LOCAL_STORAGE_MACROSTRAT_DISCLAIMER_KEY)
    ) {
      setShowMacrostratDisclaimer(true);
    }
    setMapLayers((prev) =>
      prev.map((l) => (l.id === layer.id ? { ...l, isActive: newIsActive } : l))
    );
  };

  const handleMemberToggle = (layer: PlanarianMapLayer) => {
    const memberId = layer.id;
    const newState = !layer.isActive;

    setMapLayers((prevMapLayers) => {
      const newLayers = prevMapLayers.map((l) =>
        l.id === memberId ? { ...l, isActive: newState } : l
      );

      const parentGroupDefinition = LAYERS.find(
        (l) => l.type === "group" && l.memberLayerIds?.includes(memberId)
      );

      if (parentGroupDefinition && parentGroupDefinition.memberLayerIds) {
        const parentGroupId = parentGroupDefinition.id;
        const activeMembersInGroup =
          parentGroupDefinition.memberLayerIds.filter((id) => {
            const member = newLayers.find((m) => m.id === id);
            return member && member.isActive;
          }).length;

        if (activeMembersInGroup === 0) {
          setPrevGroupMembers((prev) => {
            const copy = { ...prev };
            delete copy[parentGroupId];
            return copy;
          });
        }
      }
      return newLayers;
    });
  };

  const handleGroupToggle = (layer: PlanarianMapLayer) => {
    const groupId = layer.id;
    const memberIds = layer.memberLayerIds!;

    const currentlyActiveMemberIds = mapLayers
      .filter((l) => memberIds.includes(l.id) && l.isActive)
      .map((l) => l.id);
    const isAnyMemberCurrentlyActive = currentlyActiveMemberIds.length > 0;

    if (isAnyMemberCurrentlyActive) {
      setPrevGroupMembers((prev) => ({
        ...prev,
        [groupId]: currentlyActiveMemberIds,
      }));
      setMapLayers((prevMapLayers) =>
        prevMapLayers.map((l) =>
          memberIds.includes(l.id) ? { ...l, isActive: false } : l
        )
      );
    } else {
      const previouslyRememberedActiveMembers = prevGroupMembers[groupId];
      let membersToActivate: string[];

      if (
        previouslyRememberedActiveMembers &&
        previouslyRememberedActiveMembers.length > 0
      ) {
        membersToActivate = previouslyRememberedActiveMembers;
      } else {
        membersToActivate = [...memberIds];
      }

      setMapLayers((prevMapLayers) =>
        prevMapLayers.map((l) =>
          memberIds.includes(l.id)
            ? { ...l, isActive: membersToActivate.includes(l.id) }
            : l
        )
      );
    }
  };

  const handleLayerToggle = (layer: PlanarianMapLayer) => {
    if (layer.type === "group" && layer.memberLayerIds) {
      handleGroupToggle(layer);
    } else if (layer.isGroupMember) {
      handleMemberToggle(layer);
    } else {
      handleNormalToggle(layer);
    }
  };

  const onLayerOpacityChanged = (layer: PlanarianMapLayer, opacity: number) => {
    setMapLayers((prev) =>
      prev.map((l) =>
        l.id === layer.id ||
        (layer.type === "group" && layer.memberLayerIds?.includes(l.id))
          ? { ...l, opacity }
          : l
      )
    );
  };

  const onTerrainToggle = () => {
    setIsTerrainActive((on: boolean) => !on);
  };

  const handleCloseMacrostratDisclaimer = () => {
    if (showMacrostratDisclaimer) {
      localStorage.setItem(LOCAL_STORAGE_MACROSTRAT_DISCLAIMER_KEY, "true");
    }
    setShowMacrostratDisclaimer(false);
  };

  const legendPosition = {
    top: position?.top ? `${parseInt(position.top) + 50}px` : "100px",
    right: position?.right || "0",
    left: position?.left || "auto",
    bottom: position?.bottom || "auto",
  };

  return (
    <>
      <PlanarianModal
        open={showMacrostratDisclaimer}
        onClose={handleCloseMacrostratDisclaimer}
        height={"auto"}
        header="Macrostrat Geology Disclaimer"
        footer={
          <PlanarianButton
            alwaysShowChildren
            onClick={handleCloseMacrostratDisclaimer}
            icon={undefined}
          >
            Close
          </PlanarianButton>
        }
        width="600px"
      >
        <p>
          Macrostrat coverage varies by region and may be less detailed than
          traditional geologic maps.
        </p>
        <p>Typical resolution by state:</p>
        <ul>
          <li>KY – 1:24,000</li>
          <li>TN – 1:250,000</li>
          <li>AL – 1:250,000</li>
          <li>GA – 1:500,000</li>
        </ul>
        <p>Only 1:24,000 is generally suitable for precision use.</p>
        <p>
          For the most detailed map available, turn on the NGMDB Geology layer
          and select the smallest scale.
        </p>
      </PlanarianModal>
      <Source
        id="terrainLayer"
        type="raster-dem"
        tiles={[
          "https://api.maptiler.com/tiles/terrain-rgb-v2/{z}/{x}/{y}.webp?key=G0kZR1vCDJukD1MigCcI",
        ]}
        tileSize={256}
      />

      {mapLayers.map((layer) => {
        if (layer.type === "raster" && layer.source) {
          return (
            <Source
              key={layer.id}
              id={layer.id}
              type="raster"
              tiles={layer.source.tiles}
              attribution={layer.attribution}
            >
              <Layer
                id={layer.id}
                source={layer.id}
                type="raster"
                paint={{ "raster-opacity": layer.opacity }}
                layout={{ visibility: layer.isActive ? "visible" : "none" }}
              />
            </Source>
          );
        }
        if (layer.type === "vector" && layer.source) {
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
                    type="fill"
                    source={layer.id}
                    source-layer={
                      layer.fillLayer.sourceLayer || layer.source.layerName
                    }
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
                    type={layer.secondaryLayer.type}
                    source={layer.secondaryLayer.id}
                    source-layer={layer.secondaryLayer.source.layerName}
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
            style={{ maxHeight: "55vh", overflowY: "auto", marginBottom: 10 }}
          >
            {mapLayers
              .filter((l) => !l.isGroupMember)
              .map((layer) => {
                const isGroup =
                  layer.type === "group" && !!layer.memberLayerIds;
                const memberIds = layer.memberLayerIds || [];
                const members = mapLayers.filter((l) =>
                  memberIds.includes(l.id)
                );
                const activeCount = members.filter((m) => m.isActive).length;
                const allCount = members.length;
                const checked = isGroup
                  ? activeCount === allCount && allCount > 0
                  : layer.isActive;
                const indeterminate = isGroup
                  ? activeCount > 0 && activeCount < allCount
                  : false;

                return (
                  <div key={layer.id} style={{ marginBottom: 15 }}>
                    <Checkbox
                      checked={checked}
                      indeterminate={indeterminate}
                      onChange={() => handleLayerToggle(layer)}
                    >
                      {layer.displayName}
                    </Checkbox>
                    <Slider
                      min={0}
                      max={1}
                      step={0.1}
                      value={layer.opacity}
                      onChange={(v) => onLayerOpacityChanged(layer, v)}
                    />
                    {isGroup && activeCount > 0 && (
                      <div style={{ marginLeft: 20, marginTop: 5 }}>
                        {layer.memberLayerIds!.map((id) => {
                          const member = mapLayers.find((m) => m.id === id)!;
                          return (
                            <div key={id} style={{ marginBottom: 5 }}>
                              <Checkbox
                                checked={member.isActive}
                                onChange={() => handleLayerToggle(member)}
                              >
                                {member.displayName}
                              </Checkbox>
                            </div>
                          );
                        })}
                      </div>
                    )}
                  </div>
                );
              })}
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
                  onChange={(v) => {
                    if (v !== null) {
                      setTerrainExaggeration(v);
                      map?.getMap().setTerrain({
                        source: "terrainLayer",
                        exaggeration: v,
                      });
                    }
                  }}
                />
              </div>
            )}
          </Space>
        </ContentWrapper>
      </ControlPanel>

      {mapLayers
        .filter((l) => l.isActive && l.legend)
        .map((l) => (
          <LegendPanel key={`legend-${l.id}`} style={legendPosition}>
            <HoverIcon>
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">
                <path d="M256 512A256 256 0 1 0 256 0a256 256 0 1 0 0 512zM216 336l24 0 0-64-24 0c-13.3 0-24-10.7-24-24s10.7-24 24-24l48 0c13.3 0 24 10.7 24 24l0 88 8 0c13.3 0 24 10.7 24 24s-10.7 24-24 24l-80 0c-13.3 0-24-10.7-24-24s10.7-24 24-24zm40-208a32 32 0 1 1 0 64 32 32 0 1 1 0-64z" />
              </svg>
            </HoverIcon>
            <ContentWrapper>{l.legend}</ContentWrapper>{" "}
          </LegendPanel>
        ))}
    </>
  );
};

const ControlPanel = styled.div`
  position: absolute;
  background: #fff;
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

const LegendPanel = styled(ControlPanel)`
  top: 100px;
  z-index: 12;
`;

const HoverIcon = styled.div`
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

export const LayerMemoComponent = React.memo(LayerControl);
export { LayerMemoComponent as LayerControl };
