import { fireEvent, render, screen } from "@testing-library/react";
import { useMapLayers } from "./MapLayerContext";
import { UsgsHillshadeSettingsControl } from "./UsgsHillshadeSettingsControl";

jest.mock("./MapLayerContext", () => ({
  useMapLayers: jest.fn(),
}));

const mockUseMapLayers = useMapLayers as jest.MockedFunction<
  typeof useMapLayers
>;
const setUsgsHillshadeVariant = jest.fn();

const createMapLayerContext = (): ReturnType<typeof useMapLayers> => ({
  isVisible: jest.fn(),
  opacity: jest.fn(),
  toggleLayer: jest.fn(),
  setOpacity: jest.fn(),
  terrainEnabled: false,
  terrainExaggeration: 1,
  setTerrainEnabled: jest.fn(),
  setTerrainExaggeration: jest.fn(),
  hillshadePreferences: {
    exaggeration: 0.42,
    illuminationDirection: 123,
    contrast: 0.9,
    paletteOverrides: {},
  },
  setHillshadeExaggeration: jest.fn(),
  setHillshadeDirection: jest.fn(),
  setHillshadeContrast: jest.fn(),
  setHillshadePaletteColor: jest.fn(),
  resetHillshadePalette: jest.fn(),
  resetHillshadeSettings: jest.fn(),
  usgsHillshadeVariant: "gray",
  setUsgsHillshadeVariant,
});

beforeEach(() => {
  jest.clearAllMocks();
  mockUseMapLayers.mockReturnValue(createMapLayerContext());
});

describe("UsgsHillshadeSettingsControl", () => {
  test("shows every supported server renderer for an active USGS source", () => {
    render(<UsgsHillshadeSettingsControl />);

    expect(screen.getByLabelText("USGS hillshade variant")).toHaveValue("gray");
    expect(screen.getByRole("option", { name: "Gray" })).toBeInTheDocument();
    expect(
      screen.getByRole("option", { name: "Gray-Stretch" })
    ).toBeInTheDocument();
    expect(
      screen.getByRole("option", { name: "Multidirectional" })
    ).toBeInTheDocument();
  });

  test("updates the server-side raster function variant", () => {
    render(<UsgsHillshadeSettingsControl />);

    fireEvent.change(screen.getByLabelText("USGS hillshade variant"), {
      target: { value: "multidirectional" },
    });

    expect(setUsgsHillshadeVariant).toHaveBeenCalledWith("multidirectional");
  });
});
