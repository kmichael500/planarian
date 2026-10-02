import { render, screen } from "@testing-library/react";
import { MapLayers } from "./MapLayers";

let mockMapCurrent: any;

jest.mock("react-map-gl/maplibre", () => ({
  Source: (props: any) => (
    <div
      data-testid={`source-${props.id}`}
      data-has-minzoom={String(Object.prototype.hasOwnProperty.call(props, "minzoom"))}
      data-has-maxzoom={String(Object.prototype.hasOwnProperty.call(props, "maxzoom"))}
      data-minzoom={props.minzoom}
      data-maxzoom={props.maxzoom}
    >
      {props.children}
    </div>
  ),
  Layer: (props: any) => (
    <div
      data-testid={`layer-${props.id}`}
      data-has-minzoom={String(Object.prototype.hasOwnProperty.call(props, "minzoom"))}
      data-has-maxzoom={String(Object.prototype.hasOwnProperty.call(props, "maxzoom"))}
      data-minzoom={props.minzoom}
      data-maxzoom={props.maxzoom}
      data-before-id={props.beforeId}
      data-paint={JSON.stringify(props.paint)}
    />
  ),
  useMap: () => ({ current: mockMapCurrent }),
}));

jest.mock("./MapLayerContext", () => ({
  useMapLayers: () => ({
    isVisible: () => false,
    opacity: (id: string) => id === "mapterhorn-hillshade" ? 0.4 : 1,
    terrainEnabled: false,
    terrainExaggeration: 1,
  }),
}));

describe("MapLayers optional zoom props", () => {
  test("omits undefined zoom props while preserving configured zoom limits", () => {
    render(<MapLayers includeDataOverlays={false} />);

    expect(screen.getByTestId("source-osm-street")).toHaveAttribute("data-has-minzoom", "false");
    expect(screen.getByTestId("source-osm-street")).toHaveAttribute("data-has-maxzoom", "false");
    expect(screen.getByTestId("layer-osm-street")).toHaveAttribute("data-has-minzoom", "false");
    expect(screen.getByTestId("layer-osm-street")).toHaveAttribute("data-has-maxzoom", "false");
    expect(screen.getByTestId("layer-osm-street")).toHaveAttribute("data-before-id", "planarian-overlay-anchor");

    expect(screen.getByTestId("source-usgs-500k-geology")).toHaveAttribute("data-has-minzoom", "true");
    expect(screen.getByTestId("source-usgs-500k-geology")).toHaveAttribute("data-has-maxzoom", "true");
    expect(screen.getByTestId("source-usgs-500k-geology")).toHaveAttribute("data-minzoom", "4");
    expect(screen.getByTestId("source-usgs-500k-geology")).toHaveAttribute("data-maxzoom", "12");
    expect(screen.getByTestId("layer-usgs-500k-geology")).toHaveAttribute("data-has-minzoom", "false");
    expect(screen.getByTestId("layer-usgs-500k-geology")).toHaveAttribute("data-has-maxzoom", "false");

    expect(screen.getByTestId("source-regrid-parcel-boundaries")).toHaveAttribute("data-has-minzoom", "false");
    expect(screen.getByTestId("source-regrid-parcel-boundaries")).toHaveAttribute("data-has-maxzoom", "false");
    expect(screen.getByTestId("layer-regrid-parcel-boundaries")).toHaveAttribute("data-has-minzoom", "true");
    expect(screen.getByTestId("layer-regrid-parcel-boundaries")).toHaveAttribute("data-has-maxzoom", "true");
    expect(screen.getByTestId("layer-regrid-parcel-boundaries")).toHaveAttribute("data-minzoom", "15");
    expect(screen.getByTestId("layer-regrid-parcel-boundaries")).toHaveAttribute("data-maxzoom", "17");
    expect(screen.getByTestId("layer-regrid-parcel-boundaries")).toHaveAttribute("data-before-id", "planarian-overlay-anchor");

    expect(JSON.parse(screen.getByTestId("layer-mapterhorn-hillshade").getAttribute("data-paint") || "{}"))
      .toEqual({
        "hillshade-exaggeration": 1,
        "hillshade-shadow-color": "rgba(0, 0, 0, 0.4)",
        "hillshade-highlight-color": "rgba(255, 255, 255, 0.4)",
        "hillshade-accent-color": "rgba(0, 0, 0, 0.4)",
      });
  });
});


describe("MapLayers terrain lifecycle", () => {
  afterEach(() => {
    mockMapCurrent = undefined;
  });

  test("does not call the terrain API before a new map style is loaded", () => {
    let styleLoaded = false;
    let loadHandler: (() => void) | undefined;
    const setTerrain = jest.fn();
    const instance = {
      isStyleLoaded: jest.fn(() => styleLoaded),
      once: jest.fn((event: string, handler: () => void) => {
        if (event === "load") loadHandler = handler;
      }),
      off: jest.fn(),
      getTerrain: jest.fn(() => null),
      getSource: jest.fn(),
      setTerrain,
    };
    mockMapCurrent = { getMap: () => instance };

    render(<MapLayers includeDataOverlays={false} />);

    expect(setTerrain).not.toHaveBeenCalled();
    expect(instance.once).toHaveBeenCalledWith("load", expect.any(Function));

    styleLoaded = true;
    loadHandler?.();
    expect(setTerrain).not.toHaveBeenCalled();
  });
});
