import { render, screen, waitFor } from "@testing-library/react";
import type { FeatureCollection } from "geojson";
import { VectorDatasetViewer } from "./VectorDatasetViewer";

jest.mock("../../Map/Components/SpatialFileMap", () => ({
  SpatialFileMap: ({
    initialCenter,
    bounds,
    children,
  }: {
    initialCenter: [number, number];
    bounds: [[number, number], [number, number]];
    children?: React.ReactNode;
  }) => (
    <div
      data-testid="spatial-file-map"
      data-center={JSON.stringify(initialCenter)}
      data-bounds={JSON.stringify(bounds)}
    >
      {children}
    </div>
  ),
}));

jest.mock("react-map-gl/maplibre", () => ({
  Source: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  Layer: () => null,
}));

afterEach(() => {
  jest.restoreAllMocks();
});

it("renders inline revision line-plot data without attempting a file fetch", async () => {
  const fetchSpy = jest.spyOn(global, "fetch");
  const data: FeatureCollection = {
    type: "FeatureCollection",
    features: [
      {
        type: "Feature",
        properties: {},
        geometry: {
          type: "LineString",
          coordinates: [
            [-86, 35],
            [-85, 36],
          ],
        },
      },
    ],
  };

  render(
    <VectorDatasetViewer embedUrl="" fileType="geojson" data={data} />
  );

  await waitFor(() =>
    expect(screen.getByTestId("spatial-file-map")).toBeInTheDocument()
  );

  expect(fetchSpy).not.toHaveBeenCalled();
  expect(screen.getByTestId("spatial-file-map")).toHaveAttribute(
    "data-center",
    JSON.stringify([35.5, -85.5])
  );
  expect(screen.getByTestId("spatial-file-map")).toHaveAttribute(
    "data-bounds",
    JSON.stringify([
      [-86, 35],
      [-85, 36],
    ])
  );
});
