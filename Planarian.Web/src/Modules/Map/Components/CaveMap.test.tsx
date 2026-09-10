import React from "react";
import { act, render, screen, waitFor } from "@testing-library/react";
import { message } from "antd";
import type { CaveVm } from "../../Caves/Models/CaveVm";
import { CaveService } from "../../Caves/Service/CaveService";
import { CaveMap } from "./CaveMap";

let mockOnMoveEnd: (() => void) | undefined;
let mockOnClick: ((event: any) => void) | undefined;
const mockQueryRenderedFeatures = jest.fn();

jest.mock("../../../Configuration/Context/AppContext", () => {
  const React = require("react");
  return { AppContext: React.createContext({ currentAccountName: "Test account" }) };
});

jest.mock("antd", () => ({ message: { error: jest.fn() } }));
jest.mock("react-router-dom", () => ({ useNavigate: () => jest.fn() }));
jest.mock("../../../Shared/Services/NavigationService", () => ({
  NavigationService: { NavigateToMap: jest.fn() },
}));
jest.mock("../../../Shared/Components/Buttons/PlanarianButtton", () => ({
  PlanarianButton: () => null,
}));
jest.mock("../../Caves/Service/CaveService", () => ({
  CaveService: { GetCave: jest.fn() },
}));
jest.mock("./MapClickCaveModal", () => ({
  MapClickCaveModal: ({ isModalVisible, cave }: { isModalVisible: boolean; cave?: CaveVm }) =>
    isModalVisible ? <div data-testid="cave-modal">{cave?.id}</div> : null,
}));
jest.mock("./MapLayerContext", () => ({ MapLayerProvider: ({ children }: any) => <>{children}</> }));
jest.mock("./MapLayers", () => ({ MapLayers: () => null }));
jest.mock("./MapLayerControl", () => ({ MapLayerControl: () => null }));
jest.mock("./MapEntranceTileLayer", () => ({
  MAP_ENTRANCE_LAYER_ID: "entrances",
  MapEntranceTileLayer: () => <div data-testid="nearby-entrances" />,
}));
jest.mock("./MapLinePlotLayer", () => ({
  MapLinePlotLayer: ({ viewportRevision }: { viewportRevision: number }) => (
    <div data-testid="viewport-linework" data-revision={viewportRevision} />
  ),
}));
jest.mock("./PlanarianBaseMap", () => {
  const React = require("react");
  return {
    PlanarianBaseMap: React.forwardRef((props: any, ref: React.Ref<unknown>) => {
      React.useImperativeHandle(ref, () => ({
        getCenter: () => ({ lat: 35, lng: -87 }),
        getZoom: () => 15,
        getLayer: () => ({ id: "entrances" }),
        project: () => ({ x: 105, y: 100 }),
        queryRenderedFeatures: mockQueryRenderedFeatures,
      }));
      mockOnMoveEnd = props.onMoveEnd;
      mockOnClick = props.onClick;
      return <div data-testid="cave-map">{props.children}</div>;
    }),
  };
});

const cave = {
  id: "cave-1",
  primaryEntrance: { latitude: 35, longitude: -87 },
} as CaveVm;

describe("CaveMap", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockOnMoveEnd = undefined;
    mockOnClick = undefined;
    mockQueryRenderedFeatures.mockReturnValue([{
      layer: { id: "entrances" },
      properties: { CaveId: "cave-2" },
      geometry: { type: "Point", coordinates: [-87.1, 35.1] },
    }]);
  });

  test("shows nearby entrances and refreshes viewport linework after movement", () => {
    render(<CaveMap cave={cave} />);

    expect(screen.getByTestId("nearby-entrances")).toBeInTheDocument();
    expect(screen.getByTestId("viewport-linework")).toHaveAttribute("data-revision", "0");

    act(() => mockOnMoveEnd?.());

    expect(screen.getByTestId("viewport-linework")).toHaveAttribute("data-revision", "1");
  });

  test("does nothing when the click is not on or near a cave entrance", () => {
    mockQueryRenderedFeatures.mockReturnValue([]);
    render(<CaveMap cave={cave} />);

    act(() => {
      mockOnClick?.({
        features: [],
        point: { x: 100, y: 100 },
        originalEvent: { target: document.body },
      });
    });

    expect(CaveService.GetCave).not.toHaveBeenCalled();
    expect(screen.queryByTestId("cave-modal")).not.toBeInTheDocument();
  });

  test("keeps the modal closed when a cave lookup fails", async () => {
    jest.spyOn(console, "error").mockImplementation(() => undefined);
    (CaveService.GetCave as jest.Mock).mockRejectedValue(new Error("not found"));
    render(<CaveMap cave={cave} />);

    act(() => {
      mockOnClick?.({
        features: [{ layer: { id: "entrances" }, properties: { CaveId: "bad-cave" } }],
        point: { x: 100, y: 100 },
        originalEvent: { target: document.body },
      });
    });

    await waitFor(() => expect(message.error).toHaveBeenCalledWith("Unable to load cave."));
    expect(CaveService.GetCave).toHaveBeenCalledWith("bad-cave");
    expect(screen.queryByTestId("cave-modal")).not.toBeInTheDocument();
    (console.error as jest.Mock).mockRestore();
  });

  test("opens a nearby cave when the tap lands within the enlarged entrance hit target", async () => {
    (CaveService.GetCave as jest.Mock).mockResolvedValue({ id: "cave-2", name: "Nearby Cave" });
    render(<CaveMap cave={cave} />);

    act(() => {
      mockOnClick?.({
        features: [],
        point: { x: 100, y: 100 },
        originalEvent: { target: document.body },
      });
    });

    await waitFor(() => expect(CaveService.GetCave).toHaveBeenCalledWith("cave-2"));
    await waitFor(() => expect(screen.getByTestId("cave-modal")).toHaveTextContent("cave-2"));
  });
});
