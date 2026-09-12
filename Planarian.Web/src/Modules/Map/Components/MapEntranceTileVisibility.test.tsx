import { fireEvent, render, screen } from "@testing-library/react";
import { MapEntranceTileLayer, MAP_ENTRANCE_LAYER_ID } from "./MapEntranceTileLayer";
import { MapLayerProvider, useMapLayers } from "./MapLayerContext";

jest.mock("../../../Configuration/Context/AppContext", () => {
  const React = require("react");
  return {
    AppContext: React.createContext({
      currentAccountId: "account-1",
      currentAccountName: "Test account",
    }),
  };
});

jest.mock("react-map-gl/maplibre", () => ({
  Source: ({ id, tiles, children }: any) => (
    <div data-testid={`source-${id}`} data-tiles={JSON.stringify(tiles ?? [])}>
      {children}
    </div>
  ),
  Layer: (props: any) => (
    <div
      data-testid={props.id}
      data-visibility={props.layout?.visibility ?? "unset"}
      data-paint={JSON.stringify(props.paint ?? {})}
    />
  ),
}));

const Controls = () => {
  const { toggleLayer, setOpacity } = useMapLayers();
  return (
    <>
      <button onClick={() => toggleLayer("entrances")}>Toggle entrances</button>
      <button onClick={() => setOpacity("entrances", 0.4)}>Fade entrances</button>
    </>
  );
};
test("account entrance layers follow the shared visibility toggle", () => {
  localStorage.clear();
  render(
    <MapLayerProvider>
      <Controls />
      <MapEntranceTileLayer />
    </MapLayerProvider>
  );

  expect(screen.getByTestId(MAP_ENTRANCE_LAYER_ID)).toHaveAttribute(
    "data-visibility",
    "visible"
  );
  expect(screen.getByTestId("entrances-heatmap")).toHaveAttribute(
    "data-visibility",
    "visible"
  );

  const circlePaint = JSON.parse(screen.getByTestId(MAP_ENTRANCE_LAYER_ID).getAttribute("data-paint")!);
  const heatmapPaint = JSON.parse(screen.getByTestId("entrances-heatmap").getAttribute("data-paint")!);
  const labelPaint = JSON.parse(screen.getByTestId("entrance-labels").getAttribute("data-paint")!);
  expect(circlePaint["circle-opacity"][0]).toBe("step");
  expect(heatmapPaint["heatmap-opacity"][0]).toBe("step");
  expect(labelPaint["text-opacity"][0]).toBe("step");

  fireEvent.click(screen.getByText("Fade entrances"));
  const fadedCirclePaint = JSON.parse(screen.getByTestId(MAP_ENTRANCE_LAYER_ID).getAttribute("data-paint")!);
  expect(fadedCirclePaint["circle-stroke-opacity"]).toBe(0.4);
  expect(fadedCirclePaint["circle-opacity"][4]).toBeCloseTo(0.24);

  fireEvent.click(screen.getByText("Toggle entrances"));
  expect(screen.getByTestId(MAP_ENTRANCE_LAYER_ID)).toHaveAttribute(
    "data-visibility",
    "none"
  );
  expect(screen.getByTestId("entrances-heatmap")).toHaveAttribute(
    "data-visibility",
    "none"
  );
});
