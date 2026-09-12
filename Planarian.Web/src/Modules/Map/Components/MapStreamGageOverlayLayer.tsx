import React, { useEffect, useRef, useState } from "react";
import type { MapMouseEvent } from "maplibre-gl";
import { Popup, useMap } from "react-map-gl/maplibre";
import { formatCoordinates } from "../../../Shared/Helpers/StringHelpers";
import { MapService, StreamGageLocation } from "../Services/MapService";
import { useMapLayers } from "./MapLayerContext";
import {
  MapStreamGageLayer,
  STREAM_GAGE_LABEL_LAYER_ID,
  STREAM_GAGE_LAYER_SETTING_ID,
  STREAM_GAGE_POINT_LAYER_ID,
} from "./MapStreamGageLayer";

const STREAM_GAGE_MIN_ZOOM = 8;

export const MapStreamGageOverlayLayer: React.FC = () => {
  const { current: map } = useMap();
  const { isVisible } = useMapLayers();
  const visible = isVisible(STREAM_GAGE_LAYER_SETTING_ID);
  const [gages, setGages] = useState<StreamGageLocation[]>([]);
  const [popup, setPopup] = useState<StreamGageLocation | null>(null);
  const requestRevision = useRef(0);

  useEffect(() => {
    const instance = map?.getMap();
    if (!instance) return;

    const loadGages = () => {
      const bounds = instance.getBounds();
      const zoom = instance.getZoom();
      setPopup(null);

      if (
        !visible ||
        zoom < STREAM_GAGE_MIN_ZOOM ||
        bounds.getNorth() - bounds.getSouth() > 10 ||
        bounds.getEast() - bounds.getWest() > 10
      ) {
        requestRevision.current += 1;
        setGages([]);
        return;
      }

      const revision = ++requestRevision.current;
      MapService.getStreamGagesInBounds(
        bounds.getNorth(),
        bounds.getSouth(),
        bounds.getEast(),
        bounds.getWest()
      )
        .then((result) => {
          if (requestRevision.current === revision) setGages(result);
        })
        .catch((error) => {
          console.error("Unable to load USGS stream gages", error);
          if (requestRevision.current === revision) setGages([]);
        });
    };

    if (instance.isStyleLoaded()) {
      loadGages();
    } else {
      instance.once("load", loadGages);
    }
    instance.on("moveend", loadGages);

    return () => {
      requestRevision.current += 1;
      instance.off("load", loadGages);
      instance.off("moveend", loadGages);
    };
  }, [map, visible]);

  useEffect(() => {
    const instance = map?.getMap();
    if (!instance || !visible) return;

    const handleClick = (event: MapMouseEvent) => {
      const layers = [STREAM_GAGE_POINT_LAYER_ID, STREAM_GAGE_LABEL_LAYER_ID]
        .filter((id) => instance.getLayer(id));
      if (layers.length === 0) return;

      const feature = instance.queryRenderedFeatures(event.point, { layers })[0];
      const id = feature?.properties?.id;
      if (typeof id !== "string") return;

      const gage = gages.find((candidate) => candidate.id === id);
      if (gage) setPopup(gage);
    };

    instance.on("click", handleClick);
    return () => {
      instance.off("click", handleClick);
    };
  }, [gages, map, visible]);

  if (!visible) return null;

  return (
    <>
      <MapStreamGageLayer gages={gages} />
      {popup && (
        <Popup
          longitude={popup.longitude}
          latitude={popup.latitude}
          anchor="bottom"
          closeOnClick={false}
          onClose={() => setPopup(null)}
        >
          <div style={{ minWidth: 170 }}>
            <strong>{popup.siteName}</strong>
            <div>USGS {popup.siteCode}</div>
            <div>{formatCoordinates(popup.latitude, popup.longitude)}</div>
            <div>USGS Water Data</div>
          </div>
        </Popup>
      )}
    </>
  );
};
