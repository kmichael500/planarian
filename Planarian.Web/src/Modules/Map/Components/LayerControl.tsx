import { Checkbox, Slider } from "antd";
import maplibregl from "maplibre-gl";
import React from "react";
import { useEffect, useRef, useState } from "react";
import { useMap } from "react-map-gl/maplibre";
import styled from "styled-components";

interface PlanarianMapLayer extends maplibregl.Layer {
  displayName: string;
  isActive: boolean;
  opacity: number;
}

const LAYERS = [
  {
    id: "open-topo",
    displayName: "Topo",
    type: "raster",
    source: {
      type: "raster",
      tiles: ["https://tile.opentopomap.org/{z}/{x}/{y}.png"],
      tileSize: 256,
    },
    isActive: false, // Set to true if you want it active by default
    opacity: 1,
  },
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
    isActive: false, // Set to true if you want it active by default
    opacity: 1,
  },
] as PlanarianMapLayer[];

const LayerControl: React.FC = () => {
  const [mapLayers, setMapLayers] = useState<PlanarianMapLayer[]>(LAYERS);

  const { current: map } = useMap();

  useEffect(() => {
    if (map) {
      const mapInstance = map.getMap();

      const handleLoad = () => {
        mapLayers.forEach((layer) => {
          if (layer.isActive) {
            addLayer(layer);
          }
        });
      };
      mapInstance.on("load", handleLoad); // Listen for the load event

      return () => {
        mapInstance.off("load", handleLoad); // Clean up the event listener
      };
    }
  }, [map]); // Depend on map and mapLayers

  const toggleLayer = (layer: PlanarianMapLayer) => {
    if (!layer.isActive) {
      addLayer(layer);
    } else {
      removeLayer(layer);
    }
  };

  const handleOpacityChange = (layer: PlanarianMapLayer, value: number) => {
    if (map) {
      const updatedLayers = mapLayers.map((l) =>
        l.id === layer.id ? { ...l, opacity: value } : l
      );
      setMapLayers(updatedLayers);

      map.getMap().setPaintProperty(layer.id, "raster-opacity", value);
    }
  };

  const addLayer = (layer: PlanarianMapLayer) => {
    if (map) {
      const firstLayerId = map.getMap().getStyle().layers?.[0].id;
      map.getMap().addLayer(layer as maplibregl.AnyLayer, firstLayerId);

      const updatedLayers = mapLayers.map((l) =>
        l.id === layer.id ? { ...l, isActive: true } : l
      );
      setMapLayers(updatedLayers);
    }
  };

  const removeLayer = (layer: PlanarianMapLayer) => {
    if (map) {
      map.getMap().removeLayer(layer.id);
      map.getMap().removeSource(layer.id);

      const updatedLayers = mapLayers.map((l) =>
        l.id === layer.id ? { ...l, isActive: false } : l
      );
      setMapLayers(updatedLayers);
    }
  };

  return (
    <ControlPanel>
      Layers
      {mapLayers.map((layer) => (
        <div key={layer.id}>
          <Checkbox
            checked={layer.isActive}
            onChange={(e) => toggleLayer(layer)}
          >
            {layer.displayName}
          </Checkbox>
          <Slider
            min={0}
            max={1}
            step={0.1}
            value={layer.opacity}
            onChange={(value: number) => handleOpacityChange(layer, value)}
          />
        </div>
      ))}
    </ControlPanel>
  );
};

const ControlPanel = styled.div`
  position: absolute;
  top: 0;
  right: 0;
  max-width: 320px;
  background: #fff;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.3);
  padding: 12px 24px;
  margin: 20px;
  font-size: 13px;
  line-height: 2;
  color: #6b6b76;
  outline: none;
`;

const LayerMemoComponent = React.memo(LayerControl);

export { LayerMemoComponent as LayerControl };
