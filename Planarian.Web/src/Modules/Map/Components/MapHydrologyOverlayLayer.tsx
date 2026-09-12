import React, { useEffect, useMemo, useRef, useState } from "react";
import type { FeatureCollection, Point } from "geojson";
import type { MapMouseEvent } from "maplibre-gl";
import { Layer, Popup, Source, useMap } from "react-map-gl/maplibre";
import { formatCoordinates } from "../../../Shared/Helpers/StringHelpers";
import { HydrologyFeature, MapService } from "../Services/MapService";
import { useMapLayers } from "./MapLayerContext";
import {
  STREAM_GAGE_LABEL_LAYER_ID,
  STREAM_GAGE_POINT_LAYER_ID,
} from "./MapStreamGageLayer";

export const HYDROLOGY_3DHP_SPRINGS_LAYER_SETTING_ID = "usgs-3dhp-springs";
export const HYDROLOGY_3DHP_SINKS_LAYER_SETTING_ID = "usgs-3dhp-sinks";
export const HYDROLOGY_3DHP_OUTLETS_LAYER_SETTING_ID = "usgs-3dhp-waterbody-outlets";
export const HYDROLOGY_3DHP_SOURCE_ID = "usgs-3dhp-features-source";
export const HYDROLOGY_3DHP_POINT_LAYER_ID = "usgs-3dhp-features-points";
export const HYDROLOGY_3DHP_LABEL_LAYER_ID = "usgs-3dhp-features-labels";
const HYDROLOGY_3DHP_MIN_ZOOM = 9;

type HydrologyProperties = {
  id: string;
  name: string;
  featureType: string;
  source: string;
};

type PopupFeature = HydrologyProperties & {
  latitude: number;
  longitude: number;
};
const isOverlayFeature = (layerId: string | undefined) =>
  layerId === HYDROLOGY_3DHP_POINT_LAYER_ID ||
  layerId === HYDROLOGY_3DHP_LABEL_LAYER_ID ||
  layerId === STREAM_GAGE_POINT_LAYER_ID ||
  layerId === STREAM_GAGE_LABEL_LAYER_ID;

export const hasHydrologyOverlayFeatureAtPoint = (
  map: { queryRenderedFeatures: (...args: any[]) => any[]; getLayer: (id: string) => unknown } | null | undefined,
  point: { x: number; y: number }
) => {
  if (!map) return false;
  const layers = [
    HYDROLOGY_3DHP_POINT_LAYER_ID,
    HYDROLOGY_3DHP_LABEL_LAYER_ID,
    STREAM_GAGE_POINT_LAYER_ID,
    STREAM_GAGE_LABEL_LAYER_ID,
  ].filter((id) => map.getLayer(id));
  if (layers.length === 0) return false;
  return map.queryRenderedFeatures(point, { layers }).some((feature) =>
    isOverlayFeature(feature.layer?.id)
  );
};

export const MapHydrologyOverlayLayer: React.FC = () => {
  const { current: map } = useMap();
  const { isVisible, opacity } = useMapLayers();
  const springsVisible = isVisible(HYDROLOGY_3DHP_SPRINGS_LAYER_SETTING_ID);
  const sinksVisible = isVisible(HYDROLOGY_3DHP_SINKS_LAYER_SETTING_ID);
  const outletsVisible = isVisible(HYDROLOGY_3DHP_OUTLETS_LAYER_SETTING_ID);
  const visible = springsVisible || sinksVisible || outletsVisible;
  const layerOpacity = opacity("usgs-hydrology-group");
  const [features, setFeatures] = useState<HydrologyFeature[]>([]);
  const [zoom, setZoom] = useState<number | null>(null);
  const [popup, setPopup] = useState<PopupFeature | null>(null);
  const requestRevision = useRef(0);
  useEffect(() => {
    const instance = map?.getMap();
    if (!instance) return;

    const loadFeatures = () => {
      const nextZoom = instance.getZoom();
      setZoom(nextZoom);
      setPopup(null);

      if (!visible || nextZoom < HYDROLOGY_3DHP_MIN_ZOOM) {
        requestRevision.current += 1;
        setFeatures([]);
        return;
      }

      const bounds = instance.getBounds();
      const revision = ++requestRevision.current;
      MapService.getHydrologyFeaturesInBounds(
        bounds.getNorth(),
        bounds.getSouth(),
        bounds.getEast(),
        bounds.getWest()
      )
        .then((result) => {
          if (requestRevision.current === revision) setFeatures(result);
        })
        .catch((error) => {
          console.error("Unable to load USGS 3DHP map features", error);
          if (requestRevision.current === revision) setFeatures([]);
        });
    };

    if (instance.isStyleLoaded()) {
      loadFeatures();
    } else {
      instance.once("load", loadFeatures);
    }
    instance.on("moveend", loadFeatures);

    return () => {
      requestRevision.current += 1;
      instance.off("load", loadFeatures);
      instance.off("moveend", loadFeatures);
    };
  }, [map, visible]);

  useEffect(() => {
    const instance = map?.getMap();
    if (!instance || !visible) return;

    const handleClick = (event: MapMouseEvent) => {
      const layers = [HYDROLOGY_3DHP_POINT_LAYER_ID, HYDROLOGY_3DHP_LABEL_LAYER_ID]
        .filter((id) => instance.getLayer(id));
      if (layers.length === 0) return;

      const feature = instance.queryRenderedFeatures(event.point, { layers })[0];
      if (!feature || feature.geometry.type !== "Point") return;

      const coordinates = feature.geometry.coordinates as [number, number];
      const properties = feature.properties ?? {};
      const featureType = typeof properties.featureType === "string"
        ? properties.featureType
        : "Feature";
      setPopup({
        id: typeof properties.id === "string" ? properties.id : "",
        name: typeof properties.name === "string" && properties.name
          ? properties.name
          : featureType,
        featureType,
        source: typeof properties.source === "string" ? properties.source : "USGS 3DHP",
        longitude: coordinates[0],
        latitude: coordinates[1],
      });
    };

    instance.on("click", handleClick);
    return () => {
      instance.off("click", handleClick);
    };
  }, [map, visible]);

  const featureCollection = useMemo<FeatureCollection<Point, HydrologyProperties>>(
    () => ({
      type: "FeatureCollection",
      features: features
        .filter((feature) =>
          (feature.featureType === "Spring" && springsVisible) ||
          (feature.featureType === "Sink" && sinksVisible) ||
          (feature.featureType === "Waterbody Outlet" && outletsVisible)
        )
        .map((feature) => ({
          type: "Feature",
          geometry: {
            type: "Point",
            coordinates: [feature.longitude, feature.latitude],
          },
          properties: {
            id: feature.id,
            name: feature.name ?? "",
            featureType: feature.featureType,
            source: feature.source,
          },
        })),
    }),
    [features, outletsVisible, sinksVisible, springsVisible]
  );

  if (!visible) return null;

  if (zoom !== null && zoom < HYDROLOGY_3DHP_MIN_ZOOM) {
    return (
      <div
        className="planarian-map-control"
        style={{
          position: "absolute",
          left: "50%",
          bottom: 34,
          transform: "translateX(-50%)",
          padding: "4px 8px",
          borderRadius: 4,
          background: "var(--surface-color)",
          border: "1px solid var(--border-color)",
          pointerEvents: "none",
        }}
      >
        Zoom in to view USGS 3DHP hydrology features
      </div>
    );
  }

  return (
    <>
      <Source
        id={HYDROLOGY_3DHP_SOURCE_ID}
        type="geojson"
        data={featureCollection}
        attribution="USGS 3DHP"
      >
        <Layer
          id={HYDROLOGY_3DHP_POINT_LAYER_ID}
          type="circle"
          minzoom={HYDROLOGY_3DHP_MIN_ZOOM}
          paint={{
            "circle-color": [
              "match",
              ["get", "featureType"],
              "Spring",
              "#1677ff",
              "Sink",
              "#fa8c16",
              "Waterbody Outlet",
              "#13a8a8",
              "#64748b",
            ],
            "circle-opacity": layerOpacity,
            "circle-radius": 5.5,
            "circle-stroke-color": "#ffffff",
            "circle-stroke-opacity": layerOpacity,
            "circle-stroke-width": 1.5,
          }}
        />
        <Layer
          id={HYDROLOGY_3DHP_LABEL_LAYER_ID}
          type="symbol"
          minzoom={HYDROLOGY_3DHP_MIN_ZOOM}
          layout={{
            "text-field": [
              "case",
              ["!=", ["get", "name"], ""],
              ["concat", ["get", "name"], "\n", ["get", "featureType"]],
              ["get", "featureType"],
            ],
            "text-size": 11,
            "text-anchor": "top",
            "text-offset": [0, 0.9],
            "text-allow-overlap": false,
            "text-optional": true,
          }}
          paint={{
            "text-color": "#111827",
            "text-opacity": layerOpacity,
            "text-halo-color": "rgba(255, 255, 255, 0.95)",
            "text-halo-width": 1.5,
          }}
        />
      </Source>
      {popup && (
        <Popup
          longitude={popup.longitude}
          latitude={popup.latitude}
          anchor="bottom"
          closeOnClick={false}
          onClose={() => setPopup(null)}
        >
          <div style={{ minWidth: 150 }}>
            <strong>{popup.name}</strong>
            <div>{popup.featureType}</div>
            <div>{formatCoordinates(popup.latitude, popup.longitude)}</div>
            <div>{popup.source}</div>
          </div>
        </Popup>
      )}
    </>
  );
};
