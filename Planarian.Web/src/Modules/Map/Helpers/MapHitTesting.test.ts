import type { MapGeoJSONFeature } from "maplibre-gl";
import {
  entranceHitRadiusPixels,
  entranceVisibleMinZoom,
  findNearbyEntranceCaveId,
} from "./MapHitTesting";

const entranceFeature = (
  caveId: string,
  x: number,
  y: number
): MapGeoJSONFeature =>
  ({
    properties: { CaveId: caveId },
    geometry: { type: "Point", coordinates: [x, y] },
  } as unknown as MapGeoJSONFeature);

const createMap = (features: MapGeoJSONFeature[], zoom = 12) => {
  const getLayer = jest.fn<{ id: string } | undefined, []>(() => ({
    id: "entrances",
  }));
  const getZoom = jest.fn(() => zoom);
  const project = jest.fn(([longitude, latitude]: [number, number]) => ({
    x: longitude,
    y: latitude,
  }));
  const queryRenderedFeatures = jest.fn().mockReturnValue(features);

  const map = {
    getLayer,
    getZoom,
    project,
    queryRenderedFeatures,
  } as unknown as NonNullable<Parameters<typeof findNearbyEntranceCaveId>[0]>;

  return { map, getLayer, getZoom, project, queryRenderedFeatures };
};

describe("findNearbyEntranceCaveId", () => {
  test("expands entrance selection to the configured hit radius", () => {
    const { map } = createMap([entranceFeature("nearby", 113, 100)], 10);

    expect(findNearbyEntranceCaveId(map, { x: 100, y: 100 })).toBe("nearby");
  });

  test("queries the entrance layer around the tap and selects the nearest cave", () => {
    const { map, queryRenderedFeatures } = createMap([
      entranceFeature("farther", 112, 100),
      entranceFeature("nearest", 103, 104),
    ]);

    expect(findNearbyEntranceCaveId(map, { x: 100, y: 100 })).toBe("nearest");
    expect(queryRenderedFeatures).toHaveBeenCalledWith(
      [
        [100 - entranceHitRadiusPixels, 100 - entranceHitRadiusPixels],
        [100 + entranceHitRadiusPixels, 100 + entranceHitRadiusPixels],
      ],
      { layers: ["entrances"] }
    );
  });

  test("rejects a cave outside the circular tolerance", () => {
    const { map } = createMap([entranceFeature("corner", 110, 110)]);

    expect(findNearbyEntranceCaveId(map, { x: 100, y: 100 })).toBeUndefined();
  });

  test("does not query entrances while their circle layer is hidden by zoom", () => {
    const { map, queryRenderedFeatures } = createMap(
      [entranceFeature("hidden", 100, 100)],
      entranceVisibleMinZoom - 0.1
    );

    expect(findNearbyEntranceCaveId(map, { x: 100, y: 100 })).toBeUndefined();
    expect(queryRenderedFeatures).not.toHaveBeenCalled();
  });

  test("does not query a layer that has not been added to the map style yet", () => {
    const { map, getLayer, queryRenderedFeatures } = createMap([
      entranceFeature("not-ready", 100, 100),
    ]);
    getLayer.mockReturnValue(undefined);

    expect(findNearbyEntranceCaveId(map, { x: 100, y: 100 })).toBeUndefined();
    expect(queryRenderedFeatures).not.toHaveBeenCalled();
  });
});
