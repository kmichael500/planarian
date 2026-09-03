import type { MapRef } from "react-map-gl/maplibre";

export const entranceHitRadiusPixels = 14;
export const entranceVisibleMinZoom = 9;

type MapHitTestApi = Pick<
  MapRef,
  "getLayer" | "getZoom" | "project" | "queryRenderedFeatures"
>;

type ScreenPoint = {
  x: number;
  y: number;
};

export const findNearbyEntranceCaveId = (
  map: MapHitTestApi | null | undefined,
  point: ScreenPoint,
  radius = entranceHitRadiusPixels
): string | undefined => {
  if (
    !map ||
    map.getZoom() < entranceVisibleMinZoom ||
    !map.getLayer("entrances")
  ) {
    return undefined;
  }

  const features = map.queryRenderedFeatures(
    [
      [point.x - radius, point.y - radius],
      [point.x + radius, point.y + radius],
    ],
    { layers: ["entrances"] }
  );

  let nearestCaveId: string | undefined;
  let nearestDistanceSquared = Number.POSITIVE_INFINITY;
  const hitRadiusSquared = radius * radius;

  for (const feature of features) {
    const caveId = feature.properties?.CaveId;
    if (typeof caveId !== "string" || caveId.length === 0) continue;
    if (feature.geometry.type !== "Point") continue;

    const [longitude, latitude] = feature.geometry.coordinates;
    const featurePoint = map.project([longitude, latitude]);
    const dx = featurePoint.x - point.x;
    const dy = featurePoint.y - point.y;
    const distanceSquared = dx * dx + dy * dy;

    if (distanceSquared > hitRadiusSquared) continue;

    if (distanceSquared < nearestDistanceSquared) {
      nearestDistanceSquared = distanceSquared;
      nearestCaveId = caveId;
    }
  }

  return nearestCaveId;
};
