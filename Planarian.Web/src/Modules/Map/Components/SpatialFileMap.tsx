import React, { useCallback, useState } from "react";
import { Popup } from "react-map-gl/maplibre";
import type { FitBoundsOptions, LngLatBoundsLike } from "maplibre-gl";
import type { MapLayerMouseEvent } from "react-map-gl/maplibre";
import { MapLayerControl } from "./MapLayerControl";
import { PlanarianBaseMap } from "./PlanarianBaseMap";
import { MapLayerProvider } from "./MapLayerContext";
import { MapLayers } from "./MapLayers";

interface SpatialFileMapProps {
  initialCenter: [number, number];
  initialZoom?: number;
  bounds: LngLatBoundsLike;
  fitBoundsOptions?: FitBoundsOptions;
  interactiveLayerIds?: string[];
  children: React.ReactNode;
}

export const SpatialFileMap: React.FC<SpatialFileMapProps> = ({
  initialCenter,
  initialZoom = 10,
  bounds,
  fitBoundsOptions,
  interactiveLayerIds = [],
  children,
}) => {
  const [popup, setPopup] = useState<{
    longitude: number;
    latitude: number;
    properties: Record<string, unknown>;
  } | null>(null);

  const handleClick = useCallback((event: MapLayerMouseEvent) => {
    const feature = event.features?.[0];
    if (!feature?.properties) {
      setPopup(null);
      return;
    }
    setPopup({
      longitude: event.lngLat.lng,
      latitude: event.lngLat.lat,
      properties: feature.properties,
    });
  }, []);

  return (
    <MapLayerProvider>
      <PlanarianBaseMap
        initialCenter={initialCenter}
        initialZoom={initialZoom}
        initialBounds={bounds}
        initialFitBoundsOptions={fitBoundsOptions}
        interactiveLayerIds={interactiveLayerIds.length ? interactiveLayerIds : undefined}
        onClick={interactiveLayerIds.length ? handleClick : undefined}
        reuseMaps={false}
      >
        <MapLayers />
        <MapLayerControl position={{ top: "0", right: "0" }} />
        {children}
        {popup && (
          <Popup
            longitude={popup.longitude}
            latitude={popup.latitude}
            onClose={() => setPopup(null)}
            closeOnClick={false}
            anchor="top"
          >
            <pre style={{ whiteSpace: "pre-wrap", wordWrap: "break-word", maxHeight: 200, overflow: "auto" }}>
              {JSON.stringify(popup.properties, null, 2)}
            </pre>
          </Popup>
        )}
      </PlanarianBaseMap>
    </MapLayerProvider>
  );
};
