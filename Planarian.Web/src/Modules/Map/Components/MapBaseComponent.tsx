import React, { useContext, useEffect, useMemo, useState } from "react";
import { MapService } from "../Services/MapService";
import {
  Map,
  Source,
  Layer,
  GeolocateControl,
  NavigationControl,
  MapLayerMouseEvent,
  MapProvider,
} from "react-map-gl/maplibre";
import { StyleSpecification } from "@maplibre/maplibre-gl-style-spec";
import { Spin } from "antd";
import { AppOptions } from "../../../Shared/Services/AppService";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { LayerControl } from "./LayerControl";

interface MapBaseComponentProps {
  initialCenter?: [number, number];
  initialZoom?: number;
  onCaveClicked: (caveId: string) => void;
}

// MUST be defined outside the function or in useMemo (still bugs with this tho on mobile?) otherwise the map re-renders every time the you click on it
const mapStyle = {
  glyphs:
    "https://api.mapbox.com/fonts/v1/mapbox/{fontstack}/{range}.pbf?access_token=pk.eyJ1IjoibWljaGFlbGtldHpuZXIiLCJhIjoiY2xvODF0M2ZiMDloNTJpbzYzdXRrYWhrcSJ9.B8x4P8SK9Zpe-sdN6pJ3Eg",
  version: 8,

  sources: {},
  layers: [],
} as StyleSpecification;

const MapBaseComponent: React.FC<MapBaseComponentProps> = ({
  initialCenter,
  initialZoom,
  onCaveClicked,
}) => {
  const { setHideBodyPadding, hideBodyPadding } = useContext(AppContext);
  useEffect(() => {
    setHideBodyPadding(true);
    return () => {
      setHideBodyPadding(false);
    };
  }, []);
  const [isLoading, setIsLoading] = useState(true);
  const [mapCenter, setMapCenter] = useState<[number, number]>(
    initialCenter || [0, 0]
  );
  const [zoom, setZoom] = useState(initialZoom || 7);

  useEffect(() => {
    if (!initialCenter) {
      const fetchData = async () => {
        try {
          const data = await MapService.getMapCenter();
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

  const handleMapClick = async (event: MapLayerMouseEvent) => {
    const clickedFeatures = event.features;
    const clickedEntrance = clickedFeatures?.find(
      (feature: any) => feature.layer.id === "entrances"
    );
    if (clickedEntrance) {
      if (clickedEntrance.properties?.CaveId) {
        onCaveClicked(clickedEntrance.properties?.CaveId);
      }
    }
  };

  const zoomControlPosition = "top-left";
  return (
    <>
      <Spin spinning={isLoading}>
        {!isLoading && AppOptions.serverBaseUrl && hideBodyPadding && (
          <>
            <MapProvider>
              <Map
                reuseMaps
                interactiveLayerIds={["entrances"]}
                initialViewState={{
                  longitude: mapCenter[1],
                  latitude: mapCenter[0],
                  zoom: zoom,
                }}
                onClick={handleMapClick}
                mapStyle={mapStyle}
                // terrain={{ exaggeration: 1.5, source: "terrainLayer" }}
              >
                <LayerControl />
                <Source
                  id="entrances"
                  type="vector"
                  tiles={[
                    `${
                      AppOptions.serverBaseUrl
                    }/api/map/{z}/{x}/{y}.mvt?access_token=${AuthenticationService.GetToken()}&account_id=${AuthenticationService.GetAccountId()}&test=1`,
                  ]}
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
                        5,
                        2, // At zoom level 5, radius is 2
                        10,
                        4, // At zoom level 10, radius is 4
                        15,
                        8, // At zoom level 15, radius is 8
                      ],
                      "circle-opacity": [
                        "step",
                        ["zoom"],
                        0, // invisible at zoom levels < 13
                        9,
                        0.6, // visible at zoom levels >= 13
                      ],

                      // "circle-color": "#161616",
                    }}
                  ></Layer>

                  <Layer
                    source-layer="entrances"
                    id="entrances-heatmap"
                    type="heatmap"
                    paint={{
                      "heatmap-radius": [
                        "interpolate",
                        ["linear"],
                        ["zoom"],
                        0,
                        0.000001, // At zoom level 0, radius is 1
                        22,
                        30, // At zoom level 22, radius is 30
                      ],
                      "heatmap-opacity": [
                        "step",
                        ["zoom"],
                        0.6,
                        9,
                        0, // invisible at zoom levels > 9
                      ],
                    }}
                  />

                  <Layer
                    source-layer="entrances"
                    id="entrance-labels"
                    type="symbol"
                    layout={{
                      "text-font": ["Open Sans Regular"],
                      "text-field": [
                        "concat",
                        ["get", "cavename"], // Always display 'cavename'
                        [
                          "case",
                          ["all", ["has", "Name"], ["!=", ["get", "Name"], ""]],
                          ["concat", " (", ["get", "Name"], ")"], // Only add ' (Name)' if 'Name' property exists and is not an empty string
                          "",
                        ],
                        [
                          "case",
                          ["get", "IsPrimary"],
                          "*", // Add a star symbol if 'IsPrimary' is true
                          "", // Do nothing if 'IsPrimary' is false
                        ],
                      ],

                      "text-size": [
                        "interpolate",
                        ["linear"],
                        ["zoom"],
                        5,
                        0, // At zoom level 5, text size is 0 (effectively hiding the text)
                        10,
                        8, // At zoom level 10, text size is 8
                        15,
                        12, // At zoom level 15, text size is 12
                      ],
                      "text-offset": [0, 1.5], // This offset will position the text above the point
                      "text-anchor": "top", // Anchor the text to the top of the point
                      "text-allow-overlap": false, // This will prevent text from overlapping, reducing clutter
                    }}
                    paint={{
                      "text-color": "#000", // Set text color to black
                      "text-opacity": [
                        "step", // Change interpolation type to 'step'
                        ["zoom"],
                        0, // Default opacity is 0
                        9,
                        0.8, // At zoom level 9, text opacity is 0.8
                        15,
                        1, // At zoom level 15, text opacity is 1
                      ],
                    }}
                  />
                </Source>

                <NavigationControl position={zoomControlPosition} />
                <GeolocateControl position={zoomControlPosition} />
              </Map>
            </MapProvider>
          </>
        )}
      </Spin>
    </>
  );
};

const MapBaseMemo = React.memo(MapBaseComponent);

export { MapBaseMemo as MapBaseComponent };
