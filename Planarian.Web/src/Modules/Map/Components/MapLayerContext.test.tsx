import { fireEvent, render, screen } from "@testing-library/react";
import { DEFAULT_HILLSHADE_PREFERENCES } from "./HillshadeStyle";
import { DEFAULT_USGS_HILLSHADE_VARIANT } from "./UsgsHillshade";
import { MapLayerProvider, useMapLayers } from "./MapLayerContext";

jest.mock("../../../Configuration/Context/AppContext", () => {
  const React = require("react");
  return {
    AppContext: React.createContext({ currentAccountId: "account-1" }),
  };
});

const Harness = () => {
  const layers = useMapLayers();
  return (
    <>
      <button onClick={() => layers.toggleLayer("usgs-24k-geology")}>
        24K
      </button>
      <button onClick={() => layers.toggleLayer("ngmdb-geology-group")}>
        Group
      </button>
      <button onClick={() => layers.toggleLayer("entrances")}>Entrances</button>
      <button onClick={() => layers.setHillshadeExaggeration(0.4)}>
        Set relief
      </button>
      <button onClick={() => layers.setHillshadeDirection(90)}>
        Set direction
      </button>
      <button onClick={() => layers.setHillshadeContrast(1.2)}>
        Set contrast
      </button>
      <button
        onClick={() =>
          layers.setHillshadePaletteColor("dark", "backgroundColor", "#112233")
        }
      >
        Set dark background
      </button>
      <button onClick={() => layers.resetHillshadePalette("dark")}>
        Reset dark palette
      </button>
      <button onClick={layers.resetHillshadeSettings}>Reset hillshade</button>
      <button
        onClick={() => layers.setUsgsHillshadeVariant("multidirectional")}
      >
        Set USGS multidirectional
      </button>
      <button onClick={() => layers.toggleLayer("hillshade-group")}>
        Hillshade group
      </button>
      <button onClick={() => layers.toggleLayer("usgs-hillshade")}>
        USGS hillshade
      </button>
      <button onClick={() => layers.toggleLayer("mapterhorn-hillshade")}>
        Mapterhorn hillshade
      </button>
      <span data-testid="24k">
        {String(layers.isVisible("usgs-24k-geology"))}
      </span>
      <span data-testid="48k">
        {String(layers.isVisible("usgs-48k-geology"))}
      </span>
      <span data-testid="entrances">
        {String(layers.isVisible("entrances"))}
      </span>
      <span data-testid="hillshade-relief">
        {layers.hillshadePreferences.exaggeration}
      </span>
      <span data-testid="hillshade-direction">
        {layers.hillshadePreferences.illuminationDirection}
      </span>
      <span data-testid="hillshade-contrast">
        {layers.hillshadePreferences.contrast}
      </span>
      <span data-testid="hillshade-dark-background">
        {layers.hillshadePreferences.paletteOverrides.dark?.backgroundColor ??
          "default"}
      </span>
      <span data-testid="usgs-hillshade-variant">
        {layers.usgsHillshadeVariant}
      </span>
      <span data-testid="hillshade-group-visible">
        {String(layers.isVisible("hillshade-group"))}
      </span>
      <span data-testid="usgs-hillshade-visible">
        {String(layers.isVisible("usgs-hillshade"))}
      </span>
      <span data-testid="mapterhorn-hillshade-visible">
        {String(layers.isVisible("mapterhorn-hillshade"))}
      </span>
    </>
  );
};

describe("MapLayerProvider", () => {
  beforeEach(() => localStorage.clear());

  test("restores the previously selected scales when a group is toggled back on", () => {
    render(
      <MapLayerProvider>
        <Harness />
      </MapLayerProvider>
    );

    fireEvent.click(screen.getByText("24K"));
    expect(screen.getByTestId("24k")).toHaveTextContent("true");
    expect(screen.getByTestId("48k")).toHaveTextContent("false");

    fireEvent.click(screen.getByText("Group"));
    expect(screen.getByTestId("24k")).toHaveTextContent("false");

    fireEvent.click(screen.getByText("Group"));
    expect(screen.getByTestId("24k")).toHaveTextContent("true");
    expect(screen.getByTestId("48k")).toHaveTextContent("false");
  });

  test("entrances are visible by default and can be toggled off", () => {
    render(
      <MapLayerProvider>
        <Harness />
      </MapLayerProvider>
    );

    expect(screen.getByTestId("entrances")).toHaveTextContent("true");
    fireEvent.click(screen.getByText("Entrances"));
    expect(screen.getByTestId("entrances")).toHaveTextContent("false");
  });

  test("defaults the hillshade group to USGS multidirectional only", () => {
    render(
      <MapLayerProvider>
        <Harness />
      </MapLayerProvider>
    );

    expect(screen.getByTestId("hillshade-group-visible")).toHaveTextContent(
      "true"
    );
    expect(screen.getByTestId("usgs-hillshade-visible")).toHaveTextContent(
      "true"
    );
    expect(
      screen.getByTestId("mapterhorn-hillshade-visible")
    ).toHaveTextContent("false");
    expect(screen.getByTestId("usgs-hillshade-variant")).toHaveTextContent(
      "multidirectional"
    );
  });

  test("restores the selected hillshade sources after the group is toggled", () => {
    render(
      <MapLayerProvider>
        <Harness />
      </MapLayerProvider>
    );

    fireEvent.click(screen.getByText("Mapterhorn hillshade"));
    fireEvent.click(screen.getByText("USGS hillshade"));
    expect(screen.getByTestId("usgs-hillshade-visible")).toHaveTextContent(
      "false"
    );
    expect(
      screen.getByTestId("mapterhorn-hillshade-visible")
    ).toHaveTextContent("true");

    fireEvent.click(screen.getByText("Hillshade group"));
    expect(screen.getByTestId("hillshade-group-visible")).toHaveTextContent(
      "false"
    );

    fireEvent.click(screen.getByText("Hillshade group"));
    expect(screen.getByTestId("usgs-hillshade-visible")).toHaveTextContent(
      "false"
    );
    expect(
      screen.getByTestId("mapterhorn-hillshade-visible")
    ).toHaveTextContent("true");
  });

  test("uses the default-visible hillshade source when no prior selection exists", () => {
    render(
      <MapLayerProvider>
        <Harness />
      </MapLayerProvider>
    );

    fireEvent.click(screen.getByText("USGS hillshade"));
    expect(screen.getByTestId("hillshade-group-visible")).toHaveTextContent(
      "false"
    );

    fireEvent.click(screen.getByText("Hillshade group"));
    expect(screen.getByTestId("usgs-hillshade-visible")).toHaveTextContent(
      "true"
    );
    expect(
      screen.getByTestId("mapterhorn-hillshade-visible")
    ).toHaveTextContent("false");
  });

  test("loads account-scoped hillshade settings and preserves custom colors", () => {
    localStorage.setItem(
      "planarianHillshadeSettings-v1-account-1",
      JSON.stringify({
        exaggeration: 0.35,
        illuminationDirection: 180,
        contrast: 1.1,
        paletteOverrides: {
          dark: { backgroundColor: "#112233" },
        },
      })
    );

    render(
      <MapLayerProvider>
        <Harness />
      </MapLayerProvider>
    );

    expect(screen.getByTestId("hillshade-relief")).toHaveTextContent("0.35");
    expect(screen.getByTestId("hillshade-direction")).toHaveTextContent("180");
    expect(screen.getByTestId("hillshade-contrast")).toHaveTextContent("1.1");
    expect(screen.getByTestId("hillshade-dark-background")).toHaveTextContent(
      "#112233"
    );
  });

  test("loads and persists the account-scoped USGS hillshade variant", () => {
    localStorage.setItem(
      "planarianUsgsHillshadeSettings-v2-account-1",
      JSON.stringify({ variant: "gray-stretch" })
    );

    render(
      <MapLayerProvider>
        <Harness />
      </MapLayerProvider>
    );

    expect(screen.getByTestId("usgs-hillshade-variant")).toHaveTextContent(
      "gray-stretch"
    );

    fireEvent.click(screen.getByText("Set USGS multidirectional"));

    expect(screen.getByTestId("usgs-hillshade-variant")).toHaveTextContent(
      "multidirectional"
    );
    expect(
      JSON.parse(
        localStorage.getItem("planarianUsgsHillshadeSettings-v2-account-1") ??
          "{}"
      )
    ).toEqual({ variant: "multidirectional" });
  });

  test("falls back to the default USGS variant for invalid saved settings", () => {
    localStorage.setItem(
      "planarianUsgsHillshadeSettings-v2-account-1",
      JSON.stringify({ variant: "invalid" })
    );

    render(
      <MapLayerProvider>
        <Harness />
      </MapLayerProvider>
    );

    expect(screen.getByTestId("usgs-hillshade-variant")).toHaveTextContent(
      DEFAULT_USGS_HILLSHADE_VARIANT
    );
  });

  test("persists hillshade changes and supports palette and full resets", () => {
    render(
      <MapLayerProvider>
        <Harness />
      </MapLayerProvider>
    );

    fireEvent.click(screen.getByText("Set relief"));
    fireEvent.click(screen.getByText("Set direction"));
    fireEvent.click(screen.getByText("Set contrast"));
    fireEvent.click(screen.getByText("Set dark background"));

    const stored = JSON.parse(
      localStorage.getItem("planarianHillshadeSettings-v1-account-1") ?? "{}"
    );
    expect(stored).toMatchObject({
      exaggeration: 0.4,
      illuminationDirection: 90,
      contrast: 1.2,
      paletteOverrides: {
        dark: { backgroundColor: "#112233" },
      },
    });

    fireEvent.click(screen.getByText("Reset dark palette"));
    expect(screen.getByTestId("hillshade-dark-background")).toHaveTextContent(
      "default"
    );

    fireEvent.click(screen.getByText("Reset hillshade"));
    expect(screen.getByTestId("hillshade-relief")).toHaveTextContent(
      String(DEFAULT_HILLSHADE_PREFERENCES.exaggeration)
    );
    expect(screen.getByTestId("hillshade-direction")).toHaveTextContent(
      String(DEFAULT_HILLSHADE_PREFERENCES.illuminationDirection)
    );
    expect(screen.getByTestId("hillshade-contrast")).toHaveTextContent(
      String(DEFAULT_HILLSHADE_PREFERENCES.contrast)
    );
  });
});
