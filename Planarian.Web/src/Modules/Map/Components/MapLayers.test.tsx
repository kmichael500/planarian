import { render, screen } from "@testing-library/react";
import { getHillshadeStyle } from "./HillshadeStyle";
import { MapLayers } from "./MapLayers";

let mockMapCurrent: any;
let mockTheme: "light" | "dark" = "light";
let mockUsgsHillshadeVariant: "gray" | "gray-stretch" | "multidirectional" =
  "multidirectional";
const mockHillshadePreferences = {
  exaggeration: 0.4,
  illuminationDirection: 90,
  contrast: 1.2,
  paletteOverrides: {},
};

jest.mock("../../../ThemeProvider", () => ({
  useTheme: () => ({ effectiveMode: mockTheme }),
}));

jest.mock("./HillshadeStyle", () => ({
  getHillshadeStyle: jest.fn(),
}));

const mockGetHillshadeStyle = getHillshadeStyle as jest.MockedFunction<
  typeof getHillshadeStyle
>;

beforeEach(() => {
  mockMapCurrent = undefined;
  mockTheme = "light";
  mockUsgsHillshadeVariant = "multidirectional";
  mockGetHillshadeStyle.mockImplementation(
    (theme, opacity) =>
      ({
        backgroundColor: `${theme}-background`,
        paint: { "hillshade-exaggeration": opacity },
      } as ReturnType<typeof getHillshadeStyle>)
  );
});

jest.mock("react-map-gl/maplibre", () => ({
  Source: (props: any) => (
    <div
      data-testid={`source-${props.id}`}
      data-has-minzoom={String(
        Object.prototype.hasOwnProperty.call(props, "minzoom")
      )}
      data-has-maxzoom={String(
        Object.prototype.hasOwnProperty.call(props, "maxzoom")
      )}
      data-minzoom={props.minzoom}
      data-maxzoom={props.maxzoom}
      data-tiles={props.tiles ? JSON.stringify(props.tiles) : undefined}
    >
      {props.children}
    </div>
  ),
  Layer: (props: any) => (
    <div
      data-testid={`layer-${props.id}`}
      data-has-minzoom={String(
        Object.prototype.hasOwnProperty.call(props, "minzoom")
      )}
      data-has-maxzoom={String(
        Object.prototype.hasOwnProperty.call(props, "maxzoom")
      )}
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
    opacity: (id: string) => (id === "mapterhorn-hillshade" ? 0.4 : 1),
    terrainEnabled: false,
    terrainExaggeration: 1,
    hillshadePreferences: mockHillshadePreferences,
    usgsHillshadeVariant: mockUsgsHillshadeVariant,
  }),
}));

describe("MapLayers raster overzooming", () => {
  test("keeps native tile limits on sources instead of hiding layers above them", () => {
    render(<MapLayers includeDataOverlays={false} />);

    expect(screen.getByTestId("source-osm-street")).toHaveAttribute(
      "data-maxzoom",
      "19"
    );
    expect(screen.getByTestId("source-open-topo")).toHaveAttribute(
      "data-maxzoom",
      "17"
    );
    expect(screen.getByTestId("source-macrostrat")).toHaveAttribute(
      "data-maxzoom",
      "17"
    );

    expect(screen.getByTestId("source-usgs-500k-geology")).toHaveAttribute(
      "data-minzoom",
      "4"
    );
    expect(screen.getByTestId("source-usgs-500k-geology")).toHaveAttribute(
      "data-maxzoom",
      "12"
    );
    expect(screen.getByTestId("layer-usgs-500k-geology")).toHaveAttribute(
      "data-has-maxzoom",
      "false"
    );

    expect(
      screen.getByTestId("source-regrid-parcel-boundaries")
    ).toHaveAttribute("data-maxzoom", "17");
    expect(
      screen.getByTestId("layer-regrid-parcel-boundaries")
    ).toHaveAttribute("data-minzoom", "15");
    expect(
      screen.getByTestId("layer-regrid-parcel-boundaries")
    ).toHaveAttribute("data-has-maxzoom", "false");
    expect(
      screen.getByTestId("layer-regrid-parcel-boundaries")
    ).toHaveAttribute("data-before-id", "planarian-overlay-anchor");
  });

  test("asks Esri to return missing imagery as an error so MapLibre can fall back to a parent tile", () => {
    render(<MapLayers includeDataOverlays={false} />);

    const tiles = screen
      .getByTestId("source-esri-satellite")
      .getAttribute("data-tiles");

    expect(tiles).toContain("blankTile=false");
  });
});

describe("MapLayers USGS hillshade integration", () => {
  test("applies the selected variant when the raster source becomes available", () => {
    const setTiles = jest.fn();
    let styleDataHandler: (() => void) | undefined;
    let sourceReady = false;
    const instance = {
      isStyleLoaded: jest.fn(() => true),
      once: jest.fn(),
      on: jest.fn((event: string, handler: () => void) => {
        if (event === "styledata") styleDataHandler = handler;
      }),
      off: jest.fn(),
      getTerrain: jest.fn(() => null),
      setTerrain: jest.fn(),
      getSource: jest.fn((id: string) =>
        id === "usgs-hillshade" && sourceReady ? { setTiles } : undefined
      ),
    };
    mockMapCurrent = { getMap: () => instance };
    mockUsgsHillshadeVariant = "gray-stretch";

    render(<MapLayers includeDataOverlays={false} />);

    expect(setTiles).not.toHaveBeenCalled();
    expect(instance.on).toHaveBeenCalledWith("styledata", expect.any(Function));

    sourceReady = true;
    styleDataHandler?.();

    expect(
      decodeURIComponent(setTiles.mock.calls.at(-1)?.[0]?.[0] ?? "")
    ).toContain('"rasterFunction":"Hillshade Gray-Stretch"');
    expect(instance.off).toHaveBeenCalledWith(
      "styledata",
      expect.any(Function)
    );
  });

  test("pushes variant changes into MapLibre's raster source", () => {
    const setTiles = jest.fn();
    const instance = {
      isStyleLoaded: jest.fn(() => true),
      once: jest.fn(),
      off: jest.fn(),
      getTerrain: jest.fn(() => null),
      setTerrain: jest.fn(),
      getSource: jest.fn((id: string) =>
        id === "usgs-hillshade" ? { setTiles } : undefined
      ),
    };
    mockMapCurrent = { getMap: () => instance };
    mockUsgsHillshadeVariant = "gray";

    const { rerender } = render(<MapLayers includeDataOverlays={false} />);

    expect(setTiles).toHaveBeenCalled();
    expect(
      decodeURIComponent(setTiles.mock.calls.at(-1)?.[0]?.[0] ?? "")
    ).toContain('"rasterFunction":"Hillshade Gray"');

    mockUsgsHillshadeVariant = "gray-stretch";
    rerender(<MapLayers includeDataOverlays={false} />);

    expect(
      decodeURIComponent(setTiles.mock.calls.at(-1)?.[0]?.[0] ?? "")
    ).toContain('"rasterFunction":"Hillshade Gray-Stretch"');
  });
});

describe("MapLayers hillshade integration", () => {
  afterEach(() => {
    mockTheme = "light";
  });

  test("passes the effective theme and layer opacity to the hillshade style owner", () => {
    render(<MapLayers includeDataOverlays={false} />);

    expect(mockGetHillshadeStyle).toHaveBeenCalledWith(
      "light",
      0.4,
      mockHillshadePreferences
    );
    expect(
      JSON.parse(
        screen
          .getByTestId("layer-planarian-map-background")
          .getAttribute("data-paint") || "{}"
      )
    ).toEqual({ "background-color": "light-background" });
    expect(
      JSON.parse(
        screen
          .getByTestId("layer-mapterhorn-hillshade")
          .getAttribute("data-paint") || "{}"
      )
    ).toEqual({ "hillshade-exaggeration": 0.4 });
  });

  test("recomputes hillshade styling for dark mode", () => {
    mockTheme = "dark";
    render(<MapLayers includeDataOverlays={false} />);

    expect(mockGetHillshadeStyle).toHaveBeenCalledWith(
      "dark",
      0.4,
      mockHillshadePreferences
    );
    expect(
      JSON.parse(
        screen
          .getByTestId("layer-planarian-map-background")
          .getAttribute("data-paint") || "{}"
      )
    ).toEqual({ "background-color": "dark-background" });
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
      on: jest.fn(),
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
