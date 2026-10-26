import { Form } from "antd";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import dayjs from "dayjs";
import utc from "dayjs/plugin/utc";
import { AddCaveVm } from "../Models/AddCaveVm";
import { getCaveAuthoringSubmissionValues } from "../Helpers/CaveAuthoringFormValues";
import { CaveService } from "../Service/CaveService";
import { AddCaveComponent } from "./AddCaveComponent";

dayjs.extend(utc);

let mockFeaturesEnabled = true;

jest.mock("../../../Shared/Permissioning/Components/ShouldDisplay", () => ({
  ShouldDisplay: ({ children }: { children?: React.ReactNode }) => mockFeaturesEnabled ? <>{children}</> : null,
  useFeatureEnabled: () => ({ isFeatureEnabled: () => mockFeaturesEnabled }),
}));

jest.mock("./StateDropdown", () => ({
  StateDropdown: ({ value, onChange }: { value?: string; onChange?: (value: string) => void }) =>
    <input aria-label="State selector" value={value ?? ""} onChange={event => onChange?.(event.target.value)} />,
}));

jest.mock("./CountyDropdown", () => ({
  CountyDropdown: ({ value, onChange }: { value?: string; onChange?: (value: string) => void }) =>
    <input aria-label="County selector" value={value ?? ""} onChange={event => onChange?.(event.target.value)} />,
}));

jest.mock("../../Tag/Components/TagSelectComponent", () => ({
  TagSelectComponent: ({ value }: { value?: unknown }) =>
    <span data-testid="tag-value">{JSON.stringify(value ?? null)}</span>,
}));

jest.mock("../../../Shared/Components/Inputs/InputDistance", () => ({
  InputDistanceComponent: ({ value, ...props }: any) => <input value={value ?? ""} {...props} />,
}));

jest.mock("../../../Shared/Components/Buttons/DeleteButtonComponent", () => ({
  DeleteButtonComponent: ({ onConfirm, title }: { onConfirm: () => void; title: string }) =>
    <button type="button" aria-label={title} onClick={onConfirm}>Delete</button>,
}));

jest.mock("../../../Shared/Components/Buttons/PlanarianButtton", () => ({
  PlanarianButton: ({ children, icon: _icon, alwaysShowChildren: _alwaysShowChildren,
    block: _block, danger: _danger, ...props }: any) => <button {...props}>{children}</button>,
}));

jest.mock("../../../Shared/Components/PlanarianDivider/PlanarianDividerComponent", () => ({
  PlanarianDividerComponent: () => <hr />,
}));

jest.mock("../../Files/Components/FileViewerComponent", () => ({
  FileViewer: () => null,
}));

jest.mock("./CaveLinePlotViewer", () => ({
  CaveLinePlotViewer: () => null,
}));

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({
      matches: false, addListener: () => undefined, removeListener: () => undefined,
      addEventListener: () => undefined, removeEventListener: () => undefined, dispatchEvent: () => false,
    }),
  });
  Object.defineProperty(window, "scrollTo", { writable: true, value: jest.fn() });
});

beforeEach(() => {
  jest.spyOn(CaveService, "IsCountyNumberInUse").mockResolvedValue(false);
});

afterEach(() => {
  mockFeaturesEnabled = true;
  jest.restoreAllMocks();
});

const completeValues = (): AddCaveVm => ({
  id: "cave000001",
  expectedRevisionId: "revision01",
  name: "Complete Cave",
  alternateNames: ["Alias A", "Alias B"],
  stateId: "state00001",
  countyId: "county0001",
  countyDisplayId: "CNT",
  countyNumber: 42,
  isCountyNumberManuallySet: true,
  useFirstAvailableCountyNumber: false,
  lengthFeet: 123.5,
  depthFeet: 54.25,
  maxPitDepthFeet: 31.75,
  numberOfPits: 4,
  narrative: "Complete narrative",
  reportedOn: dayjs.utc("2026-01-02T00:00:00Z"),
  geologyTagIds: ["geology001"],
  reportedByNameTagIds: ["person0001"],
  biologyTagIds: ["biology001"],
  archeologyTagIds: ["arch000001"],
  cartographerNameTagIds: ["person0002"],
  mapStatusTagIds: ["mapstat001"],
  geologicAgeTagIds: ["geoage001"],
  physiographicProvinceTagIds: ["physio0001"],
  otherTagIds: ["other00001"],
  entrances: [{
    id: "entrance01",
    isPrimary: true,
    locationQualityTagId: "quality001",
    name: "Main Entrance",
    description: "Entrance description",
    latitude: 35.1234567,
    longitude: -86.7654321,
    elevationFeet: 612.25,
    reportedOn: dayjs.utc("2026-02-03T00:00:00Z"),
    pitFeet: 88.5,
    entranceStatusTagIds: ["status0001"],
    fieldIndicationTagIds: ["field00001"],
    entranceHydrologyTagIds: ["hydro00001"],
    entranceOtherTagIds: ["other00002"],
    reportedByNameTagIds: ["person0003"],
  }, {
    id: "entrance02",
    isPrimary: false,
    locationQualityTagId: "quality002",
    name: "Secondary Entrance",
    description: null,
    latitude: 35.25,
    longitude: -86.75,
    elevationFeet: 500,
    reportedOn: null,
    pitFeet: null,
    entranceStatusTagIds: [],
    fieldIndicationTagIds: [],
    entranceHydrologyTagIds: [],
    entranceOtherTagIds: [],
    reportedByNameTagIds: [],
  }],
  files: [{
    id: "file000001",
    name: "Survey",
    extension: ".pdf",
    fileTypeTagId: "filetype01",
    fileTypeKey: "Survey",
  }],
  linePlots: [{
    id: "lineplot01",
    name: "Main Traverse",
    geoJson: "{\"type\":\"FeatureCollection\",\"features\":[]}",
  }],
});

const FormHarness = ({ values, onFinish }: { values: AddCaveVm; onFinish: jest.Mock }) => {
  const [form] = Form.useForm<AddCaveVm>();
  return <Form form={form} initialValues={values}
    onFinish={() => onFinish(getCaveAuthoringSubmissionValues(form))}>
    <AddCaveComponent form={form} cave={values} isEditing submitLabel="Save" />
    <button type="button" onClick={() => form.submit()}>Submit contract</button>
  </Form>;
};

const expectCompleteSubmission = (submitted: AddCaveVm, expected: AddCaveVm) => {
  expect(submitted).toEqual(expected);
  expect(submitted.entrances.map(entrance => entrance.id)).toEqual(expected.entrances.map(entrance => entrance.id));
  expect(submitted.files?.[0]).toEqual(expected.files?.[0]);
  expect(submitted.linePlots?.[0]).toEqual(expected.linePlots?.[0]);
};

it("submits every complete Cave, Entrance, File, and line-plot field without loss", async () => {
  mockFeaturesEnabled = true;
  const values = completeValues();
  const onFinish = jest.fn();
  render(<FormHarness values={values} onFinish={onFinish} />);

  await userEvent.click(screen.getByRole("button", { name: "Submit contract" }));

  await waitFor(() => expect(onFinish).toHaveBeenCalledTimes(1));
  expectCompleteSubmission(onFinish.mock.calls[0][0], values);
});

it("preserves pre-existing feature-gated values when those fields are hidden", async () => {
  mockFeaturesEnabled = false;
  const values = completeValues();
  const onFinish = jest.fn();
  render(<FormHarness values={values} onFinish={onFinish} />);

  await userEvent.click(screen.getByRole("button", { name: "Submit contract" }));

  await waitFor(() => expect(onFinish).toHaveBeenCalledTimes(1));
  expectCompleteSubmission(onFinish.mock.calls[0][0], values);
});


it("keeps an existing Entrance stable ID when its editable fields change", async () => {
  const values = completeValues();
  const onFinish = jest.fn();
  render(<FormHarness values={values} onFinish={onFinish} />);

  const entranceName = screen.getByDisplayValue("Main Entrance");
  await userEvent.clear(entranceName);
  await userEvent.type(entranceName, "Renamed Main Entrance");
  await userEvent.click(screen.getByRole("button", { name: "Submit contract" }));

  await waitFor(() => expect(onFinish).toHaveBeenCalledTimes(1));
  const submitted = onFinish.mock.calls[0][0] as AddCaveVm;
  expect(submitted.entrances[0]).toEqual(expect.objectContaining({
    id: "entrance01",
    name: "Renamed Main Entrance",
  }));
  expect(submitted.entrances[1].id).toBe("entrance02");
});

it("removes exactly the selected existing Entrance from the submitted aggregate", async () => {
  const values = completeValues();
  const onFinish = jest.fn();
  render(<FormHarness values={values} onFinish={onFinish} />);

  await userEvent.click(screen.getByRole("button", { name: /delete entrance 2/i }));
  await userEvent.click(screen.getByRole("button", { name: "Submit contract" }));

  await waitFor(() => expect(onFinish).toHaveBeenCalledTimes(1));
  const submitted = onFinish.mock.calls[0][0] as AddCaveVm;
  expect(submitted.entrances).toHaveLength(1);
  expect(submitted.entrances[0]).toEqual(expect.objectContaining({
    id: "entrance01",
    isPrimary: true,
  }));
  expect(submitted.entrances.some(entrance => entrance.id === "entrance02")).toBe(false);
  expect(submitted.files).toEqual(values.files);
  expect(submitted.linePlots).toEqual(values.linePlots);
});

it("does not submit a phantom Entrance when a new row is added and removed before save", async () => {
  const values = completeValues();
  values.entrances = [values.entrances[0]];
  const onFinish = jest.fn();
  render(<FormHarness values={values} onFinish={onFinish} />);

  await userEvent.click(screen.getByRole("button", { name: "Add Entrance" }));
  expect(screen.getByRole("button", { name: /delete entrance 2/i })).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: /delete entrance 2/i }));
  await userEvent.click(screen.getByRole("button", { name: "Submit contract" }));

  await waitFor(() => expect(onFinish).toHaveBeenCalledTimes(1));
  expectCompleteSubmission(onFinish.mock.calls[0][0], values);
});
