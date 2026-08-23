import { fireEvent, render, screen } from "@testing-library/react";
import { CaveComponent } from "./CaveComponent";

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({ matches: true, addListener: () => undefined, removeListener: () => undefined }),
  });
});

jest.mock("../../../Shared/Permissioning/Components/ShouldDisplay", () => ({
  useFeatureEnabled: () => ({ isFeatureEnabled: () => true }),
}));
jest.mock("../../Map/Components/MapComponent", () => ({ MapComponent: () => null }));
jest.mock("../../Map/Components/Macrostrat", () => ({ Macrostrat: () => <div>Macrostrat content</div> }));
jest.mock("../../Map/Components/GaugeList", () => ({ GageList: () => <div>Gage content</div> }));
jest.mock("../../Map/Components/PublicAccesDetails", () => ({ PublicAccessDetails: () => null }));
jest.mock("../../Files/Components/FileListComponent", () => ({ FileListComponent: () => null }));
jest.mock("../../Tag/Components/TagComponent", () => ({ TagComponent: ({ tagId }: any) => <span>{tagId}</span> }));
jest.mock("../../../Shared/Components/Display/CountyTagComponent", () => ({ CountyTagComponent: () => null }));
jest.mock("../../../Shared/Components/Display/StateTagComponent", () => ({ StateTagComponent: () => null }));
jest.mock("../../../Shared/Components/Display/DistanceFromMeComponent", () => ({ DistanceFromMeComponent: () => null }));
jest.mock("../../../Shared/Components/Display/PlanarianTag", () => ({ PlanarianTag: ({ children }: any) => <span>{children}</span> }));

const cave = {
  id: "cave000001", displayId: "TST1", name: "Test Cave", alternateNames: [], stateId: "tn", countyId: "county",
  archaeologyTagIds: undefined, archeologyTagIds: ["archaeology-tag"], biologyTagIds: [], cartographerNameTagIds: [],
  geologicAgeTagIds: [], geologyTagIds: [], mapStatusTagIds: [], otherTagIds: [], physiographicProvinceTagIds: [],
  reportedByNameTagIds: [], files: [], narrative: null, entrances: [{ id: "entrance01", name: "Main", isPrimary: true,
    latitude: 35, longitude: -86, elevationFeet: 500, locationQualityTagId: "quality", reportedByNameTagIds: [],
    entranceStatusTagIds: [], fieldIndicationTagIds: [], entranceHydrologyTagIds: [], entranceOtherTagIds: [] }],
  primaryEntrance: { id: "entrance01", latitude: 35, longitude: -86 },
} as any;

it("uses Archaeology copy and keeps nearby integrations compact until expanded", () => {
  render(<CaveComponent cave={cave} isLoading={false} options={{ showMap: false }} />);

  expect(screen.getByText("Archaeology")).toBeInTheDocument();
  expect(screen.getByText("Nearby Data")).toBeInTheDocument();
  expect(screen.getByText("Macrostrat · NGMDB")).toBeInTheDocument();
  expect(screen.getByText("USGS NWIS")).toBeInTheDocument();
  expect(screen.queryByText("Macrostrat content")).not.toBeInTheDocument();
  expect(screen.queryByText("Gage content")).not.toBeInTheDocument();

  fireEvent.click(screen.getByRole("button", { name: /Geology Macrostrat/i }));
  expect(screen.getByText("Macrostrat content")).toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: /Stream Gages USGS/i }));
  expect(screen.getByText("Gage content")).toBeInTheDocument();
});

it("treats zero latitude or longitude as valid nearby-data coordinates", () => {
  const zeroCoordinateCave = {
    ...cave,
    entrances: [{ ...cave.entrances[0], longitude: 0 }],
    primaryEntrance: { ...cave.primaryEntrance, longitude: 0 },
  };
  render(<CaveComponent cave={zeroCoordinateCave} isLoading={false} options={{ showMap: false }} />);

  fireEvent.click(screen.getByRole("button", { name: /Geology Macrostrat/i }));
  expect(screen.getByText("Macrostrat content")).toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: /Stream Gages USGS/i }));
  expect(screen.getByText("Gage content")).toBeInTheDocument();
  expect(screen.queryByText(/Coordinates are required/)).not.toBeInTheDocument();
});
