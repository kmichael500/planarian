import { caveToForm, snapshotToForm } from "./CaveFormMapper";
import { CaveSnapshotVm } from "../Models/CaveRevisionVm";
import { CaveVm } from "../Models/CaveVm";
import dayjs from "dayjs";

const snapshot: CaveSnapshotVm = {
  caveId: "cave", accountId: "account", name: "Cave", alternateNames: [],
  state: { id: "state", nameAtRevision: "State" },
  county: { id: "county", nameAtRevision: "County", displayIdAtRevision: "001" },
  countyNumber: 12, isArchived: false, tags: [], entrances: [], files: [], linePlots: [],
};

it.each([
  ["AutomaticNext", false, false, 12],
  ["FirstAvailable", false, true, 12],
  ["Manual", true, false, 42],
] as const)("round-trips %s county-number intent", (intent, manual, firstAvailable, number) => {
  const form = snapshotToForm(snapshot, intent, intent === "Manual" ? 42 : undefined);
  expect(form.isCountyNumberManuallySet).toBe(manual);
  expect(form.useFirstAvailableCountyNumber).toBe(firstAvailable);
  expect(form.countyNumber).toBe(number);
});

it("adds active staged files to a current-Cave stale rereview baseline", () => {
  const form = snapshotToForm(snapshot, undefined, undefined, [{
    id: "staged", fileName: "staged.pdf", displayName: "Staged",
    fileTypeTagId: "document", fileTypeNameAtRevision: "Document",
  }]);
  expect(form.files).toEqual([expect.objectContaining({ id: "staged", displayName: "Staged" })]);
});

it("preserves null, zero, and positive Cave measurements in proposal editor state", () => {
  const form = snapshotToForm({
    ...snapshot,
    lengthFeet: null,
    depthFeet: 0,
    maxPitDepthFeet: 42,
    numberOfPits: null,
  });

  expect(form.lengthFeet).toBeNull();
  expect(form.depthFeet).toBe(0);
  expect(form.maxPitDepthFeet).toBe(42);
  expect(form.numberOfPits).toBeNull();
});

it("retains Entrance Other tags when mapping a Cave into the editor", () => {
  const cave: CaveVm = {
    id: "cave", currentRevisionId: null, displayId: "A-1",
    countyId: "county", stateId: "state", countyDisplayId: "A", countyNumber: 1,
    name: "Cave", alternateNames: [], lengthFeet: 0, depthFeet: 0, maxPitDepthFeet: 0,
    numberOfPits: 0, narrative: null, reportedOn: null, isArchived: false, primaryEntrance: null,
    mapIds: [], geologyTagIds: [], files: [], reportedByNameTagIds: [], biologyTagIds: [],
    archeologyTagIds: [], cartographerNameTagIds: [], mapStatusTagIds: [], geologicAgeTagIds: [],
    physiographicProvinceTagIds: [], otherTagIds: [],
    entrances: [{
      id: "entrance", isPrimary: true, locationQualityTagId: "quality",
      name: null, description: null, latitude: 35, longitude: -86, elevationFeet: 500,
      reportedOn: null, pitFeet: null, entranceStatusTagIds: [], fieldIndicationTagIds: [],
      entranceHydrologyTagIds: [], reportedByNameTagIds: [], entranceOtherTagIds: ["other-tag"],
    }],
  };

  expect(caveToForm(cave).entrances[0].entranceOtherTagIds).toEqual(["other-tag"]);
});

it("carries exact line-plot authoring payloads into editor state", () => {
  const linePlots = [{ id: "line-1", name: "Survey", geoJson: '{"type":"FeatureCollection","features":[]}' }];
  expect(snapshotToForm(snapshot, undefined, undefined, [], linePlots).linePlots).toEqual(linePlots);
});


it("converts Cave and entrance API date strings into DatePicker-compatible values", () => {
  const cave: CaveVm = {
    id: "cave", currentRevisionId: "revision", displayId: "A-1",
    countyId: "county", stateId: "state", countyDisplayId: "A", countyNumber: 1,
    name: "Cave", alternateNames: [], lengthFeet: 0, depthFeet: 0, maxPitDepthFeet: 0,
    numberOfPits: 0, narrative: null, reportedOn: "2026-01-02T00:00:00Z", isArchived: false, primaryEntrance: null,
    mapIds: [], geologyTagIds: [], files: [], reportedByNameTagIds: [], biologyTagIds: [],
    archeologyTagIds: [], cartographerNameTagIds: [], mapStatusTagIds: [], geologicAgeTagIds: [],
    physiographicProvinceTagIds: [], otherTagIds: [],
    entrances: [{
      id: "entrance", isPrimary: true, locationQualityTagId: "quality", name: null, description: null,
      latitude: 35, longitude: -86, elevationFeet: 500, reportedOn: "2026-02-03T00:00:00Z", pitFeet: null,
      entranceStatusTagIds: [], fieldIndicationTagIds: [], entranceHydrologyTagIds: [],
      reportedByNameTagIds: [], entranceOtherTagIds: [],
    }],
  };

  const form = caveToForm(cave);

  expect(dayjs.isDayjs(form.reportedOn)).toBe(true);
  expect(dayjs.isDayjs(form.entrances[0].reportedOn)).toBe(true);
  expect(form.reportedOn?.format("YYYY-MM-DD")).toBe("2026-01-02");
  expect(form.entrances[0].reportedOn?.format("YYYY-MM-DD")).toBe("2026-02-03");
});

it("converts proposal snapshot date strings into DatePicker-compatible values", () => {
  const form = snapshotToForm({
    ...snapshot,
    reportedOn: "2026-03-04T00:00:00Z",
    entrances: [{
      id: "entrance", isPrimary: true, description: "", latitude: 35, longitude: -86, elevation: 0, srid: 4326,
      locationQualityTagId: "quality", locationQualityNameAtRevision: "Survey Grade",
      reportedOn: "2026-05-06T00:00:00Z", tags: [],
    }],
  });

  expect(dayjs.isDayjs(form.reportedOn)).toBe(true);
  expect(dayjs.isDayjs(form.entrances[0].reportedOn)).toBe(true);
  expect(form.reportedOn?.format("YYYY-MM-DD")).toBe("2026-03-04");
  expect(form.entrances[0].reportedOn?.format("YYYY-MM-DD")).toBe("2026-05-06");
});
