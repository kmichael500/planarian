import React from "react";
import { render, screen, within } from "@testing-library/react";
import { MapLayerProvider } from "./MapLayerContext";
import { MapLayerControl } from "./MapLayerControl";

jest.mock("../../../Configuration/Context/AppContext", () => {
  const React = require("react");
  return {
    AppContext: React.createContext({ currentAccountId: "account-1" }),
  };
});

jest.mock("../../../Shared/Components/Buttons/PlanarianButtton", () => ({
  PlanarianButton: ({ children }: any) => <button>{children}</button>,
}));

jest.mock("../../../Shared/Components/Buttons/PlanarianModal", () => ({
  PlanarianModal: () => null,
}));

describe("MapLayerControl organization", () => {
  beforeEach(() => localStorage.clear());

  test("separates cave data and keeps hydrology after base layers", () => {
    render(
      <MapLayerProvider>
        <MapLayerControl position={{ top: "0", right: "0" }} />
      </MapLayerProvider>
    );

    const layersControl = screen.getByLabelText("Map layers").parentElement!;
    const mapDataControl = screen.getByLabelText("Map data").parentElement!;

    expect(within(layersControl).queryByText("Cave Entrances")).not.toBeInTheDocument();
    expect(within(layersControl).queryByText("View", { exact: true })).not.toBeInTheDocument();
    expect(within(mapDataControl).getByText("Cave Entrances")).toBeInTheDocument();
    expect(within(mapDataControl).getByText("Line Plots")).toBeInTheDocument();
    const mapDataText = mapDataControl.textContent ?? "";
    expect(mapDataText.indexOf("Cave Entrances")).toBeLessThan(mapDataText.indexOf("Line Plots"));
    expect(within(mapDataControl).queryByText("Cave Data", { exact: true })).not.toBeInTheDocument();
    expect(within(mapDataControl).queryByText("Map Data", { exact: true })).not.toBeInTheDocument();

    const layersText = layersControl.textContent ?? "";
    expect(within(layersControl).queryByText("Base Layers", { exact: true })).not.toBeInTheDocument();
    expect(layersText.indexOf("Hydrology")).toBeLessThan(layersText.indexOf("Reference"));
    expect(layersText.indexOf("NGMDB Geology")).toBeLessThan(layersText.indexOf("Hillshade"));
    expect(layersText.indexOf("Hillshade")).toBeLessThan(layersText.indexOf("Hydrology"));
  });
  test("omits unsupported map-data layers without hiding supported ones", () => {
    render(
      <MapLayerProvider>
        <MapLayerControl excludedLayerIds={["line-plots"]} />
      </MapLayerProvider>
    );

    const mapDataControl = screen.getByLabelText("Map data").parentElement!;
    expect(within(mapDataControl).getByText("Cave Entrances")).toBeInTheDocument();
    expect(within(mapDataControl).queryByText("Line Plots")).not.toBeInTheDocument();
  });

});
