import { render, screen } from "@testing-library/react";
import { MapLayers } from "./MapLayers";

jest.mock("react-map-gl/maplibre", () => ({
  Source: (props: any) => (
    <div
      data-testid={`source-${props.id}`}
      data-has-minzoom={String(Object.prototype.hasOwnProperty.call(props, "minzoom"))}
      data-has-maxzoom={String(Object.prototype.hasOwnProperty.call(props, "maxzoom"))}
      data-minzoom={props.minzoom}
      data-maxzoom={props.maxzoom}
    >
      {props.children}
    </div>
  ),
  Layer: (props: any) => (
    <div
      data-testid={`layer-${props.id}`}
      data-has-minzoom={String(Object.prototype.hasOwnProperty.call(props, "minzoom"))}
      data-has-maxzoom={String(Object.prototype.hasOwnProperty.call(props, "maxzoom"))}
      data-minzoom={props.minzoom}
      data-maxzoom={props.maxzoom}
      data-before-id={props.beforeId}
    />
  ),
  useMap: () => ({ current: undefined }),
}));

jest.mock("./MapLayerContext", () => ({
  useMapLayers: () => ({
    isVisible: () => false,
    opacity: () => 1,
    terrainEnabled: false,
    terrainExaggeration: 1,
  }),
}));

describe("MapLayers optional zoom props", () => {
  test("omits undefined zoom props while preserving configured zoom limits", () => {
    render(<MapLayers />);

    expect(screen.getByTestId("source-osm-street")).toHaveAttribute("data-has-minzoom", "false");
    expect(screen.getByTestId("source-osm-street")).toHaveAttribute("data-has-maxzoom", "false");
    expect(screen.getByTestId("layer-osm-street")).toHaveAttribute("data-has-minzoom", "false");
    expect(screen.getByTestId("layer-osm-street")).toHaveAttribute("data-has-maxzoom", "false");
    expect(screen.getByTestId("layer-osm-street")).toHaveAttribute("data-before-id", "planarian-overlay-anchor");

    expect(screen.getByTestId("source-usgs-500k-geology")).toHaveAttribute("data-has-minzoom", "true");
    expect(screen.getByTestId("source-usgs-500k-geology")).toHaveAttribute("data-has-maxzoom", "true");
    expect(screen.getByTestId("source-usgs-500k-geology")).toHaveAttribute("data-minzoom", "4");
    expect(screen.getByTestId("source-usgs-500k-geology")).toHaveAttribute("data-maxzoom", "12");
    expect(screen.getByTestId("layer-usgs-500k-geology")).toHaveAttribute("data-has-minzoom", "false");
    expect(screen.getByTestId("layer-usgs-500k-geology")).toHaveAttribute("data-has-maxzoom", "false");

    expect(screen.getByTestId("source-regrid-parcel-boundaries")).toHaveAttribute("data-has-minzoom", "false");
    expect(screen.getByTestId("source-regrid-parcel-boundaries")).toHaveAttribute("data-has-maxzoom", "false");
    expect(screen.getByTestId("layer-regrid-parcel-boundaries")).toHaveAttribute("data-has-minzoom", "true");
    expect(screen.getByTestId("layer-regrid-parcel-boundaries")).toHaveAttribute("data-has-maxzoom", "true");
    expect(screen.getByTestId("layer-regrid-parcel-boundaries")).toHaveAttribute("data-minzoom", "15");
    expect(screen.getByTestId("layer-regrid-parcel-boundaries")).toHaveAttribute("data-maxzoom", "17");
    expect(screen.getByTestId("layer-regrid-parcel-boundaries")).toHaveAttribute("data-before-id", "planarian-overlay-anchor");
  });
});
