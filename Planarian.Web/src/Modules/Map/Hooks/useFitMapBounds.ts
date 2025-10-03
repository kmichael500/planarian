import { useEffect, useRef } from "react";
import type { FitBoundsOptions, LngLatBoundsLike } from "maplibre-gl";
import type { MapRef } from "react-map-gl/maplibre";

/**
 * Keeps the map view aligned with the provided bounds once the map is ready
 * and the container has a measurable size. This avoids calling fitBounds
 * while the canvas is zero-sized (which causes wildly different zoom levels).
 */
export const useFitMapBounds = (
  mapRef: MapRef | undefined,
  bounds: LngLatBoundsLike | null | undefined,
  options?: FitBoundsOptions
) => {
  const optionsRef = useRef<FitBoundsOptions | undefined>(options);

  useEffect(() => {
    optionsRef.current = options;
  }, [options]);

  useEffect(() => {
    if (!mapRef || !bounds) {
      return;
    }

    const mapInstance = mapRef.getMap();
    if (!mapInstance) {
      return;
    }

    let cancelled = false;
    let rafId: number | null = null;
    const needsLoadListener = !mapInstance.isStyleLoaded();

    const hasContainerSize = () => {
      const container = mapInstance.getContainer();
      return (
        container.clientWidth > 0 &&
        container.clientHeight > 0
      );
    };

    const applyFit = () => {
      if (cancelled) {
        return;
      }

      try {
        mapInstance.resize();

        if (!hasContainerSize()) {
          rafId = requestAnimationFrame(applyFit);
          return;
        }

        mapInstance.fitBounds(bounds, {
          duration: 0,
          padding: 20,
          ...optionsRef.current,
        });
      } catch (error) {
        // Ignore fit errors – they occur if the bounds are invalid.
      }
    };

    const scheduleFit = () => {
      if (cancelled) {
        return;
      }
      rafId = requestAnimationFrame(applyFit);
    };

    if (needsLoadListener) {
      mapInstance.once("load", scheduleFit);
    } else {
      scheduleFit();
    }

    return () => {
      cancelled = true;
      if (needsLoadListener) {
        mapInstance.off("load", scheduleFit);
      }
      if (rafId !== null) {
        cancelAnimationFrame(rafId);
      }
    };
  }, [mapRef, bounds]);
};
