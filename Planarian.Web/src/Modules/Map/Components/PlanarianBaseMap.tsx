import React, { forwardRef, useCallback, useMemo, useRef } from "react";
import {
  ErrorEvent,
  GeolocateControl,
  Map,
  MapLayerMouseEvent,
  MapProps,
  MapRef,
  NavigationControl,
  ScaleControl,
  useMap,
  ViewStateChangeEvent,
} from "react-map-gl/maplibre";
import type { FitBoundsOptions, LngLatBoundsLike } from "maplibre-gl";
import type { StyleSpecification } from "@maplibre/maplibre-gl-style-spec";
import { message } from "antd";
import { AppOptions } from "../../../Shared/Services/AppService";
import { ApiExceptionType, ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { useFitMapBounds } from "../Hooks/useFitMapBounds";
import "./PlanarianBaseMap.scss";

export const MAP_OVERLAY_ANCHOR_LAYER_ID = "planarian-overlay-anchor";

const mapStyle: StyleSpecification = {
  glyphs: "https://api.mapbox.com/fonts/v1/mapbox/{fontstack}/{range}.pbf?access_token=pk.eyJ1IjoibWljaGFlbGtldHpuZXIiLCJhIjoiY2xvODF0M2ZiMDloNTJpbzYzdXRrYWhrcSJ9.B8x4P8SK9Zpe-sdN6pJ3Eg",
  version: 8,
  sources: {},
  layers: [{ id: MAP_OVERLAY_ANCHOR_LAYER_ID, type: "background", paint: { "background-opacity": 0 } }],
} as StyleSpecification;

interface PlanarianBaseMapProps {
  initialCenter: [number, number];
  initialZoom?: number;
  initialBounds?: LngLatBoundsLike;
  initialFitBoundsOptions?: FitBoundsOptions;
  onMoveEnd?: (event: ViewStateChangeEvent) => void;
  onClick?: (event: MapLayerMouseEvent) => void;
  interactiveLayerIds?: string[];
  showGeolocate?: boolean;
  reuseMaps?: boolean;
  children?: React.ReactNode;
}

const InitialBoundsFitter: React.FC<{
  bounds?: LngLatBoundsLike;
  options?: FitBoundsOptions;
}> = ({ bounds, options }) => {
  const { current: map } = useMap();
  useFitMapBounds(map, bounds, options);
  return null;
};

export const PlanarianBaseMap = forwardRef<MapRef, PlanarianBaseMapProps>(function PlanarianBaseMap(
  {
    initialCenter,
    initialZoom = 7,
    initialBounds,
    initialFitBoundsOptions,
    onMoveEnd,
    onClick,
    interactiveLayerIds,
    showGeolocate = false,
    reuseMaps = true,
    children,
  },
  ref
) {
  const rateLimitShown = useRef(false);
  const initialViewState = useMemo<MapProps["initialViewState"]>(
    () => ({ longitude: initialCenter[1], latitude: initialCenter[0], zoom: initialZoom }),
    [initialCenter, initialZoom]
  );

  const transformRequest = useCallback<NonNullable<MapProps["transformRequest"]>>((url) => {
    if (AppOptions.apiBaseUrl && url.startsWith(AppOptions.apiBaseUrl)) {
      return { url, credentials: "include" };
    }
    return { url };
  }, []);

  const handleError = useCallback((event: ErrorEvent) => {
    const error = event.error as (Error & { status?: number; data?: ApiErrorResponse }) | undefined;
    const rawMessage = error?.message ?? "";
    const isRateLimit =
      error?.data?.errorCode === ApiExceptionType.TooManyRequests ||
      error?.status === 429 ||
      rawMessage.includes("(429)") ||
      rawMessage.includes(" 429") ||
      rawMessage.includes("AJAXError: (429)");

    if (isRateLimit && !rateLimitShown.current) {
      rateLimitShown.current = true;
      const support = AppOptions.supportName && AppOptions.supportEmail
        ? ` If you believe this was in error, please contact ${AppOptions.supportName} at ${AppOptions.supportEmail}.`
        : "";
      message.error(`Rate limit exceeded. Please try again later.${support}`);
    }
  }, []);

  return (
    <div className="planarian-map-canvas">
      <Map
        ref={ref}
        maxPitch={85}
        reuseMaps={reuseMaps}
        antialias
        interactiveLayerIds={interactiveLayerIds}
        initialViewState={initialViewState}
        onClick={onClick}
        onError={handleError}
        onMoveEnd={onMoveEnd}
        mapStyle={mapStyle}
        transformRequest={transformRequest}
      >
        <InitialBoundsFitter bounds={initialBounds} options={initialFitBoundsOptions} />
        <NavigationControl position="top-left" />
        {showGeolocate && (
          <GeolocateControl
            position="top-left"
            positionOptions={{ enableHighAccuracy: true }}
            trackUserLocation
          />
        )}
        <ScaleControl position="bottom-left" unit="imperial" />
        {children}
      </Map>
    </div>
  );
});
