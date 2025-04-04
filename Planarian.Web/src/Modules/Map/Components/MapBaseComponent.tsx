import React, { useContext, useEffect, useState } from "react";
import { MapService } from "../Services/MapService";
import {
  Map,
  Source,
  Layer,
  GeolocateControl,
  NavigationControl,
  MapLayerMouseEvent,
  MapProvider,
  ViewStateChangeEvent,
} from "react-map-gl/maplibre";
import { StyleSpecification } from "@maplibre/maplibre-gl-style-spec";
import { Spin } from "antd";
import { AppOptions } from "../../../Shared/Services/AppService";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { LayerControl } from "./LayerControl";
import { CaveSearchMapControl } from "./CaveSearchMapControl";
import { FullScreenControl } from "./FullScreenControl";
import { useNavigate } from "react-router-dom";
import { NavigationService } from "../../../Shared/Services/NavigationService";

interface MapBaseComponentProps {
  initialCenter?: [number, number];
  initialZoom?: number;
  onCaveClicked: (caveId: string) => void;
  onNonCaveClicked?: (lat: number, lng: number) => void;
  onMoveEnd?: ((e: ViewStateChangeEvent) => void) | undefined;
  showFullScreenControl?: boolean;
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
  onNonCaveClicked,
  onMoveEnd,
  showFullScreenControl = false,
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
  const [isOverControl, setIsOverControl] = useState(false);
  const mapRef = React.useRef<any>(null);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const data = await MapService.getMapCenter();
        setMapCenter([data.latitude, data.longitude]);
        setIsLoading(false);
      } catch (error) {
        console.error("An error occurred while fetching data", error);
      }
    };
    if (!initialCenter) {
      fetchData();
    } else {
      setIsLoading(false);
    }
  }, [initialCenter]);

  const handleMoveEnd = (e: ViewStateChangeEvent) => {
    const { latitude, longitude, zoom: currentZoom } = e.viewState;
    setMapCenter([latitude, longitude]);
    setZoom(currentZoom);
    if (onMoveEnd) {
      onMoveEnd(e);
    }
  };

  const navigate = useNavigate();

  const handleControlMouseEnter = () => {
    console.log("Mouse entered control");
    setIsOverControl(true);
  };

  const handleControlMouseLeave = () => {
    console.log("Mouse left control");
    setIsOverControl(false);
  };

  const handleMapClick = async (event: MapLayerMouseEvent) => {
    if (isOverControl) {
      return;
    }

    const clickedFeatures = event.features;
    const clickedEntrance = clickedFeatures?.find(
      (feature: any) => feature.layer.id === "entrances"
    );
    if (clickedEntrance) {
      if (clickedEntrance.properties?.CaveId) {
        onCaveClicked(clickedEntrance.properties?.CaveId);
      }
    } else {
      if (onNonCaveClicked) {
        const { lat, lng } = event.lngLat;
        onNonCaveClicked(lat, lng);
      }
    }
  };

  const zoomControlPosition = "top-left";
  const accountName = AuthenticationService.GetAccountName();
  return (
    <>
      <Spin spinning={isLoading}>
        {!isLoading && AppOptions.serverBaseUrl && hideBodyPadding && (
          <>
            <MapProvider>
              <Map
                ref={mapRef}
                maxPitch={85}
                reuseMaps
                antialias
                interactiveLayerIds={["entrances"]}
                initialViewState={{
                  longitude: mapCenter[1],
                  latitude: mapCenter[0],
                  zoom: zoom,
                }}
                onClick={handleMapClick}
                mapStyle={mapStyle}
                onMoveEnd={handleMoveEnd}
                id="map-container"
              >
                <div
                  id="cave-search-container"
                  onMouseEnter={handleControlMouseEnter}
                  onMouseLeave={handleControlMouseLeave}
                >
                  <CaveSearchMapControl />
                </div>

                <div
                  id="layer-control-container"
                  onMouseEnter={handleControlMouseEnter}
                  onMouseLeave={handleControlMouseLeave}
                >
                  <LayerControl />
                </div>

                <Source
                  id="entrances"
                  type="vector"
                  tiles={[
                    `${
                      AppOptions.serverBaseUrl
                    }/api/map/{z}/{x}/{y}.mvt?access_token=${AuthenticationService.GetToken()}&account_id=${AuthenticationService.GetAccountId()}&test=1`,
                  ]}
                  attribution={`© ${accountName}`}
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
                        ["get", "cavename"],
                        [
                          "case",
                          [
                            "all",
                            ["has", "Name"],
                            ["!=", ["get", "Name"], ""],
                            ["!=", ["get", "Name"], ["get", "cavename"]],
                          ],
                          ["concat", " (", ["get", "Name"], ")"],
                          "",
                        ],
                        ["case", ["get", "IsPrimary"], " *", ""],
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

                <div
                  id="navigation-controls"
                  onMouseEnter={handleControlMouseEnter}
                  onMouseLeave={handleControlMouseLeave}
                >
                  <NavigationControl position={zoomControlPosition} />
                  <GeolocateControl position={zoomControlPosition} />
                </div>

                {showFullScreenControl && (
                  <div
                    id="fullscreen-control"
                    onMouseEnter={handleControlMouseEnter}
                    onMouseLeave={handleControlMouseLeave}
                  >
                    <FullScreenControl
                      handleClick={() => {
                        const latitude = mapCenter[0];
                        const longitude = mapCenter[1];

                        NavigationService.NavigateToMap(
                          latitude,
                          longitude,
                          zoom,
                          navigate
                        );
                      }}
                    />
                  </div>
                )}
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
