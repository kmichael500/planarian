import { fireEvent, render, screen } from "@testing-library/react";
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
      <button onClick={() => layers.toggleLayer("usgs-24k-geology")}>24K</button>
      <button onClick={() => layers.toggleLayer("ngmdb-geology-group")}>Group</button>
      <button onClick={() => layers.toggleLayer("entrances")}>Entrances</button>
      <span data-testid="24k">{String(layers.isVisible("usgs-24k-geology"))}</span>
      <span data-testid="48k">{String(layers.isVisible("usgs-48k-geology"))}</span>
      <span data-testid="entrances">{String(layers.isVisible("entrances"))}</span>
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

});
