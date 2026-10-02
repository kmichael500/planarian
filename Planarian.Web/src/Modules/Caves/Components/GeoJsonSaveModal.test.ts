import { caveToForm } from "../Helpers/CaveFormMapper";
import { CaveService } from "../Service/CaveService";
import { saveImportedLinePlots } from "./GeoJsonSaveModal";

jest.mock("../Helpers/CaveFormMapper", () => ({
  caveToForm: jest.fn(),
}));

jest.mock("../Service/CaveService", () => ({
  CaveService: {
    GetEditAuthoringContext: jest.fn(),
    UpdateCave: jest.fn(),
  },
}));

const mockedCaveToForm = caveToForm as jest.MockedFunction<typeof caveToForm>;
const mockedGetEditContext =
  CaveService.GetEditAuthoringContext as jest.MockedFunction<
    typeof CaveService.GetEditAuthoringContext
  >;
const mockedUpdateCave = CaveService.UpdateCave as jest.MockedFunction<
  typeof CaveService.UpdateCave
>;

beforeEach(() => {
  jest.clearAllMocks();
});

it("publishes imported line plots through the revision-aware cave update flow", async () => {
  const cave = { id: "cave000001", currentRevisionId: "revision01" } as any;
  const mapped = { id: "cave000001", expectedRevisionId: "revision01" } as any;
  mockedGetEditContext.mockResolvedValue({ cave, linePlots: [] });
  mockedCaveToForm.mockReturnValue(mapped);

  await saveImportedLinePlots(
    "cave000001",
    JSON.stringify([
      { type: "FeatureCollection", features: [] },
      { type: "FeatureCollection", features: [{ type: "Feature", properties: {}, geometry: null }] },
    ]),
    "Imported survey"
  );

  expect(mockedGetEditContext).toHaveBeenCalledWith("cave000001");
  expect(mockedCaveToForm).toHaveBeenCalledWith(cave, [
    {
      name: "Imported survey",
      geoJson: '{"type":"FeatureCollection","features":[]}',
    },
    {
      name: "Imported survey",
      geoJson:
        '{"type":"FeatureCollection","features":[{"type":"Feature","properties":{},"geometry":null}]}',
    },
  ]);
  expect(mockedUpdateCave).toHaveBeenCalledWith(mapped);
});
