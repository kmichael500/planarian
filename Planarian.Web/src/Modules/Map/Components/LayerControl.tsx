import { Button, Checkbox, InputNumber, Slider, Space } from "antd";
import React from "react";
import { useState } from "react";
import { Source, Layer, useMap } from "react-map-gl/maplibre";
import styled from "styled-components";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { BuildOutlined } from "@ant-design/icons";

interface PlanarianMapLayer {
  displayName: string;
  isActive: boolean;
  opacity: number;
  id: string;
  type: string;
  attribution?: string;
  source: {
    type: string;
    tiles: string[];
    tileSize: number;
  }; // Added
  paint?: {
    "raster-opacity"?: number;
  };
}

const LAYERS = [
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
    id: "usgs-imagery",
    displayName: "Satellite",
    type: "raster",
    source: {
      type: "raster",
      tiles: [
        "https://basemap.nationalmap.gov/arcgis/rest/services/USGSImageryOnly/MapServer/WMTS/tile/1.0.0/USGSImageryOnly/default/GoogleMapsCompatible/{z}/{y}/{x}.png",
      ],
      tileSize: 256,
    },
    isActive: false,
    // maxzoom: 16,
    opacity: 1,
    attribution: "© USGS",
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
    attribution: "© Macrostrat",
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
    paint: {
      "raster-opacity": 1,
    },
    isActive: false,
    opacity: 1,
    attribution: "© USGS",
  },
  {
    id: "usgs-drainage-basins-16digit",
    displayName: "Subwatershed Boundary",
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
    attribution: "© USGS Watershed Boundary Dataset",
  },
] as PlanarianMapLayer[];

const LayerControl: React.FC = () => {
  const [mapLayers, setMapLayers] = useState<PlanarianMapLayer[]>(LAYERS);
  const [isTerrainActive, setIsTerrainActive] = useState(false);
  const [terrainExaggeration, setTerrainExaggeration] = useState(1.5);

  const { current: map } = useMap();

  const onLayerChecked = (layer: PlanarianMapLayer) => {
    const newLayers = mapLayers.map((l) => {
      if (l.id === layer.id) {
        return {
          ...l,
          isActive: !l.isActive,
        };
      }
      return l;
    });
    setMapLayers(newLayers);
  };

  const onLayerOpacityChanged = (layer: PlanarianMapLayer, opacity: number) => {
    const newLayers = mapLayers.map((l) => {
      if (l.id === layer.id) {
        return {
          ...l,
          opacity,
        };
      }
      return l;
    });
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
      ></Source>

      {mapLayers.map((layer) => (
        <Source
          key={layer.id}
          id={layer.id}
          type={"raster"}
          tiles={layer.source.tiles}
          attribution={layer.attribution}
        >
          <Layer
            key={layer.id}
            source={layer.id}
            paint={{ "raster-opacity": layer.opacity }}
            type="raster"
            layout={{ visibility: layer.isActive ? "visible" : "none" }}
          ></Layer>
        </Source>
      ))}

      <ControlPanel>
        <HoverIcon>
          {/* You can replace this with your desired icon */}
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
          {mapLayers.map((layer) => (
            <div key={layer.id}>
              <Checkbox
                onChange={() => {
                  onLayerChecked(layer);
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
            </div>
          ))}
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
    </>
  );
};

const ControlPanel = styled.div`
  position: absolute;
  top: 50px;
  right: 0;
  background: white;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.3);
  padding: 8px 8px;
  margin: 20px;
  font-size: 13px;
  line-height: 2;
  color: #6b6b76;
  outline: none;
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
  ${ControlPanel}:hover & {
    display: none;
  }
`;

const ContentWrapper = styled.div`
  display: none;
  ${ControlPanel}:hover & {
    display: block;
  }
`;

const LayerMemoComponent = React.memo(LayerControl);

export { LayerMemoComponent as LayerControl };
