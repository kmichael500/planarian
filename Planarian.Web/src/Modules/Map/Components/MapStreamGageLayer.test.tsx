import { fireEvent, render, screen } from "@testing-library/react";
import {
  MapStreamGageLayer,
  STREAM_GAGE_LAYER_SETTING_ID,
  STREAM_GAGE_POINT_LAYER_ID,
} from "./MapStreamGageLayer";
import { MapLayerProvider, useMapLayers } from "./MapLayerContext";

jest.mock("../../../Configuration/Context/AppContext", () => {
  const React = require("react");
  return {
    AppContext: React.createContext({ currentAccountId: "account-1" }),
  };
});

jest.mock("react-map-gl/maplibre", () => ({
  Source: ({ id, data, children }: any) => (
    <div data-testid={`source-${id}`} data-source={JSON.stringify(data)}>{children}</div>
  ),
  Layer: (props: any) => (
    <div
      data-testid={props.id}
      data-visibility={props.layout?.visibility ?? "unset"}
      data-minzoom={props.minzoom}
    />
  ),
}));
const Toggle = () => {
  const { toggleLayer } = useMapLayers();
  return <button onClick={() => toggleLayer(STREAM_GAGE_LAYER_SETTING_ID)}>Toggle gages</button>;
};

test("stream gages are controlled by the shared Hydrology layer setting", () => {
  localStorage.clear();
  render(
    <MapLayerProvider>
      <Toggle />
      <MapStreamGageLayer
        gages={[
          {
            id: "USGS-03431500",
            siteCode: "03431500",
            siteName: "Cumberland River at Nashville, TN",
            latitude: 36.1615,
            longitude: -86.77268,
          },
        ]}
      />
    </MapLayerProvider>
  );

  expect(screen.getByTestId(STREAM_GAGE_POINT_LAYER_ID)).toHaveAttribute("data-visibility", "none");
  expect(screen.getByTestId(STREAM_GAGE_POINT_LAYER_ID)).toHaveAttribute("data-minzoom", "8");
  const source = JSON.parse(
    screen.getByTestId("source-nearby-stream-gages-source").getAttribute("data-source")!
  );
  expect(source.features[0]).toMatchObject({
    geometry: { coordinates: [-86.77268, 36.1615] },
    properties: {
      siteCode: "03431500",
      siteName: "Cumberland River at Nashville, TN",
    },
  });

  fireEvent.click(screen.getByText("Toggle gages"));
  expect(screen.getByTestId(STREAM_GAGE_POINT_LAYER_ID)).toHaveAttribute(
    "data-visibility",
    "visible"
  );
  expect(screen.getByTestId("nearby-stream-gage-labels")).toHaveAttribute(
    "data-visibility",
    "visible"
  );
});
