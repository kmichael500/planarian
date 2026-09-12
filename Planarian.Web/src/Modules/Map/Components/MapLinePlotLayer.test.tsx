import { act, render, screen, waitFor } from "@testing-library/react";
import type { FeatureCollection } from "geojson";
import { MapLinePlotLayer } from "./MapLinePlotLayer";
import { MapService } from "../Services/MapService";

let mockLinePlotsVisible = true;
let mockLinePlotsOpacity = 1;
let mockMapRef: {
  getMap: () => {
    getZoom: () => number;
    getBounds: () => {
      getNorth: () => number;
      getSouth: () => number;
      getEast: () => number;
      getWest: () => number;
    };
  };
} | undefined;

jest.mock("react-map-gl/maplibre", () => ({
  useMap: () => ({ current: mockMapRef }),
  Source: ({ id, children }: { id: string; children: React.ReactNode }) => (
    <div data-testid={id}>{children}</div>
  ),
  Layer: () => null,
}));

jest.mock("antd", () => ({
  message: { error: jest.fn() },
}));

jest.mock("./MapLayerContext", () => ({
  useMapLayers: () => ({
    isVisible: () => mockLinePlotsVisible,
    opacity: () => mockLinePlotsOpacity,
  }),
}));

jest.mock("../Services/MapService", () => ({
  MapService: {
    getLinePlotIds: jest.fn(),
    getLinePlot: jest.fn(),
  },
}));

const collection: FeatureCollection = {
  type: "FeatureCollection",
  features: [],
};

const deferred = <T,>() => {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((resolvePromise) => {
    resolve = resolvePromise;
  });
  return { promise, resolve };
};

describe("MapLinePlotLayer", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockLinePlotsVisible = true;
    mockLinePlotsOpacity = 1;
    mockMapRef = undefined;
  });

  test("does not request line plots when the shared map-data layer is disabled", async () => {
    mockLinePlotsVisible = false;
    mockMapRef = {
      getMap: () => ({
        getZoom: () => 12,
        getBounds: () => ({
          getNorth: () => 36,
          getSouth: () => 35,
          getEast: () => -85,
          getWest: () => -86,
        }),
      }),
    };

    render(<MapLinePlotLayer />);

    await act(async () => undefined);
    expect(MapService.getLinePlotIds).not.toHaveBeenCalled();
    expect(MapService.getLinePlot).not.toHaveBeenCalled();
  });

  test("renders linework that loads successfully when another plot fails", async () => {
    jest.spyOn(console, "error").mockImplementation(() => undefined);
    const mapInstance = {
      getZoom: () => 12,
      getBounds: () => ({
        getNorth: () => 36,
        getSouth: () => 35,
        getEast: () => -85,
        getWest: () => -86,
      }),
    };
    mockMapRef = { getMap: () => mapInstance };
    (MapService.getLinePlotIds as jest.Mock).mockResolvedValue(["good", "bad"]);
    (MapService.getLinePlot as jest.Mock).mockImplementation((id: string) =>
      id === "good" ? Promise.resolve(collection) : Promise.reject(new Error("bad plot"))
    );

    render(<MapLinePlotLayer />);

    expect(await screen.findByTestId("linework-good")).toBeInTheDocument();
    expect(screen.queryByTestId("linework-bad")).not.toBeInTheDocument();
    expect(MapService.getLinePlot).toHaveBeenCalledTimes(2);

    (console.error as jest.Mock).mockRestore();
  });
  test("keeps an in-flight linework result when the viewport changes", async () => {
    const firstIds = deferred<string[]>();
    const secondIds = deferred<string[]>();
    const plot = deferred<FeatureCollection>();
    const mapInstance = {
      getZoom: () => 12,
      getBounds: () => ({
        getNorth: () => 36,
        getSouth: () => 35,
        getEast: () => -85,
        getWest: () => -86,
      }),
    };
    mockMapRef = { getMap: () => mapInstance };

    (MapService.getLinePlotIds as jest.Mock)
      .mockReturnValueOnce(firstIds.promise)
      .mockReturnValueOnce(secondIds.promise);
    (MapService.getLinePlot as jest.Mock).mockReturnValue(plot.promise);

    const { rerender } = render(<MapLinePlotLayer viewportRevision={0} />);
    await waitFor(() => expect(MapService.getLinePlotIds).toHaveBeenCalledTimes(1));

    rerender(<MapLinePlotLayer viewportRevision={1} />);
    await waitFor(() => expect(MapService.getLinePlotIds).toHaveBeenCalledTimes(2));

    await act(async () => {
      firstIds.resolve(["plot-1"]);
    });
    await waitFor(() => expect(MapService.getLinePlot).toHaveBeenCalledWith("plot-1"));

    await act(async () => {
      secondIds.resolve(["plot-1"]);
      plot.resolve(collection);
    });

    expect(await screen.findByTestId("linework-plot-1")).toBeInTheDocument();
    expect(MapService.getLinePlot).toHaveBeenCalledTimes(1);
  });

});
