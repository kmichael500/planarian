import React, { useContext, useEffect, useState, useMemo } from "react";
import {
  Map,
  Source,
  Layer,
  GeolocateControl,
  NavigationControl,
  Popup,
  MapLayerMouseEvent,
  MapProvider,
  ViewStateChangeEvent,
} from "react-map-gl/maplibre";
import { StyleSpecification } from "@maplibre/maplibre-gl-style-spec";
import { message, Spin } from "antd";
import { AppOptions } from "../../../Shared/Services/AppService";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { LayerControl } from "./LayerControl";
import { CaveSearchMapControl } from "./CaveSearchMapControl";
import { FullScreenControl } from "./FullScreenControl";
import { useNavigate } from "react-router-dom";
import { NavigationService } from "../../../Shared/Services/NavigationService";

import shpjs from "shpjs";
import bbox from "@turf/bbox";
import { FeatureCollection } from "geojson";
import { MapService } from "../Services/MapService";

interface MapBaseComponentProps {
  initialCenter?: [number, number];
  initialZoom?: number;
  onCaveClicked: (caveId: string) => void;
  onNonCaveClicked?: (lat: number, lng: number) => void;
  onMoveEnd?: ((e: ViewStateChangeEvent) => void) | undefined;
  showFullScreenControl?: boolean;
  showGeolocateControl?: boolean;
  showSearchBar?: boolean;
}

// MUST be defined outside the function or in useMemo
// otherwise the map might re-render unpredictably
const mapStyle: StyleSpecification = {
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
  showGeolocateControl = true,
  showSearchBar = true,
}) => {
  const { setHideBodyPadding, hideBodyPadding } = useContext(AppContext);
  useEffect(() => {
    setHideBodyPadding(true);
    return () => {
      setHideBodyPadding(false);
    };
  }, [setHideBodyPadding]);

  const [isLoading, setIsLoading] = useState(true);
  const [mapCenter, setMapCenter] = useState<[number, number]>(
    initialCenter || [0, 0]
  );
  const [zoom, setZoom] = useState(initialZoom || 7);
  const [isOverControl, setIsOverControl] = useState(false);
  const mapRef = React.useRef<any>(null);

  const [dragActive, setDragActive] = useState(false);
  const [uploadedShapeFiles, setUploadedShapeFiles] = useState<
    {
      id: string;
      data: FeatureCollection;
    }[]
  >([]);

  const [popupInfo, setPopupInfo] = useState<{
    lngLat: [number, number];
    properties: any;
  } | null>(null);

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
    setIsOverControl(true);
  };

  const handleControlMouseLeave = () => {
    setIsOverControl(false);
  };

  const handleMapClick = (event: MapLayerMouseEvent) => {
    if (isOverControl) return;

    const clickedFeatures = event.features;
    if (clickedFeatures && clickedFeatures.length > 0) {
      // Check for an "entrances" feature click.
      const clickedEntrance = clickedFeatures.find(
        (feature: any) => feature.layer.id === "entrances"
      );
      if (clickedEntrance && clickedEntrance.properties?.CaveId) {
        onCaveClicked(clickedEntrance.properties?.CaveId);
        // Clear any existing popup.
        setPopupInfo(null);
        return;
      }

      // Check if an uploaded GeoJSON feature was clicked.
      const geojsonFeature = clickedFeatures.find((feature: any) => {
        return uploadedShapeFiles.some(
          (file) => feature.layer.id === `${file.id}-layer`
        );
      });
      if (geojsonFeature) {
        setPopupInfo({
          lngLat: [event.lngLat.lng, event.lngLat.lat],
          properties: geojsonFeature.properties,
        });
        return;
      }
    }

    // Fallback for non-cave clicks.
    if (onNonCaveClicked) {
      const { lat, lng } = event.lngLat;
      onNonCaveClicked(lat, lng);
    }
    // Clear popup if no feature is found.
    setPopupInfo(null);
  };

  const zoomControlPosition = "top-left";
  const accountName = AuthenticationService.GetAccountName();

  const fullScreenControlPosition = {
    top: "110px",
    left: "10px",
  };
  const layerControlPosition = {
    top: showSearchBar ? "50px" : "0px",
    right: "0",
  };

  const onDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    if (!dragActive) {
      setDragActive(true);
    }
  };

  const onDragLeave = () => {
    setDragActive(false);
  };

  const onDrop = async (e: React.DragEvent) => {
    e.preventDefault();
    setDragActive(false);

    const file = e.dataTransfer.files?.[0];
    if (!file) return;

    try {
      const arrayBuffer = await file.arrayBuffer();
      const parsed = await shpjs(arrayBuffer);

      // Handle possibility of multiple shapefile layers.
      const asArray = Array.isArray(parsed) ? parsed : [parsed];

      // Add the new layers.
      const newLayers = asArray.map((fc, i) => ({
        id: `${file.name}-${i}`,
        data: fc as FeatureCollection,
      }));

      console.log("Parsed shapefile data:", newLayers);
      setUploadedShapeFiles((prev) => [...prev, ...newLayers]);

      if (newLayers[0]?.data) {
        const bounds = bbox(newLayers[0].data); // [minX, minY, maxX, maxY]
        if (mapRef.current) {
          mapRef.current.fitBounds(
            [
              [bounds[0], bounds[1]],
              [bounds[2], bounds[3]],
            ],
            {
              padding: 40,
              duration: 1000,
            }
          );
        }
      }
    } catch (err) {
      message.error(
        "Error parsing shapefile. Please ensure it is a valid zipped shapefile that contains .shp, and .dbf files."
      );
      console.log(err);
    }
  };

  // Compute dynamic interactive layer IDs including uploaded GeoJSON layers.
  const uploadedLayerIds = useMemo(
    () => uploadedShapeFiles.map(({ id }) => `${id}-layer`),
    [uploadedShapeFiles]
  );
  const interactiveLayerIds = useMemo(
    () => ["entrances", ...uploadedLayerIds],
    [uploadedLayerIds]
  );

  return (
    <Spin spinning={isLoading}>
      {!isLoading && AppOptions.serverBaseUrl && hideBodyPadding && (
        <div
          style={{ position: "relative", width: "100%", height: "100%" }}
          onDragOver={onDragOver}
          onDragLeave={onDragLeave}
          onDrop={onDrop}
        >
          {dragActive && (
            <div
              style={{
                position: "absolute",
                inset: 0,
                background: "rgba(0, 120, 255, 0.15)",
                border: "3px dashed #0078ff",
                zIndex: 9999,
                pointerEvents: "none",
              }}
            >
              <p
                style={{
                  position: "absolute",
                  top: "50%",
                  left: "50%",
                  transform: "translate(-50%,-50%)",
                  fontSize: "1.3rem",
                  fontWeight: "bold",
                  color: "#0078ff",
                  textShadow: "1px 1px 2px #fff",
                }}
              >
                Drop your Shapefile (zip) here!
              </p>
            </div>
          )}

          <MapProvider>
            <Map
              ref={mapRef}
              maxPitch={85}
              reuseMaps
              antialias
              interactiveLayerIds={interactiveLayerIds}
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
              {showSearchBar && (
                <div
                  id="cave-search-container"
                  onMouseEnter={handleControlMouseEnter}
                  onMouseLeave={handleControlMouseLeave}
                >
                  <CaveSearchMapControl />
                </div>
              )}

              <div
                id="layer-control-container"
                onMouseEnter={handleControlMouseEnter}
                onMouseLeave={handleControlMouseLeave}
              >
                <LayerControl position={layerControlPosition} />
              </div>

              {/* Entrances layer */}
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
                    "circle-color": [
                      "case",
                      // If IsFavorite is true, return gold, else blue
                      ["boolean", ["get", "IsFavorite"], false],
                      "#FFC107", // Gold for favorites
                      "#00008B", // Default color for non-favorites
                    ],
                    "circle-stroke-color": [
                      "step",
                      ["zoom"],
                      "rgba(0, 0, 0, 0)", // Transparent at zoom levels below 9
                      9,
                      "#000", // Black stroke for zoom levels 9 and above
                    ],
                    "circle-stroke-width": [
                      "step",
                      ["zoom"],
                      0, // Stroke width 0 below zoom level 9
                      9,
                      1, // Stroke width 1 at zoom level 9 and above
                    ],
                  }}
                />

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

              {/* Uploaded Shapefile layers */}
              {uploadedShapeFiles.map(({ id, data }) => {
                const firstFeature = data.features[0];
                const geomType =
                  firstFeature?.geometry?.type?.replace("Multi", "") ??
                  "Polygon";

                let paint: any;
                let layerType: "fill" | "line" | "circle";

                switch (geomType) {
                  case "Point":
                    layerType = "circle";
                    paint = {
                      "circle-color": "#ff5722",
                      "circle-radius": [
                        "interpolate",
                        ["linear"],
                        ["zoom"],
                        16,
                        0.5, // At zoom level 16, radius is 0.5
                        18,
                        4, // At zoom level 18, radius is 4
                        20,
                        8, // At zoom level 20, radius is 8
                      ],
                      "circle-opacity": [
                        "step",
                        ["zoom"],
                        0, // Invisible below zoom level 16
                        16,
                        0.8, // Visible at 80% opacity at zoom level 16+
                      ],
                      "circle-stroke-color": "#fff",
                      "circle-stroke-width": [
                        "step",
                        ["zoom"],
                        0, // No stroke below zoom level 16
                        16,
                        0.5, // 0.5px stroke at zoom level 16+
                      ],
                    };
                    break;
                  case "LineString":
                    layerType = "line";
                    paint = {
                      "line-color": "#00008B",
                    };
                    break;
                  default:
                    layerType = "fill";
                    paint = {
                      "fill-color": "#FF0000",
                      "fill-opacity": 0.8,
                      "fill-outline-color": "#B22222",
                    };
                    break;
                }

                return (
                  <Source key={id} id={id} type="geojson" data={data}>
                    <Layer id={`${id}-layer`} type={layerType} paint={paint} />
                  </Source>
                );
              })}

              {/* Popup for displaying GeoJSON feature properties */}
              {popupInfo && (
                <Popup
                  longitude={popupInfo.lngLat[0]}
                  latitude={popupInfo.lngLat[1]}
                  onClose={() => setPopupInfo(null)}
                  closeOnClick={false}
                  anchor="top"
                >
                  <div>
                    <strong>Properties:</strong>
                    <pre
                      style={{
                        whiteSpace: "pre-wrap",
                        wordWrap: "break-word",
                        maxHeight: "200px",
                        overflow: "auto",
                      }}
                    >
                      {JSON.stringify(popupInfo.properties, null, 2)}
                    </pre>
                  </div>
                </Popup>
              )}

              <div
                id="navigation-controls"
                onMouseEnter={handleControlMouseEnter}
                onMouseLeave={handleControlMouseLeave}
              >
                <NavigationControl position={zoomControlPosition} />
                {showGeolocateControl && (
                  <GeolocateControl
                    position={zoomControlPosition}
                    positionOptions={{ enableHighAccuracy: true }}
                    trackUserLocation={true}
                  />
                )}
              </div>

              {showFullScreenControl && (
                <div
                  id="fullscreen-control"
                  onMouseEnter={handleControlMouseEnter}
                  onMouseLeave={handleControlMouseLeave}
                >
                  <FullScreenControl
                    position={fullScreenControlPosition}
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
        </div>
      )}
    </Spin>
  );
};

const MapBaseMemo = React.memo(MapBaseComponent);
export { MapBaseMemo as MapBaseComponent };
