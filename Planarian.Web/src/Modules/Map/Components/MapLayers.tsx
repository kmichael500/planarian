import React, { useEffect } from "react";
import { Layer, Source, useMap } from "react-map-gl/maplibre";
import { AppOptions } from "../../../Shared/Services/AppService";
import { MAP_LAYER_DEFINITIONS, MAPTERHORN_TILEJSON_URL } from "./MapLayerDefinitions";
import { useMapLayers } from "./MapLayerContext";
import { MapHydrologyOverlayLayer } from "./MapHydrologyOverlayLayer";
import { MapStreamGageOverlayLayer } from "./MapStreamGageOverlayLayer";
import { MAP_OVERLAY_ANCHOR_LAYER_ID } from "./PlanarianBaseMap";

const resolveTiles = (tiles: string[] | undefined) =>
  tiles?.map((tile) => tile.startsWith("/api/") ? `${AppOptions.apiBaseUrl}${tile}` : tile);

const TerrainController = () => {
  const { current: map } = useMap();
  const { terrainEnabled, terrainExaggeration } = useMapLayers();

  useEffect(() => {
    const instance = map?.getMap();
    if (!instance) return;

    const applyTerrain = () => {
      if (!instance.isStyleLoaded()) return;

      if (terrainEnabled && instance.getSource("mapterhorn-terrain")) {
        instance.setTerrain({
          source: "mapterhorn-terrain",
          exaggeration: terrainExaggeration,
        });
      } else if (!terrainEnabled && instance.getTerrain()) {
        instance.setTerrain(null);
      }
    };

    if (instance.isStyleLoaded()) {
      applyTerrain();
      return;
    }

    instance.once("load", applyTerrain);
    return () => {
      instance.off("load", applyTerrain);
    };
  }, [map, terrainEnabled, terrainExaggeration]);

  return null;
};

interface MapLayersProps {
  includeDataOverlays?: boolean;
}

export const MapLayers: React.FC<MapLayersProps> = ({ includeDataOverlays = true }) => {
  const { isVisible, opacity } = useMapLayers();

  return (
    <>
      <Source id="mapterhorn-terrain" type="raster-dem" url={MAPTERHORN_TILEJSON_URL} tileSize={512} encoding="terrarium" />
      <TerrainController />
      {MAP_LAYER_DEFINITIONS.map((definition) => {
        if (definition.type === "group" || definition.type === "overlay") return null;
        const visible = isVisible(definition.id);
        const layerOpacity = opacity(definition.id);

        if (definition.type === "raster") {
          return (
            <Source
              key={definition.id}
              id={definition.id}
              type="raster"
              tiles={resolveTiles(definition.source.tiles)}
              tileSize={definition.source.tileSize}
              attribution={definition.attribution}
              {...(definition.source.minzoom !== undefined && { minzoom: definition.source.minzoom })}
              {...(definition.source.maxzoom !== undefined && { maxzoom: definition.source.maxzoom })}
            >
              <Layer
                id={definition.id}
                type="raster"
                beforeId={MAP_OVERLAY_ANCHOR_LAYER_ID}
                paint={{ "raster-opacity": layerOpacity }}
                layout={{ visibility: visible ? "visible" : "none" }}
                {...(definition.minzoom !== undefined && { minzoom: definition.minzoom })}
                {...(definition.maxzoom !== undefined && { maxzoom: definition.maxzoom })}
              />
            </Source>
          );
        }

        if (definition.type === "hillshade") {
          return (
            <Source
              key={definition.id}
              id={definition.id}
              type="raster-dem"
              url={definition.source.url}
              tileSize={definition.source.tileSize}
              encoding={definition.source.encoding}
              attribution={definition.attribution}
            >
              <Layer
                id={definition.id}
                type="hillshade"
                beforeId={MAP_OVERLAY_ANCHOR_LAYER_ID}
                paint={{
                  "hillshade-exaggeration": 1,
                  "hillshade-shadow-color": `rgba(0, 0, 0, ${layerOpacity})`,
                  "hillshade-highlight-color": `rgba(255, 255, 255, ${layerOpacity})`,
                  "hillshade-accent-color": `rgba(0, 0, 0, ${layerOpacity})`,
                }}
                layout={{ visibility: visible ? "visible" : "none" }}
              />
            </Source>
          );
        }

        return (
          <React.Fragment key={definition.id}>
            <Source id={definition.id} type="vector" tiles={resolveTiles(definition.source.tiles)} attribution={definition.attribution}>
              <Layer
                id={definition.fillLayer.id}
                type="fill"
                beforeId={MAP_OVERLAY_ANCHOR_LAYER_ID}
                source-layer={definition.fillLayer.sourceLayer}
                layout={{ ...definition.fillLayer.layout, visibility: visible ? "visible" : "none" }}
                paint={{ ...definition.fillLayer.paint, "fill-opacity": layerOpacity }}
              />
            </Source>
            {definition.labelLayer && (
              <Source id={`${definition.id}-labels-source`} type="vector" tiles={resolveTiles(definition.labelLayer.source.tiles)}>
                <Layer
                  id={definition.labelLayer.id}
                  type="symbol"
                  beforeId={MAP_OVERLAY_ANCHOR_LAYER_ID}
                  source-layer={definition.labelLayer.source.layerName}
                  minzoom={definition.labelLayer.minzoom}
                  maxzoom={definition.labelLayer.maxzoom}
                  layout={{ ...definition.labelLayer.layout, visibility: visible ? "visible" : "none" }}
                  paint={definition.labelLayer.paint(layerOpacity)}
                />
              </Source>
            )}
          </React.Fragment>
        );
      })}
      {includeDataOverlays && (
        <>
          <MapHydrologyOverlayLayer />
          <MapStreamGageOverlayLayer />
        </>
      )}
    </>
  );
};
