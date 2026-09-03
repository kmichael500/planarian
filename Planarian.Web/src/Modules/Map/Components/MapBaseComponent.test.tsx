import React from "react";
import { act, render, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AppOptions } from "../../../Shared/Services/AppService";
import { MapBaseComponent } from "./MapBaseComponent";

let mockMapOnClick: ((event: any) => void) | undefined;

const mockMapApi = {
  getLayer: jest.fn(() => ({ id: "entrances" })),
  getZoom: jest.fn(() => 10),
  project: jest.fn(() => ({ x: 112, y: 100 })),
  queryRenderedFeatures: jest.fn(),
};

jest.mock("react-map-gl/maplibre", () => {
  const React = require("react");

  return {
    Map: React.forwardRef((props: any, ref: React.Ref<unknown>) => {
      React.useImperativeHandle(ref, () => mockMapApi);
      mockMapOnClick = props.onClick;
      return <div data-testid="map">{props.children}</div>;
    }),
    MapProvider: ({ children }: any) => <>{children}</>,
    Source: ({ children }: any) => <>{children}</>,
    Layer: () => null,
    GeolocateControl: () => null,
    NavigationControl: () => null,
    ScaleControl: () => null,
    Popup: ({ children }: any) => <>{children}</>,
  };
});

jest.mock("./LayerControl", () => ({ LayerControl: () => null }));
jest.mock("./CaveSearchMapControl", () => ({
  CaveSearchMapControl: () => null,
}));
jest.mock("./FullScreenControl", () => ({ FullScreenControl: () => null }));
jest.mock("../../Caves/Components/CaveAdvancedSearchDrawer", () => ({
  CaveAdvancedSearchDrawer: () => null,
}));

const entranceFeature = (caveId: string) => ({
  properties: { CaveId: caveId },
  geometry: { type: "Point", coordinates: [-87, 35] },
});

const renderMap = (onCaveClicked: jest.Mock, onNonCaveClicked: jest.Mock) =>
  render(
    <MemoryRouter>
      <MapBaseComponent
        initialCenter={[35, -87]}
        initialZoom={10}
        onCaveClicked={onCaveClicked}
        onNonCaveClicked={onNonCaveClicked}
        showSearchBar={false}
        showGeolocateControl={false}
        manageBodyPadding={false}
      />
    </MemoryRouter>
  );

const clickMap = () => {
  if (!mockMapOnClick) {
    throw new Error("Map click handler was not rendered.");
  }

  act(() => {
    mockMapOnClick?.({
      features: [],
      point: { x: 100, y: 100 },
      lngLat: { lat: 35.1, lng: -87.1 },
      originalEvent: { target: document.body },
    });
  });
};

describe("MapBaseComponent entrance hit tolerance", () => {
  const originalApiBaseUrl = AppOptions.apiBaseUrl;

  beforeAll(() => {
    AppOptions.apiBaseUrl = "https://api.example.test";
  });

  afterAll(() => {
    AppOptions.apiBaseUrl = originalApiBaseUrl;
  });

  beforeEach(() => {
    mockMapOnClick = undefined;
    mockMapApi.getLayer.mockReturnValue({ id: "entrances" });
    mockMapApi.getZoom.mockReturnValue(10);
    mockMapApi.project.mockReturnValue({ x: 112, y: 100 });
    mockMapApi.queryRenderedFeatures.mockReset();
  });

  test("opens a nearby cave instead of treating a slightly missed entrance as a location click", async () => {
    const onCaveClicked = jest.fn();
    const onNonCaveClicked = jest.fn();
    mockMapApi.queryRenderedFeatures.mockReturnValue([
      entranceFeature("nearby-cave"),
    ]);

    renderMap(onCaveClicked, onNonCaveClicked);
    await waitFor(() => expect(mockMapOnClick).toBeDefined());

    clickMap();

    expect(onCaveClicked).toHaveBeenCalledWith("nearby-cave");
    expect(onNonCaveClicked).not.toHaveBeenCalled();
  });

  test("preserves the location-click fallback when no entrance is close enough", async () => {
    const onCaveClicked = jest.fn();
    const onNonCaveClicked = jest.fn();
    mockMapApi.queryRenderedFeatures.mockReturnValue([]);

    renderMap(onCaveClicked, onNonCaveClicked);
    await waitFor(() => expect(mockMapOnClick).toBeDefined());

    clickMap();

    expect(onCaveClicked).not.toHaveBeenCalled();
    expect(onNonCaveClicked).toHaveBeenCalledWith(35.1, -87.1);
  });
});
