import React, {
  useContext,
  useEffect,
  useState,
  useMemo,
  useCallback,
} from "react";
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
  ScaleControl, // Import ScaleControl
} from "react-map-gl/maplibre";
import type { MapProps } from "react-map-gl/maplibre";
import { StyleSpecification } from "@maplibre/maplibre-gl-style-spec";
import { message, Spin, Form } from "antd";
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
import type { FitBoundsOptions, LngLatBoundsLike } from "maplibre-gl";
import { AdvancedSearchInlineControlsContext } from "../../Search/Components/AdvancedSearchDrawerComponent";
import { QueryBuilder } from "../../Search/Services/QueryBuilder";
import { CaveSearchParamsVm } from "../../Caves/Models/CaveSearchParamsVm";
import { NestedKeyOf } from "../../../Shared/Helpers/StringHelpers";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import styled from "styled-components";
import { SlidersOutlined, ClearOutlined } from "@ant-design/icons";
import { CaveAdvancedSearchDrawer } from "../../Caves/Components/CaveAdvancedSearchDrawer";
import {
  applyEntranceLocationFilterToQuery,
  EntranceLocationFilter,
  parseEntranceLocationFilter,
} from "../../Search/Helpers/EntranceLocationFilterHelpers";

interface MapBaseComponentProps {
  initialCenter?: [number, number];
  initialZoom?: number;
  initialBounds?: LngLatBoundsLike;
  initialFitBoundsOptions?: FitBoundsOptions;
  onCaveClicked: (caveId: string) => void;
  onNonCaveClicked?: (lat: number, lng: number) => void;
  onMoveEnd?: ((e: ViewStateChangeEvent) => void) | undefined;
  showFullScreenControl?: boolean;
  showGeolocateControl?: boolean;
  showSearchBar?: boolean;
  onShapefileUploaded?: (data: FeatureCollection[]) => void;
  children?: React.ReactNode;
  manageBodyPadding?: boolean;
  additionalInteractiveLayerIds?: string[];
  reuseMaps?: boolean;
}

// MUST be defined outside the function (or via useMemo)
// otherwise the map might re-render unpredictably
const mapStyle: StyleSpecification = {
  glyphs:
    "https://api.mapbox.com/fonts/v1/mapbox/{fontstack}/{range}.pbf?access_token=pk.eyJ1IjoibWljaGFlbGtldHpuZXIiLCJhIjoiY2xvODF0M2ZiMDloNTJpbzYzdXRrYWhrcSJ9.B8x4P8SK9Zpe-sdN6pJ3Eg",
  version: 8,
  sources: {},
  layers: [],
} as StyleSpecification;

// Helper functions to convert tile coordinates to longitude/latitude.
// These functions use the standard Web Mercator tile scheme.
const tile2lon = (x: number, zoom: number): number => {
  return (x / Math.pow(2, zoom)) * 360 - 180;
};

const tile2lat = (y: number, zoom: number): number => {
  const n = Math.PI - (2 * Math.PI * y) / Math.pow(2, zoom);
  return (180 / Math.PI) * Math.atan(0.5 * (Math.exp(n) - Math.exp(-n)));
};

const hashString = (input: string): string => {
  let hash = 0;
  for (let i = 0; i < input.length; i++) {
    hash = (hash << 5) - hash + input.charCodeAt(i);
    hash |= 0;
  }
  return `h${Math.abs(hash)}`;
};

const offsetPositionValue = (value: string | undefined, offset: number) => {
  if (!value) {
    return `${offset}px`;
  }

  const trimmed = value.trim();

  if (trimmed.endsWith("px")) {
    const numeric = Number.parseFloat(trimmed);
    if (!Number.isNaN(numeric)) {
      return `${numeric + offset}px`;
    }
  }

  return `calc(${value} + ${offset}px)`;
};

const mapControlContainerIds = {
  caveSearch: "cave-search-container",
  layerControl: "layer-control-container",
  advancedSearch: "advanced-search-control",
  navigationControls: "navigation-controls",
  fullScreenControl: "fullscreen-control",
} as const;

const mapControlSelector = Object.values(mapControlContainerIds)
  .map((id) => `#${id}`)
  .join(", ");

const FloatingPanel = styled.div`
  position: absolute;
  background: #fff;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.3);
  padding: 8px;
  margin: 20px;
  border-radius: 8px;
  transition: all 0.3s ease;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
`;

const MapBaseComponent: React.FC<MapBaseComponentProps> = ({
  initialCenter,
  initialZoom,
  initialBounds,
  initialFitBoundsOptions,
  onCaveClicked,
  onNonCaveClicked,
  onMoveEnd,
  showFullScreenControl = false,
  showGeolocateControl = true,
  showSearchBar = true,
  onShapefileUploaded,
  children,
  manageBodyPadding = true,
  additionalInteractiveLayerIds = [],
  reuseMaps = true,
}) => {
  const { setHideBodyPadding, hideBodyPadding } = useContext(AppContext);
  useEffect(() => {
    if (!manageBodyPadding) {
      return;
    }
    setHideBodyPadding(true);
    return () => {
      setHideBodyPadding(false);
    };
  }, [setHideBodyPadding, manageBodyPadding]);

  const [isLoading, setIsLoading] = useState(true);
  const [mapCenter, setMapCenter] = useState<[number, number]>(
    initialCenter || [0, 0]
  );
  const [zoom, setZoom] = useState(initialZoom || 7);
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

  const [mapFiltersQueryString, setMapFiltersQueryString] =
    useState<string>("");
  const [mapFiltersVersion, setMapFiltersVersion] = useState(0);

  const [lineplotsData, setLineplotsData] = useState<
    {
      id: string;
      data: FeatureCollection;
      type: string;
    }[]
  >([]);

  const queryBuilder = useMemo(
    () =>
      new QueryBuilder<CaveSearchParamsVm>(
        window.location.search.substring(1),
        false
      ),
    []
  );
  const [form] = Form.useForm<CaveSearchParamsVm>();
  const [filterClearSignal, setFilterClearSignal] = useState(0);
  const [polygonResetSignal, setPolygonResetSignal] = useState(0);
  const [entranceLocationFilter, setEntranceLocationFilter] =
    useState<EntranceLocationFilter>(() =>
      parseEntranceLocationFilter(
        queryBuilder.getFieldValue("entranceLocation") as string | undefined
      )
    );
  const [isFetchingEntranceLocation, setIsFetchingEntranceLocation] =
    useState(false);
  const [hasAppliedFilters, setHasAppliedFilters] = useState(
    queryBuilder.hasFilters()
  );

  useEffect(() => {
    if (queryBuilder.hasFilters()) {
      const initialQuery = queryBuilder.buildAsQueryString();
      setHasAppliedFilters(true);
      setMapFiltersQueryString(initialQuery);
      setMapFiltersVersion((version) => version + 1);
    } else {
      setMapFiltersQueryString("");
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const applyEntranceLocationFilter = useCallback(
    (next: EntranceLocationFilter) => {
      setEntranceLocationFilter(next);
      applyEntranceLocationFilterToQuery(
        queryBuilder,
        "entranceLocation" as NestedKeyOf<CaveSearchParamsVm>,
        next
      );
    },
    [queryBuilder]
  );

  const handleEntranceLocationChange = useCallback(
    (field: keyof EntranceLocationFilter, rawValue: number | string | null) => {
      let parsedValue: number | undefined;

      if (rawValue === null || rawValue === undefined || rawValue === "") {
        parsedValue = undefined;
      } else {
        const numericValue = Number(rawValue);
        parsedValue = Number.isFinite(numericValue) ? numericValue : undefined;
      }

      applyEntranceLocationFilter({
        ...entranceLocationFilter,
        [field]: parsedValue,
      });
    },
    [applyEntranceLocationFilter, entranceLocationFilter]
  );

  const syncEntranceLocationState = useCallback(() => {
    const currentValue = queryBuilder.getFieldValue(
      "entranceLocation"
    ) as string | undefined;
    setEntranceLocationFilter(parseEntranceLocationFilter(currentValue));
  }, [queryBuilder]);

  const handleUseCurrentLocation = useCallback(() => {
    if (!navigator?.geolocation) {
      message.error("Geolocation is not supported in this browser.");
      return;
    }

    setIsFetchingEntranceLocation(true);

    navigator.geolocation.getCurrentPosition(
      (position) => {
        const { latitude, longitude } = position.coords;
        const radius = entranceLocationFilter.radius ?? 5;

        applyEntranceLocationFilter({
          latitude,
          longitude,
          radius,
        });
        setIsFetchingEntranceLocation(false);
      },
      (error) => {
        message.error(error.message || "Unable to fetch current location.");
        setIsFetchingEntranceLocation(false);
      },
      {
        enableHighAccuracy: true,
        timeout: 15000,
        maximumAge: 0,
      }
    );
  }, [applyEntranceLocationFilter, entranceLocationFilter.radius]);

  const handleFiltersCleared = useCallback(() => {
    applyEntranceLocationFilter({});
    setFilterClearSignal((previous) => previous + 1);
    setPolygonResetSignal((previous) => previous + 1);
  }, [applyEntranceLocationFilter]);

  const runSearch = useCallback(async () => {
    syncEntranceLocationState();
    const hasFilters = queryBuilder.hasFilters();
    const queryString = hasFilters ? queryBuilder.buildAsQueryString() : "";
    const previousFilterQuery = mapFiltersQueryString;

    const urlSearchParams = new URLSearchParams(window.location.search);
    if (previousFilterQuery) {
      const previousParams = new URLSearchParams(previousFilterQuery);
      previousParams.forEach((_, key) => {
        urlSearchParams.delete(key);
      });
    }

    if (hasFilters) {
      const nextParams = new URLSearchParams(queryString);
      nextParams.forEach((value, key) => {
        urlSearchParams.set(key, value);
      });
    }

    const updatedSearch = urlSearchParams.toString();
    const nextUrl = `${window.location.pathname}${updatedSearch ? `?${updatedSearch}` : ""
      }`;
    window.history.replaceState({}, "", nextUrl);

    setHasAppliedFilters(hasFilters);
    setMapFiltersQueryString((previous) => {
      if (previous === queryString) {
        return previous;
      }
      setMapFiltersVersion((version) => version + 1);
      return queryString;
    });
  }, [
    mapFiltersQueryString,
    queryBuilder,
    syncEntranceLocationState,
  ]);

  useEffect(() => {
    if (initialCenter) {
      setMapCenter(initialCenter);
    }
  }, [initialCenter]);

  useEffect(() => {
    if (typeof initialZoom === "number") {
      setZoom(initialZoom);
    }
  }, [initialZoom]);

  const memoizedFitBoundsOptions = useMemo(() => {
    if (!initialBounds) {
      return undefined;
    }

    return {
      padding: 20,
      ...initialFitBoundsOptions,
    };
  }, [initialBounds, initialFitBoundsOptions]);

  const initialViewState = useMemo(() => {
    if (initialBounds) {
      return {
        bounds: initialBounds,
        fitBoundsOptions: memoizedFitBoundsOptions,
      };
    }

    return {
      longitude: mapCenter[1],
      latitude: mapCenter[0],
      zoom: zoom,
    };
  }, [initialBounds, memoizedFitBoundsOptions, mapCenter, zoom]) as MapProps["initialViewState"];

  const mapComponentKey = useMemo(() => {
    if (initialBounds) {
      return `bounds-${JSON.stringify(initialBounds)}`;
    }
    if (initialCenter) {
      const centerString = initialCenter.join(",");
      return `center-${centerString}-${initialZoom ?? "auto"}`;
    }
    return "default-map";
  }, [initialBounds, initialCenter, initialZoom]);

  // Get the map center using the MapService if no initialCenter was provided.
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

  // Instead of relying on the current zoom for caching, we fix the caching zoom to 12.
  // This way the same geographic area yields the same tile keys regardless of the actual zoom.
  const cachingZoom = 12;
  // Track which fixed-level tiles have been fetched.
  const fetchedTilesRef = React.useRef<Set<string>>(new Set());
  // Cache the fetched lineplots data.
  const cachedLineplotsDataRef = React.useRef<
    { id: string; data: FeatureCollection; type: string }[]
  >([]);

  const loadedPlotIds = React.useRef<Set<string>>(new Set());

  const fetchLineplots = async () => {
    if (zoom < 11 || !mapRef.current) return;

    const mapInstance = mapRef.current.getMap();
    const bounds = mapInstance.getBounds();
    const north = bounds.getNorth();
    const south = bounds.getSouth();
    const east = bounds.getEast();
    const west = bounds.getWest();

    // Compute fixed‑zoom tile indices
    const n = Math.pow(2, cachingZoom);
    const minTileX = Math.floor(((west + 180) / 360) * n);
    const maxTileX = Math.floor(((east + 180) / 360) * n);
    const minTileY = Math.floor(
      ((1 -
        Math.log(
          Math.tan((north * Math.PI) / 180) +
          1 / Math.cos((north * Math.PI) / 180)
        ) /
        Math.PI) /
        2) *
      n
    );
    const maxTileY = Math.floor(
      ((1 -
        Math.log(
          Math.tan((south * Math.PI) / 180) +
          1 / Math.cos((south * Math.PI) / 180)
        ) /
        Math.PI) /
        2) *
      n
    );

    // Find tiles we haven’t fetched yet
    const missingTiles: { x: number; y: number; tileId: string }[] = [];
    for (let x = minTileX; x <= maxTileX; x++) {
      for (let y = minTileY; y <= maxTileY; y++) {
        const tileId = `${cachingZoom}/${x}/${y}`;
        if (!fetchedTilesRef.current.has(tileId)) {
          missingTiles.push({ x, y, tileId });
        }
      }
    }

    if (missingTiles.length === 0) {
      return;
    }

    // For each missing tile, fetch just its IDs then its GeoJSONs
    for (const { x, y, tileId } of missingTiles) {
      // Tile bbox
      const tileWest = tile2lon(x, cachingZoom);
      const tileEast = tile2lon(x + 1, cachingZoom);
      const tileNorth = tile2lat(y, cachingZoom);
      const tileSouth = tile2lat(y + 1, cachingZoom);

      let ids: string[] = [];
      try {
        ids = await MapService.getLinePlotIds(
          tileNorth,
          tileSouth,
          tileEast,
          tileWest,
          zoom
        );
      } catch (err) {
        console.error(`Error fetching IDs for tile ${tileId}`, err);
        // still mark tile so we don’t retry immediately
        fetchedTilesRef.current.add(tileId);
        continue;
      }

      for (const id of ids) {
        if (loadedPlotIds.current.has(id)) continue;
        try {
          const featureCollection: FeatureCollection =
            await MapService.getLinePlot(id);

          const geometryType =
            featureCollection.features[0]?.geometry?.type.replace(
              "Multi",
              ""
            ) || "LineString";

          setLineplotsData((prev) => [
            ...prev,
            { id, data: featureCollection, type: geometryType },
          ]);
        } catch (err) {
          console.error(`Lineplot ${id} failed to load`, err);
        } finally {
          loadedPlotIds.current.add(id);
        }
      }

      fetchedTilesRef.current.add(tileId);
    }
  };

  useEffect(() => {
    fetchLineplots();
  }, [zoom, mapCenter]);

  const handleMoveEnd = (e: ViewStateChangeEvent) => {
    const { latitude, longitude, zoom: currentZoom } = e.viewState;
    setMapCenter([latitude, longitude]);
    setZoom(currentZoom);
    if (onMoveEnd) {
      onMoveEnd(e);
    }
  };

  const navigate = useNavigate();

  const handleMapClick = (event: MapLayerMouseEvent) => {
    const clickTarget = event.originalEvent?.target as HTMLElement | null;
    if (
      clickTarget?.closest(
        mapControlSelector
      )
    ) {
      return;
    }

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

      // Check if a lineplot feature was clicked
      const lineplotFeature = clickedFeatures.find((feature: any) => {
        return lineplotsData.some(
          (plot) => feature.layer.id === `${plot.id}-layer`
        );
      });
      if (lineplotFeature) {
        setPopupInfo({
          lngLat: [event.lngLat.lng, event.lngLat.lat],
          properties: lineplotFeature.properties,
        });
        return;
      }

      if (additionalInteractiveLayerIds.length > 0) {
        const customFeature = clickedFeatures.find((feature: any) =>
          additionalInteractiveLayerIds.includes(feature.layer.id)
        );
        if (customFeature) {
          setPopupInfo({
            lngLat: [event.lngLat.lng, event.lngLat.lat],
            properties: customFeature.properties,
          });
          return;
        }
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
  const scaleControlPosition = "bottom-left"; // Define position for scale control
  const accountName = AuthenticationService.GetAccountName();

  const entrancesTileParams = useMemo(() => {
    const params = new URLSearchParams();
    const token = AuthenticationService.GetToken();
    const accountId = AuthenticationService.GetAccountId();

    if (token) {
      params.set("access_token", token);
    }
    if (accountId) {
      params.set("account_id", accountId);
    }

    if (mapFiltersQueryString) {
      const filterParams = new URLSearchParams(mapFiltersQueryString);
      filterParams.forEach((value, key) => {
        params.set(key, value);
      });
    }
    return params.toString();
  }, [mapFiltersQueryString]);

  const entranceTiles = useMemo(() => {
    if (!AppOptions.serverBaseUrl) {
      return [];
    }
    return [
      `${AppOptions.serverBaseUrl}/api/map/{z}/{x}/{y}.mvt?${entrancesTileParams}`,
    ];
  }, [entrancesTileParams]);

  const entrancesSourceKey = useMemo(
    () => hashString(`${mapFiltersVersion}-${mapFiltersQueryString}`),
    [mapFiltersQueryString, mapFiltersVersion]
  );

  const fullScreenControlPosition = {
    top: "110px",
    left: "10px",
  };
  const layerControlPosition = {
    top: showSearchBar ? "50px" : "0px",
    right: "0",
  };

  const advancedSearchPosition = useMemo(
    () => ({
      top: offsetPositionValue(layerControlPosition.top, 50),
      right: layerControlPosition.right ?? "0",
    }),
    [layerControlPosition]
  );

  const renderAdvancedSearchControls = useCallback(
    (
      context: AdvancedSearchInlineControlsContext<CaveSearchParamsVm>
    ) => (
      <FloatingPanel style={{ zIndex: 100, ...advancedSearchPosition }}>
        <PlanarianButton
          icon={<SlidersOutlined />}
          onClick={() => context.openDrawer()}
          tooltip="Advanced search"
          neverShowChildren
          type={hasAppliedFilters ? "primary" : "default"}
        />
        {hasAppliedFilters && (
          <PlanarianButton
            icon={<ClearOutlined />}
            onClick={(event) => {
              event.preventDefault();
              void context.clearFilters();
            }}
            tooltip="Clear filters"
            neverShowChildren
          />
        )}
      </FloatingPanel>
    ),
    [advancedSearchPosition, hasAppliedFilters]
  );

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

      if (onShapefileUploaded) {
        onShapefileUploaded(newLayers.map((layer) => layer.data));
      }
    } catch (err) {
      message.error(
        "Error parsing shapefile. Please ensure it is a valid zipped shapefile that contains .shp, and .dbf files."
      );
      console.error(err);
    }
  };

  // Compute dynamic interactive layer IDs including uploaded GeoJSON layers.
  const uploadedLayerIds = useMemo(
    () => uploadedShapeFiles.map(({ id }) => `${id}-layer`),
    [uploadedShapeFiles]
  );
  const lineplotLayerIds = useMemo(
    () => lineplotsData.map(({ id }) => `${id}-layer`),
    [lineplotsData]
  );
  const interactiveLayerIds = useMemo(
    () => [
      "entrances",
      ...uploadedLayerIds,
      ...lineplotLayerIds,
      ...additionalInteractiveLayerIds,
    ],
    [uploadedLayerIds, lineplotLayerIds, additionalInteractiveLayerIds]
  );

  // Sort lineplot data by geometry type for proper layering
  const sortedLineplotsData = useMemo(() => {
    // Create a copy of the data to avoid mutating state
    const sorted = [...lineplotsData];

    // Define order of geometry types (polygons first, then lines, then points)
    const typeOrder = { Polygon: 0, LineString: 1, Point: 2 };

    // Sort by geometry type
    return sorted.sort((a, b) => {
      return (
        (typeOrder[a.type as keyof typeof typeOrder] || 0) -
        (typeOrder[b.type as keyof typeof typeOrder] || 0)
      );
    });
  }, [lineplotsData]);

  const bodyPaddingReady = manageBodyPadding ? hideBodyPadding : true;

  return (
    <Spin spinning={isLoading}>
      {!isLoading && AppOptions.serverBaseUrl && bodyPaddingReady && (
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
              key={mapComponentKey}
              ref={mapRef}
              maxPitch={85}
              reuseMaps={reuseMaps}
              antialias
              interactiveLayerIds={interactiveLayerIds}
              initialViewState={initialViewState}
              onLoad={() => {
                fetchLineplots();
              }}
              onClick={handleMapClick}
              mapStyle={mapStyle}
              onMoveEnd={handleMoveEnd}
              id="map-container"
            >
              {showSearchBar && (
                <div
                  id={mapControlContainerIds.caveSearch}
                >
                  <CaveSearchMapControl />
                </div>
              )}

              <div
                id={mapControlContainerIds.layerControl}
              >
                <LayerControl position={layerControlPosition} />
              </div>
              <div
                id={mapControlContainerIds.advancedSearch}
              >
                <CaveAdvancedSearchDrawer
                  onSearch={runSearch}
                  queryBuilder={queryBuilder}
                  form={form}
                  onFiltersCleared={handleFiltersCleared}
                  inlineControls={renderAdvancedSearchControls}
                  entranceLocationFilter={entranceLocationFilter}
                  isFetchingEntranceLocation={isFetchingEntranceLocation}
                  onEntranceLocationChange={handleEntranceLocationChange}
                  onUseCurrentLocation={handleUseCurrentLocation}
                  onClearEntranceLocation={() => applyEntranceLocationFilter({})}
                  polygonResetSignal={polygonResetSignal}
                  filterClearSignal={filterClearSignal}
                />
              </div>

              {/* Lineplots layers from fetched data - render in order: Polygons, Lines, Points */}
              {sortedLineplotsData.map(({ id, data, type }) => {
                let paint: any;
                let layerType: "fill" | "line" | "circle";

                switch (type) {
                  case "Point":
                    layerType = "circle";
                    paint = {
                      "circle-color": "#ff5722",
                      "circle-radius": [
                        "interpolate",
                        ["linear"],
                        ["zoom"],
                        16,
                        0.5,
                        18,
                        4,
                        20,
                        8,
                      ],
                      "circle-opacity": ["step", ["zoom"], 0, 16, 0.8],
                      "circle-stroke-color": "#fff",
                      "circle-stroke-width": ["step", ["zoom"], 0, 16, 0.5],
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
                  <Source
                    key={id}
                    id={id}
                    type="geojson"
                    data={data}
                    attribution={`© ${accountName}`}
                  >
                    <Layer id={`${id}-layer`} type={layerType} paint={paint} />
                  </Source>
                );
              })}

              {/* Uploaded Shapefile layers */}
              {uploadedShapeFiles.map(({ id, data }) => {
                const firstFeature = data.features[0];
                const geomType =
                  firstFeature?.geometry?.type?.replace("Multi", "") ||
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

              {/* Entrances layer */}
              <Source
                key={entrancesSourceKey}
                id="entrances"
                type="vector"
                tiles={entranceTiles}
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

              {/* Popup for displaying feature properties */}
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
                id={mapControlContainerIds.navigationControls}
              >
                <NavigationControl position={zoomControlPosition} />
                {showGeolocateControl && (
                  <GeolocateControl
                    position={zoomControlPosition}
                    positionOptions={{ enableHighAccuracy: true }}
                    trackUserLocation={true}
                  />
                )}
                <ScaleControl position={scaleControlPosition} unit="imperial" />
              </div>

              {showFullScreenControl && (
                <div
                  id={mapControlContainerIds.fullScreenControl}
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

              {children}
            </Map>
          </MapProvider>
        </div>
      )}
    </Spin>
  );
};

const MapBaseMemo = React.memo(MapBaseComponent);
export { MapBaseMemo as MapBaseComponent };
