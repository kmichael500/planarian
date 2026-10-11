import React from "react";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { MapLayerProvider } from "./MapLayerContext";
import { MapLayerControl } from "./MapLayerControl";

jest.mock("../../../Configuration/Context/AppContext", () => {
  const React = require("react");
  return {
    AppContext: React.createContext({ currentAccountId: "account-1" }),
  };
});

jest.mock("../../../Shared/Components/Buttons/PlanarianButtton", () => ({
  PlanarianButton: ({ children, block, onClick }: any) => (
    <button data-block={String(Boolean(block))} onClick={onClick}>
      {children}
    </button>
  ),
}));

jest.mock("../../../Shared/Components/Buttons/PlanarianModal", () => ({
  PlanarianModal: () => null,
}));

describe("MapLayerControl organization", () => {
  beforeEach(() => localStorage.clear());

  test("separates cave data and keeps hillshade before hydrology", () => {
    render(
      <MapLayerProvider>
        <MapLayerControl position={{ top: "0", right: "0" }} />
      </MapLayerProvider>
    );

    const layersControl = screen.getByLabelText("Map layers").parentElement!;
    const mapDataControl = screen.getByLabelText("Map data").parentElement!;

    expect(
      within(layersControl).queryByText("Cave Entrances")
    ).not.toBeInTheDocument();
    expect(
      within(layersControl).queryByText("View", { exact: true })
    ).not.toBeInTheDocument();
    expect(
      within(mapDataControl).getByText("Cave Entrances")
    ).toBeInTheDocument();
    expect(within(mapDataControl).getByText("Line Plots")).toBeInTheDocument();

    const mapDataText = mapDataControl.textContent ?? "";
    expect(mapDataText.indexOf("Cave Entrances")).toBeLessThan(
      mapDataText.indexOf("Line Plots")
    );
    expect(
      within(mapDataControl).queryByText("Cave Data", { exact: true })
    ).not.toBeInTheDocument();
    expect(
      within(mapDataControl).queryByText("Map Data", { exact: true })
    ).not.toBeInTheDocument();

    const layersText = layersControl.textContent ?? "";
    expect(
      within(layersControl).queryByText("Base Layers", { exact: true })
    ).not.toBeInTheDocument();
    expect(layersText.indexOf("NGMDB Geology")).toBeLessThan(
      layersText.indexOf("Hillshade")
    );
    expect(layersText.indexOf("Hillshade")).toBeLessThan(
      layersText.indexOf("Hydrology")
    );
    expect(layersText.indexOf("Hydrology")).toBeLessThan(
      layersText.indexOf("Reference")
    );
  });

  test("groups USGS and Mapterhorn under one enabled Hillshade item", () => {
    render(
      <MapLayerProvider>
        <MapLayerControl position={{ top: "0", right: "0" }} />
      </MapLayerProvider>
    );

    const layersControl = screen.getByLabelText("Map layers").parentElement!;
    const hillshadeCheckbox = within(layersControl).getByLabelText("Hillshade");
    const usgsCheckbox = within(layersControl).getByLabelText("USGS");
    const mapterhornCheckbox =
      within(layersControl).getByLabelText("Mapterhorn");

    expect(hillshadeCheckbox).toBeChecked();
    expect(usgsCheckbox).toBeChecked();
    expect(mapterhornCheckbox).not.toBeChecked();
    expect(
      within(layersControl).getByLabelText("USGS hillshade opacity")
    ).toBeInTheDocument();
    expect(
      within(layersControl).getByLabelText("USGS hillshade variant")
    ).toHaveValue("multidirectional");
    expect(
      within(layersControl).queryByText("Relief strength")
    ).not.toBeInTheDocument();
  });

  test("shows source-specific controls only for enabled hillshade sources", () => {
    render(
      <MapLayerProvider>
        <MapLayerControl position={{ top: "0", right: "0" }} />
      </MapLayerProvider>
    );

    const layersControl = screen.getByLabelText("Map layers").parentElement!;
    const mapterhornCheckbox =
      within(layersControl).getByLabelText("Mapterhorn");

    fireEvent.click(mapterhornCheckbox);

    expect(
      within(layersControl).getByLabelText("Mapterhorn hillshade opacity")
    ).toBeInTheDocument();
    expect(
      within(layersControl).getByText("Relief strength")
    ).toBeInTheDocument();
    expect(within(layersControl).getByText("Contrast")).toBeInTheDocument();

    fireEvent.click(within(layersControl).getByLabelText("USGS"));

    expect(
      within(layersControl).queryByLabelText("USGS hillshade opacity")
    ).not.toBeInTheDocument();
    expect(
      within(layersControl).queryByLabelText("USGS hillshade variant")
    ).not.toBeInTheDocument();
    expect(
      within(layersControl).getByLabelText("Mapterhorn hillshade opacity")
    ).toBeInTheDocument();
  });

  test("uses a full-width terrain button and natural-scale vertical controls", () => {
    render(
      <MapLayerProvider>
        <MapLayerControl position={{ top: "0", right: "0" }} />
      </MapLayerProvider>
    );

    const layersControl = screen.getByLabelText("Map layers").parentElement!;
    const enableButton = within(layersControl).getByRole("button", {
      name: "Enable 3D Terrain",
    });

    expect(enableButton).toHaveAttribute("data-block", "true");
    expect(
      within(layersControl).queryByText("Vertical scale")
    ).not.toBeInTheDocument();

    fireEvent.click(enableButton);

    expect(
      within(layersControl).getByRole("button", {
        name: "Disable 3D Terrain",
      })
    ).toHaveAttribute("data-block", "true");
    expect(
      within(layersControl).getByText("Vertical scale")
    ).toBeInTheDocument();
    expect(
      within(layersControl).getByLabelText("3D terrain vertical scale")
    ).toHaveValue("1.0");
    expect(within(layersControl).getByText("×")).toBeInTheDocument();

    const slider = within(layersControl).getByLabelText(
      "3D terrain vertical scale slider"
    );
    expect(slider).toHaveAttribute("aria-valuemin", "0");
    expect(slider).toHaveAttribute("aria-valuemax", "3");
    expect(slider).toHaveAttribute("aria-valuenow", "1");
    expect(
      within(layersControl).queryByText(/Exaggeration/)
    ).not.toBeInTheDocument();
  });

  test("omits unsupported map-data layers without hiding supported ones", () => {
    render(
      <MapLayerProvider>
        <MapLayerControl excludedLayerIds={["line-plots"]} />
      </MapLayerProvider>
    );

    const mapDataControl = screen.getByLabelText("Map data").parentElement!;
    expect(
      within(mapDataControl).getByText("Cave Entrances")
    ).toBeInTheDocument();
    expect(
      within(mapDataControl).queryByText("Line Plots")
    ).not.toBeInTheDocument();
  });
});
