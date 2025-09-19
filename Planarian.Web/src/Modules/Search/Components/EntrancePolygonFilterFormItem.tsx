import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Button, Form, Space, Spin, Typography, message } from "antd";
import type { GeoJsonObject } from "geojson";
import {
  Layer,
  Map,
  MapLayerMouseEvent,
  MapRef,
  MapProvider,
  Source,
} from "react-map-gl/maplibre";
import { NestedKeyOf } from "../../../Shared/Helpers/StringHelpers";
import {
  QueryBuilder,
  QueryOperator,
} from "../Services/QueryBuilder";
import { MapService } from "../../Map/Services/MapService";
import type { LngLatBoundsLike } from "maplibre-gl";
import { LayerControl } from "../../Map/Components/LayerControl";
import { StyleSpecification } from "@maplibre/maplibre-gl-style-spec";
import shpjs from "shpjs";
import bbox from "@turf/bbox";

const mapStyle: StyleSpecification = {
  glyphs:
    "https://api.mapbox.com/fonts/v1/mapbox/{fontstack}/{range}.pbf?access_token=pk.eyJ1IjoibWljaGFlbGtldHpuZXIiLCJhIjoiY2xvODFyN3lqMDl3bzJxbm56d3lzOTBkNyJ9.9_UNmt2gelLuQ-BPQjPiCQ",
  version: 8,
  sources: {},
  layers: [],
} as StyleSpecification;

const LAYER_CONTROL_POSITION = {
  top: "8px",
  right: "0",
};

type InitialViewState = {
  latitude: number;
  longitude: number;
  zoom: number;
  bearing: number;
  pitch: number;
};

const DEFAULT_VIEW_STATE: InitialViewState = {
  latitude: 39.8283,
  longitude: -98.5795,
  zoom: 3.5,
  bearing: 0,
  pitch: 0,
};

type LngLatTuple = [number, number];

const coordsAreEqual = (a: LngLatTuple, b: LngLatTuple) => {
  return Math.abs(a[0] - b[0]) < 1e-9 && Math.abs(a[1] - b[1]) < 1e-9;
};

const closeRing = (coords: LngLatTuple[]): LngLatTuple[] => {
  if (coords.length === 0) {
    return coords;
  }

  const first = coords[0];
  const last = coords[coords.length - 1];

  return coordsAreEqual(first, last) ? coords : [...coords, first];
};

const normalizeRing = (coords: LngLatTuple[]): LngLatTuple[] => {
  if (coords.length === 0) {
    return coords;
  }

  const normalized = coords.slice();

  if (
    normalized.length >= 2 &&
    coordsAreEqual(normalized[0], normalized[normalized.length - 1])
  ) {
    normalized.pop();
  }

  return normalized;
};

const parsePolygon = (rawValue?: string | null): LngLatTuple[] | null => {
  if (!rawValue) {
    return null;
  }

  try {
    const parsed = JSON.parse(rawValue) as GeoJsonObject & {
      type?: string;
      coordinates?: unknown;
    };

    if (parsed.type !== "Polygon" || !Array.isArray(parsed.coordinates)) {
      return null;
    }

    const outerRing = parsed.coordinates[0];

    if (!Array.isArray(outerRing)) {
      return null;
    }

    const coords: LngLatTuple[] = [];

    outerRing.forEach((entry) => {
      if (Array.isArray(entry) && entry.length >= 2) {
        const [lng, lat] = entry;
        const lngNumber = Number(lng);
        const latNumber = Number(lat);
        if (Number.isFinite(lngNumber) && Number.isFinite(latNumber)) {
          coords.push([lngNumber, latNumber]);
        }
      }
    });

    if (coords.length < 3) {
      return null;
    }

    // Remove closing duplicate if present so we can manage it consistently.
    if (coordsAreEqual(coords[0], coords[coords.length - 1])) {
      coords.pop();
    }

    return coords;
  } catch (error) {
    return null;
  }
};

interface EntrancePolygonFilterFormItemProps<T extends object> {
  queryBuilder: QueryBuilder<T>;
  field: NestedKeyOf<T>;
  label: string;
  resetSignal?: number;
  helpText?: string;
}

const EntrancePolygonFilterFormItem = <T extends object,>(
  props: EntrancePolygonFilterFormItemProps<T>
) => {
  const { queryBuilder, field, label, resetSignal, helpText } = props;
  const mapRef = useRef<MapRef | null>(null);
  const pendingCenterRef = useRef<LngLatTuple | null>(null);
  const pendingBoundsRef = useRef<LngLatBoundsLike | null>(null);
  const hasSavedPolygonRef = useRef(false);
  const [savedPolygon, setSavedPolygon] = useState<LngLatTuple[] | null>(null);
  const [drawingCoords, setDrawingCoords] = useState<LngLatTuple[]>([]);
  const [isDrawing, setIsDrawing] = useState(false);
  const [initialViewState, setInitialViewState] = useState<InitialViewState | null>(null);
  const [isInitializing, setIsInitializing] = useState(true);
  const [dragActive, setDragActive] = useState(false);

  useEffect(() => {
    let isMounted = true;

    const flyToCenter = (center: LngLatTuple, zoom: number) => {
      pendingCenterRef.current = center;
      const mapInstance = mapRef.current?.getMap();
      if (mapInstance && mapInstance.isStyleLoaded()) {
        mapRef.current?.flyTo({ center, zoom, duration: 0 });
        pendingCenterRef.current = null;
      }
    };

    const applyPolygonViewState = (polygon: LngLatTuple[]) => {
      const lats = polygon.map((coord) => coord[1]);
      const lngs = polygon.map((coord) => coord[0]);

      const polygonCenter: InitialViewState = {
        latitude: (Math.min(...lats) + Math.max(...lats)) / 2,
        longitude: (Math.min(...lngs) + Math.max(...lngs)) / 2,
        zoom: initialViewState?.zoom ?? 8,
        bearing: 0,
        pitch: 0,
      };

      setInitialViewState(polygonCenter);
      setIsInitializing(false);
    };

    if (savedPolygon && savedPolygon.length >= 3) {
      applyPolygonViewState(savedPolygon);
      return () => {
        isMounted = false;
      };
    }

    setIsInitializing(true);
    MapService.getMapCenter()
      .then((center) => {
        if (!isMounted || hasSavedPolygonRef.current) {
          return;
        }

        const targetState: InitialViewState = {
          latitude: center.latitude,
          longitude: center.longitude,
          zoom: 5,
          bearing: 0,
          pitch: 0,
        };

        setInitialViewState(targetState);
        flyToCenter([center.longitude, center.latitude], targetState.zoom);
      })
      .catch(() => {
        if (!isMounted) {
          return;
        }

        setInitialViewState(DEFAULT_VIEW_STATE);
      })
      .finally(() => {
        if (isMounted) {
          setIsInitializing(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, [savedPolygon]);

  useEffect(() => {
    const existingValue = queryBuilder.getFieldValue(field as string) as
      | string
      | undefined;
    const parsed = parsePolygon(existingValue);
    hasSavedPolygonRef.current = !!parsed && parsed.length >= 3;
    setSavedPolygon(parsed);
    setIsDrawing(false);
    setDrawingCoords([]);
  }, [field, queryBuilder, resetSignal]);

  useEffect(() => {
    if (!isDrawing && savedPolygon && savedPolygon.length > 0) {
      const lats = savedPolygon.map((coord) => coord[1]);
      const lngs = savedPolygon.map((coord) => coord[0]);

      const bounds: LngLatBoundsLike = [
        [Math.min(...lngs), Math.min(...lats)],
        [Math.max(...lngs), Math.max(...lats)],
      ];

      if (mapRef.current) {
        mapRef.current.fitBounds(bounds, {
          padding: 24,
          duration: 500,
        });
      } else {
        pendingBoundsRef.current = bounds;
      }
    }
  }, [isDrawing, savedPolygon]);

  const activeCoords = isDrawing
    ? drawingCoords
    : savedPolygon ?? undefined;

  const polygonFeature = useMemo(() => {
    if (!activeCoords || activeCoords.length < 3) {
      return null;
    }

    const ring = closeRing(activeCoords);
    return {
      type: "Feature",
      geometry: {
        type: "Polygon",
        coordinates: [ring],
      },
      properties: {},
    } as GeoJsonObject;
  }, [activeCoords]);

  const lineFeature = useMemo(() => {
    if (!activeCoords || activeCoords.length < 2) {
      return null;
    }

    return {
      type: "Feature",
      geometry: {
        type: "LineString",
        coordinates: activeCoords,
      },
      properties: {},
    } as GeoJsonObject;
  }, [activeCoords]);

  const handleMapClick = useCallback(
    (event: MapLayerMouseEvent) => {
      if (!isDrawing) {
        return;
      }

      const { lngLat } = event;
      const newPoint: LngLatTuple = [lngLat.lng, lngLat.lat];
      setDrawingCoords((prev) => [...prev, newPoint]);
    },
    [isDrawing]
  );

  const handleStartDrawing = () => {
    setDrawingCoords(savedPolygon ?? []);
    setIsDrawing(true);
  };

  const handleCancelDrawing = () => {
    setDrawingCoords([]);
    setIsDrawing(false);
  };

  const handleUndoLastPoint = () => {
    setDrawingCoords((prev) => prev.slice(0, -1));
  };

  const handleClear = () => {
    setSavedPolygon(null);
    setDrawingCoords([]);
    setIsDrawing(false);
    queryBuilder.removeFromDictionary(field as string);
    pendingBoundsRef.current = null;
    pendingCenterRef.current = null;
    hasSavedPolygonRef.current = false;
  };

  const persistPolygon = (coords: LngLatTuple[]) => {
    const normalized = normalizeRing(coords);
    if (normalized.length < 3) {
      message.warning("Add at least three points to create a polygon.");
      return;
    }

    const ring = closeRing(normalized);
    const polygonGeoJson = {
      type: "Polygon",
      coordinates: [ring],
    };

    queryBuilder.filterBy(
      field,
      QueryOperator.Equal,
      JSON.stringify(polygonGeoJson) as any
    );

    setSavedPolygon(normalized);
    setDrawingCoords([]);
    setIsDrawing(false);
    hasSavedPolygonRef.current = true;
  };

  const handleFinishDrawing = () => {
    if (drawingCoords.length < 3) {
      message.warning("Add at least three points to close the polygon.");
      return;
    }

    persistPolygon(drawingCoords);
  };

  const handleDragEnter = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setDragActive(true);
  };

  const handleDragOver = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
  };

  const handleDragLeave = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    if (event.target === event.currentTarget) {
      setDragActive(false);
    }
  };

  const handleDrop = async (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setDragActive(false);

    const file = event.dataTransfer.files?.[0];
    if (!file) {
      return;
    }

    try {
      const arrayBuffer = await file.arrayBuffer();
      const parsed = await shpjs(arrayBuffer);

      const collections = Array.isArray(parsed) ? parsed : [parsed];

      let polygonCoords: LngLatTuple[] | null = null;
      let polygonBounds: number[] | null = null;

      for (const collection of collections) {
        const features = (collection as any)?.features ?? [];
        for (const feature of features) {
          const geometry = feature?.geometry;
          if (!geometry) {
            continue;
          }

          if (geometry.type === "Polygon" && geometry.coordinates?.length > 0) {
            const ring = geometry.coordinates[0];
            if (Array.isArray(ring) && ring.length >= 3) {
              polygonCoords = ring.map((coordinate: number[]) => [
                coordinate[0],
                coordinate[1],
              ]) as LngLatTuple[];
              polygonBounds = bbox(feature);
              break;
            }
          }

          if (
            geometry.type === "MultiPolygon" &&
            geometry.coordinates?.length > 0 &&
            geometry.coordinates[0]?.[0]?.length >= 3
          ) {
            const ring = geometry.coordinates[0][0];
            polygonCoords = ring.map((coordinate: number[]) => [
              coordinate[0],
              coordinate[1],
            ]) as LngLatTuple[];
            polygonBounds = bbox(feature);
            break;
          }
        }

        if (polygonCoords) {
          break;
        }
      }

      if (!polygonCoords || polygonCoords.length < 3) {
        message.warning(
          "No polygon geometry found in the provided shapefile."
        );
        return;
      }

      if (coordsAreEqual(polygonCoords[0], polygonCoords[polygonCoords.length - 1])) {
        polygonCoords = polygonCoords.slice(0, -1);
      }

      persistPolygon(polygonCoords);

      if (polygonBounds && mapRef.current) {
        mapRef.current.fitBounds(
          [
            [polygonBounds[0], polygonBounds[1]],
            [polygonBounds[2], polygonBounds[3]],
          ],
          {
            padding: 40,
            duration: 600,
          }
        );
      }
    }
    catch (error) {
      console.error("Error parsing shapefile", error);
      message.error(
        "Unable to read shapefile. Ensure you drop a zipped polygon shapefile (.shp, .dbf, .shx)."
      );
    }
  };

  return (
    <Form.Item label={label} colon={false} style={{ marginBottom: 16 }}>
      <Space direction="vertical" size={8} style={{ width: "100%" }}>
        <div
          onDragEnter={handleDragEnter}
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          style={{
            position: "relative",
            width: "100%",
            height: 260,
            border: "1px solid #d9d9d9",
            borderRadius: 4,
            overflow: "hidden",
          }}
        >
          {dragActive && (
            <div
              style={{
                position: "absolute",
                inset: 0,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                backgroundColor: "rgba(79,157,255,0.12)",
                border: "2px dashed #1f6fde",
                color: "#1f6fde",
                fontWeight: 600,
                zIndex: 1000,
                pointerEvents: "none",
              }}
            >
              <Typography.Text strong>
                Drop a polygon shapefile (.zip) to use as your search area
              </Typography.Text>
            </div>
          )}
          <MapProvider>
            {initialViewState ? (
              <Map
                ref={mapRef}
                initialViewState={initialViewState as any}
                mapStyle={mapStyle}
                style={{ width: "100%", height: "100%" }}
                onClick={handleMapClick}
                cursor={isDrawing ? "crosshair" : "grab"}
                reuseMaps
                antialias
                onLoad={() => {
                  if (pendingBoundsRef.current && mapRef.current) {
                    mapRef.current.fitBounds(pendingBoundsRef.current, {
                      padding: 24,
                      duration: 0,
                    });
                    pendingBoundsRef.current = null;
                  } else if (pendingCenterRef.current && mapRef.current) {
                    mapRef.current.flyTo({
                      center: pendingCenterRef.current,
                      zoom: mapRef.current.getMap().getZoom(),
                      duration: 0,
                    });
                    pendingCenterRef.current = null;
                  }
                }}
              >
                <div id="layer-control-container">
                  <LayerControl position={LAYER_CONTROL_POSITION} />
                </div>
                {polygonFeature && (
                  <Source id="entrance-polygon" type="geojson" data={polygonFeature}>
                    <Layer
                      id="entrance-polygon-fill"
                      type="fill"
                      paint={{
                        "fill-color": "#4f9dff",
                        "fill-opacity": 0.35,
                      }}
                    />
                    <Layer
                      id="entrance-polygon-outline"
                      type="line"
                      paint={{
                        "line-color": "#1f6fde",
                        "line-width": 2,
                      }}
                    />
                  </Source>
                )}
                {isDrawing && lineFeature && (
                  <Source id="entrance-polygon-line" type="geojson" data={lineFeature}>
                    <Layer
                      id="entrance-polygon-line-layer"
                      type="line"
                      paint={{
                        "line-color": "#1f6fde",
                        "line-width": 2,
                        "line-dasharray": [1, 1],
                      }}
                    />
                  </Source>
                )}
              </Map>
            ) : (
              <div
                style={{
                  width: "100%",
                  height: "100%",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  backgroundColor: "#f5f5f5",
                }}
              >
                <Spin spinning={isInitializing} />
              </div>
            )}
          </MapProvider>
        </div>
        <Space wrap>
          {isDrawing ? (
            <>
              <Button
                size="small"
                type="primary"
                onClick={handleFinishDrawing}
                disabled={drawingCoords.length < 3}
              >
                Finish Polygon
              </Button>
              <Button
                size="small"
                onClick={handleUndoLastPoint}
                disabled={drawingCoords.length === 0}
              >
                Undo Last Point
              </Button>
              <Button size="small" onClick={handleCancelDrawing}>
                Cancel
              </Button>
            </>
          ) : (
            <>
              <Button size="small" type="primary" onClick={handleStartDrawing}>
                {savedPolygon ? "Redraw Polygon" : "Draw Polygon"}
              </Button>
              <Button
                size="small"
                onClick={handleClear}
                disabled={!savedPolygon}
              >
                Clear
              </Button>
            </>
          )}
        </Space>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {isDrawing
            ? "Click on the map to add points. Finish the polygon when you have at least three points."
            : helpText ??
            "Draw a polygon to find caves with entrances inside the selected area."}
        </Typography.Text>
      </Space>
    </Form.Item>
  );
};

export { EntrancePolygonFilterFormItem };
