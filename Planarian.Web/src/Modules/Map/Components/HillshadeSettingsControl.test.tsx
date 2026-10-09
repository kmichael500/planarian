import { fireEvent, render, screen } from "@testing-library/react";
import { useMapLayers } from "./MapLayerContext";
import { HillshadeSettingsControl } from "./HillshadeSettingsControl";

jest.mock("./MapLayerContext", () => ({
  useMapLayers: jest.fn(),
}));

const mockUseMapLayers = useMapLayers as jest.MockedFunction<
  typeof useMapLayers
>;
const setHillshadeDirection = jest.fn();
const resetHillshadePalette = jest.fn();
const resetHillshadeSettings = jest.fn();

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
  setHillshadeDirection,
  setHillshadeContrast: jest.fn(),
  setHillshadePaletteColor: jest.fn(),
  resetHillshadePalette,
  resetHillshadeSettings,
});

beforeEach(() => {
  jest.clearAllMocks();
  mockUseMapLayers.mockReturnValue(createMapLayerContext());
});

describe("HillshadeSettingsControl", () => {
  test("keeps advanced controls collapsed until requested", () => {
    render(<HillshadeSettingsControl />);

    expect(screen.queryByText("Relief strength")).not.toBeInTheDocument();
    fireEvent.click(
      screen.getByRole("button", { name: /Hillshade settings/i })
    );

    expect(screen.getByText("Relief strength")).toBeInTheDocument();
    expect(screen.getByText("Contrast")).toBeInTheDocument();
    expect(screen.getByText(/Light direction/)).toBeInTheDocument();
    expect(screen.queryByText("Background")).not.toBeInTheDocument();
  });

  test("maps compass choices to illumination directions", () => {
    render(<HillshadeSettingsControl />);
    fireEvent.click(
      screen.getByRole("button", { name: /Hillshade settings/i })
    );
    fireEvent.click(screen.getByRole("button", { name: "Light from E" }));

    expect(setHillshadeDirection).toHaveBeenCalledWith(90);
  });

  test("reveals theme-specific color controls and reset actions", () => {
    render(<HillshadeSettingsControl />);
    fireEvent.click(
      screen.getByRole("button", { name: /Hillshade settings/i })
    );
    fireEvent.click(screen.getByRole("button", { name: /Advanced colors/i }));

    expect(screen.getByRole("tab", { name: "Light" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Dark" })).toBeInTheDocument();
    expect(screen.getByText("Background")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Reset light colors" }));
    expect(resetHillshadePalette).toHaveBeenCalledWith("light");

    fireEvent.click(
      screen.getByRole("button", { name: "Reset hillshade defaults" })
    );
    expect(resetHillshadeSettings).toHaveBeenCalled();
  });
});
