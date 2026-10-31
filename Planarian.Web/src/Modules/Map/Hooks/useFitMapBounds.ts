import { useEffect, useRef } from "react";
import type { FitBoundsOptions, LngLatBoundsLike } from "maplibre-gl";
import type { MapRef } from "react-map-gl/maplibre";

/** Wait for map load and a non-zero container before fitting bounds. */
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
        // Invalid bounds are non-fatal.
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
