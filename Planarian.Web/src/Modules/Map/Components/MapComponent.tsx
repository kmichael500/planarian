import React, { useEffect, useState } from "react";
import { MapService } from "../Services/MapService";
import {
  Map,
  Source,
  Layer,
  GeolocateControl,
  NavigationControl,
  FillLayer,
} from "react-map-gl/maplibre";
import { StyleSpecification } from "@maplibre/maplibre-gl-style-spec";

// import "maplibre-gl/dist/maplibre-gl.css";

interface MapComponentProps {
  initialCenter?: [number, number];
  initialZoom?: number;
}

const MapComponent: React.FC<MapComponentProps> = ({
  initialCenter,
  initialZoom,
}) => {
  const [isLoading, setIsLoading] = useState(true);
  const [mapCenter, setMapCenter] = useState<[number, number]>(
    initialCenter || [0, 0]
  );
  const [zoom, setZoom] = useState(initialZoom || 7);

  const mapStyle = {
    // glyphs: "https://fonts.openmaptiles.org/{fontstack}/{range}.pbf", // Glyphs URL
    version: 8,
    sources: {
      "osm-tiles": {
        type: "raster",
        tiles: [
          "https://a.tile.openstreetmap.org/{z}/{x}/{y}.png",
          "https://b.tile.openstreetmap.org/{z}/{x}/{y}.png",
          "https://c.tile.openstreetmap.org/{z}/{x}/{y}.png",
        ],
        tileSize: 256,
      },
    },
    layers: [
      {
        id: "osm-tiles-layer",
        type: "raster",
        source: "osm-tiles",
        minzoom: 0,
        maxzoom: 19,
      },
    ],
  } as StyleSpecification;

  useEffect(() => {
    if (!initialCenter) {
      const fetchData = async () => {
        try {
          const data = await MapService.getMapCenter();
          console.log("Center", data);
          setMapCenter([data.latitude, data.longitude]);
          setIsLoading(false);
        } catch (error) {
          console.error("An error occurred while fetching data", error);
        } finally {
        }
      };
      fetchData();
    } else {
      setIsLoading(false);
    }
  }, [initialCenter]);

  return (
    <>
      {!isLoading && (
        <>
          <Map
            interactiveLayerIds={["entrances"]}
            initialViewState={{
              longitude: mapCenter[1],
              latitude: mapCenter[0],
              zoom: zoom,
            }}
            onClick={(event) => {
              const clickedFeatures = event.features;
              const clickedEntrance = clickedFeatures?.find(
                (feature) => feature.layer.id === "entrances"
              );
              if (clickedEntrance) {
                // Handle click on entrance point
                console.log("Entrance point clicked:", clickedEntrance);
              }
            }}
            // style={{ width: 600, height: 400 }}
            mapStyle={mapStyle}
          >
            <NavigationControl />
            <GeolocateControl />
            <Source
              id="entrances"
              type="vector"
              tiles={["https://localhost:7111/api/map/{z}/{x}/{y}.mvt"]}
            >
              <Layer
                source-layer="entrances"
                id="entrances"
                type="circle"
                paint={{
                  "circle-radius": [
                    "interpolate",
                    ["linear"],
                    ["zoom"],
                    10, // at zoom level 10
                    2, // circle radius is 5
                    15, // at zoom level 20
                    5, // circle radius is 20
                  ],
                  "circle-color": "#007cbf",
                }}
              ></Layer>
              {/* <Layer
                source-layer="entrances"
                id="entrance-labels"
                type="symbol"
                layout={{
                  "text-field": "{CaveId}", // Assuming 'name' is the property containing the name
                  "text-offset": [0, 1.5], // This offset will position the text above the point
                  "text-anchor": "top", // Anchor the text to the top of the point
                  "text-size": 12, // Adjust text size to your preference
                }}
                paint={{
                  "text-color": "#000", // Set text color to black
                }}
              /> */}
            </Source>
          </Map>
        </>
      )}
    </>
  );
};

export { MapComponent };
