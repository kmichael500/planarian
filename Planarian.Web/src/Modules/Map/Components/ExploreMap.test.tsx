import React from "react";
import { act, render, screen, waitFor } from "@testing-library/react";
import { AppOptions } from "../../../Shared/Services/AppService";
import { CaveService } from "../../Caves/Service/CaveService";
import { ExploreMap } from "./ExploreMap";

let mockMapOnClick: ((event: any) => void) | undefined;
let mockHydrologyOverlayHit = false;
const mockQueryRenderedFeatures = jest.fn();
const mockMapApi = {
  getMap: () => mockMapApi,
  getLayer: jest.fn(() => ({ id: "entrances" })),
  getZoom: jest.fn(() => 10),
  project: jest.fn(() => ({ x: 112, y: 100 })),
  queryRenderedFeatures: mockQueryRenderedFeatures,
};

jest.mock("../../../Configuration/Context/AppContext", () => {
  const React = require("react");
  return {
    AppContext: React.createContext({
      currentAccountId: "account-1",
      currentAccountName: "Test account",
      setHideBodyPadding: jest.fn(),
      hideBodyPadding: true,
    }),
  };
});

jest.mock("react-map-gl/maplibre", () => ({
  Popup: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

jest.mock("./PlanarianBaseMap", () => {
  const React = require("react");
  return {
    PlanarianBaseMap: React.forwardRef((props: any, ref: React.Ref<unknown>) => {
      React.useImperativeHandle(ref, () => mockMapApi);
      mockMapOnClick = props.onClick;
      return <div data-testid="map">{props.children}</div>;
    }),
  };
});

jest.mock("./MapLayerContext", () => ({ MapLayerProvider: ({ children }: any) => <>{children}</> }));
jest.mock("./MapLayers", () => ({ MapLayers: () => null }));
jest.mock("./MapEntranceTileLayer", () => ({
  MAP_ENTRANCE_LAYER_ID: "entrances",
  MapEntranceTileLayer: () => null,
}));
jest.mock("./MapLinePlotLayer", () => ({ MapLinePlotLayer: () => null }));
jest.mock("./CaveSearchMapControl", () => ({ CaveSearchMapControl: () => null }));
jest.mock("./ExploreMapFilters", () => ({ ExploreMapFilters: () => null }));
jest.mock("./MapLayerControl", () => ({ MapLayerControl: () => null }));
jest.mock("./MapHydrologyOverlayLayer", () => ({
  hasHydrologyOverlayFeatureAtPoint: () => mockHydrologyOverlayHit,
}));
jest.mock("./MapClickCaveModal", () => ({ MapClickCaveModal: () => null }));
jest.mock("./MapClickPointModal", () => ({
  MapClickPointModal: ({ isModalVisible }: { isModalVisible: boolean }) =>
    isModalVisible ? <div data-testid="point-modal" /> : null,
}));
jest.mock("../../Caves/Service/CaveService", () => ({
  CaveService: { GetCave: jest.fn() },
}));

const entranceFeature = (caveId: string) => ({
  layer: { id: "entrances" },
  properties: { CaveId: caveId },
  geometry: { type: "Point", coordinates: [-87, 35] },
});

const clickMap = async () => {
  await waitFor(() => expect(mockMapOnClick).toBeDefined());
  await act(async () => {
    mockMapOnClick?.({
      features: [],
      point: { x: 100, y: 100 },
      lngLat: { lat: 35.1, lng: -87.1 },
      originalEvent: { target: document.body },
    });
    await Promise.resolve();
  });
};

describe("ExploreMap click routing", () => {
  const originalApiBaseUrl = AppOptions.apiBaseUrl;

  beforeAll(() => {
    AppOptions.apiBaseUrl = "https://api.example.test";
  });

  afterAll(() => {
    AppOptions.apiBaseUrl = originalApiBaseUrl;
  });

  beforeEach(() => {
    mockMapOnClick = undefined;
    mockHydrologyOverlayHit = false;
    mockQueryRenderedFeatures.mockReset();
    mockMapApi.getZoom.mockReturnValue(10);
    mockMapApi.getLayer.mockReturnValue({ id: "entrances" });
    mockMapApi.project.mockReturnValue({ x: 112, y: 100 });
    (CaveService.GetCave as jest.Mock).mockReset().mockResolvedValue({ id: "cave" });
  });

  test("opens a nearby cave when the tap narrowly misses its rendered entrance", async () => {
    mockQueryRenderedFeatures.mockImplementation((query) =>
      Array.isArray(query) ? [entranceFeature("nearby-cave")] : []
    );

    render(<ExploreMap initialCenter={[35, -87]} initialZoom={10} />);
    await clickMap();

    await waitFor(() => expect(CaveService.GetCave).toHaveBeenCalledWith("nearby-cave"));
    expect(screen.queryByTestId("point-modal")).not.toBeInTheDocument();
  });

  test("opens point details when no entrance or linework is hit", async () => {
    mockQueryRenderedFeatures.mockReturnValue([]);

    render(<ExploreMap initialCenter={[35, -87]} initialZoom={10} />);
    await clickMap();

    expect(CaveService.GetCave).not.toHaveBeenCalled();
    expect(await screen.findByTestId("point-modal")).toBeInTheDocument();
  });
  test("does not open generic point details when a Hydrology overlay was clicked", async () => {
    mockHydrologyOverlayHit = true;
    mockQueryRenderedFeatures.mockReturnValue([]);

    render(<ExploreMap initialCenter={[35, -87]} initialZoom={10} />);
    await clickMap();

    expect(CaveService.GetCave).not.toHaveBeenCalled();
    expect(screen.queryByTestId("point-modal")).not.toBeInTheDocument();
  });

});
