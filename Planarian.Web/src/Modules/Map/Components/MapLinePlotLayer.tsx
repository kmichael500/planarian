import React, { useEffect, useRef, useState } from "react";
import { Layer, Source, useMap } from "react-map-gl/maplibre";
import type { FeatureCollection } from "geojson";
import { message } from "antd";
import { MapService } from "../Services/MapService";

interface MapLinePlotLayerProps {
  viewportRevision?: number;
  attribution?: string;
}

type LoadedLinework = { id: string; data: FeatureCollection };

export const MapLinePlotLayer: React.FC<MapLinePlotLayerProps> = ({
  viewportRevision = 0,
  attribution,
}) => {
  const { current: map } = useMap();
  const [linework, setLinework] = useState<LoadedLinework[]>([]);
  const requestedIds = useRef(new Set<string>());
  const mounted = useRef(true);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  useEffect(() => {
    const load = async () => {
      try {
        const instance = map?.getMap();
        if (!instance || instance.getZoom() < 11) return;
        const bounds = instance.getBounds();
        const ids = await MapService.getLinePlotIds(
          bounds.getNorth(),
          bounds.getSouth(),
          bounds.getEast(),
          bounds.getWest(),
          instance.getZoom()
        );

        const missingIds = ids.filter((id) => !requestedIds.current.has(id));
        if (missingIds.length === 0) return;
        missingIds.forEach((id) => requestedIds.current.add(id));

        const results = (
          await Promise.all(
            missingIds.map(async (id): Promise<LoadedLinework | null> => {
              try {
                return { id, data: await MapService.getLinePlot(id) };
              } catch (error) {
                console.error(`Unable to load cave linework ${id}`, error);
                return null;
              }
            })
          )
        ).filter((result): result is LoadedLinework => result !== null);

        if (mounted.current) {
          setLinework((current) => [...current, ...results]);
        }
      } catch (error) {
        if (mounted.current) {
          console.error("Unable to load cave linework", error);
          message.error("Unable to load cave linework.");
        }
      }
    };

    void load();
  }, [map, viewportRevision]);

  return (
    <>
      {linework.map(({ id, data }) => (
        <Source key={id} id={`linework-${id}`} type="geojson" data={data} attribution={attribution}>
          <Layer
            id={`linework-${id}-fill`}
            type="fill"
            filter={["==", ["geometry-type"], "Polygon"]}
            paint={{ "fill-color": "#FF0000", "fill-opacity": 0.8, "fill-outline-color": "#B22222" }}
          />
          <Layer
            id={`linework-${id}-line`}
            type="line"
            filter={["==", ["geometry-type"], "LineString"]}
            paint={{ "line-color": "#00008B" }}
          />
          <Layer
            id={`linework-${id}-point`}
            type="circle"
            minzoom={16}
            filter={["==", ["geometry-type"], "Point"]}
            paint={{
              "circle-color": "#ff5722",
              "circle-radius": ["interpolate", ["linear"], ["zoom"], 16, 0.5, 18, 4, 20, 8],
              "circle-opacity": 0.8,
              "circle-stroke-color": "#0f1720",
              "circle-stroke-width": 0.5,
            }}
          />
        </Source>
      ))}
    </>
  );
};
