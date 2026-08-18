import { CaveRevisionDiffVm, CaveSnapshotVm } from "../Models/CaveRevisionVm";
import { buildCaveRevisionDiffPresentation, parseReferenceMetadataPath } from "./CaveRevisionDiffPresentation";

const snapshot = (): CaveSnapshotVm => ({
  caveId: "cave", accountId: "account", name: "Before", alternateNames: [],
  state: { id: "tn", nameAtRevision: "Tennessee", abbreviationAtRevision: "TN" },
  county: { id: "county", nameAtRevision: "Franklin", displayIdAtRevision: "026" },
  countyNumber: 1, lengthFeet: null, numberOfPits: null,
  narrative: "old narrative", isArchived: false,
  tags: [],
  entrances: [{
    id: "entrance", name: "Main", isPrimary: false, description: "old description",
    latitude: 35, longitude: -86,
    elevation: undefined, srid: 4326, locationQualityTagId: "exact", locationQualityNameAtRevision: "Exact",
    pitDepthFeet: undefined, tags: [],
  }],
  files: [{ id: "file", fileTypeTagId: "map", fileTypeNameAtRevision: "Map", name: "Map", extension: ".pdf" }],
  linePlots: [],
});

const emptyDiff = (): CaveRevisionDiffVm => ({
  scalars: [], addedTags: [], removedTags: [], addedEntrances: [], removedEntrances: [], changedEntrances: [],
  addedFiles: [], removedFiles: [], changedFiles: [], entranceChanges: [], fileChanges: [], addedLinePlots: [], removedLinePlots: [], changedLinePlots: [], linePlotChanges: [], referenceMetadataChanges: [],
});

it("groups Cave roles in Cave-detail order with removals before additions", () => {
  const diff = emptyDiff();
  diff.removedTags = [
    { role: "PhysiographicProvince", tagTypeId: "west", nameAtRevision: "Western Escarpment" },
    { role: "GeologicAge", tagTypeId: "miss", nameAtRevision: "Mississippian" },
  ];
  diff.addedTags = [
    { role: "Cartographer", tagTypeId: "david", nameAtRevision: "David Parr" },
    { role: "Biology", tagTypeId: "cricket", nameAtRevision: "Cricket" },
    { role: "PhysiographicProvince", tagTypeId: "east", nameAtRevision: "Eastern Escarpment" },
    { role: "PhysiographicProvince", tagTypeId: "highland", nameAtRevision: "Highland Rim" },
  ];

  const model = buildCaveRevisionDiffPresentation(diff, snapshot(), snapshot());

  expect(model.caveInformation.map(field => field.label)).toEqual([
    "Geologic Age", "Physiographic Province", "Biology", "Cartographers",
  ]);
  const province = model.caveInformation[1].tags!;
  expect(province.removed.map(tag => tag.nameAtRevision)).toEqual(["Western Escarpment"]);
  expect(province.added.map(tag => tag.nameAtRevision)).toEqual(["Eastern Escarpment", "Highland Rim"]);
});

it("uses only authoritative nested fields and groups authoritative coordinates", () => {
  const previous = snapshot();
  const current = snapshot();
  current.entrances[0] = { ...current.entrances[0], name: "Snapshot-only difference", latitude: 36,
    elevation: 0, isPrimary: true };
  const diff = emptyDiff();
  diff.changedEntrances = ["entrance"];
  diff.entranceChanges = [{ entranceId: "entrance", scalars: [{ path: "Latitude", previous: 35, current: 36 }],
    addedTags: [], removedTags: [] }];

  const entrance = buildCaveRevisionDiffPresentation(diff, previous, current).entrances[0];

  expect(entrance.fields.map(field => field.key)).toEqual(["Coordinates"]);
  expect(entrance.fields[0].previous).toEqual([35, -86]);
  expect(entrance.fields[0].current).toEqual([36, -86]);
  expect(entrance.fields.map(field => field.label)).not.toContain("Name");
  expect(entrance.fields.map(field => field.label)).not.toContain("Reported By");
  expect(entrance.fields.map(field => field.label)).not.toContain("Elevation");
  expect(entrance.fields.map(field => field.label)).not.toContain("Primary");
});

it("preserves false and zero while keeping Narrative separate", () => {
  const diff = emptyDiff();
  diff.scalars = [
    { path: "NumberOfPits", previous: null, current: 0 },
    { path: "IsArchived", previous: true, current: false },
    { path: "Narrative", previous: "old", current: "new" },
  ];
  const model = buildCaveRevisionDiffPresentation(diff, snapshot(), snapshot());

  expect(model.caveInformation.map(field => field.key)).toEqual(["NumberOfPits", "IsArchived"]);
  expect(model.caveInformation[0].change?.current).toBe(0);
  expect(model.caveInformation[1].change?.current).toBe(false);
  expect(model.narrative?.current).toBe("new");
});

it("uses historical State and County labels and preserves County Number intent", () => {
  const previous = snapshot();
  previous.state = { id: "ky", nameAtRevision: "Kentucky", abbreviationAtRevision: "KY" };
  previous.county = { id: "barren", nameAtRevision: "Barren", displayIdAtRevision: "009" };
  const current = snapshot();
  const diff = emptyDiff();
  diff.scalars = [
    { path: "State.Id", previous: "ky", current: "tn" },
    { path: "County.Id", previous: "barren", current: "county" },
    { path: "CountyNumber", previous: 12, current: 0 },
  ];

  const model = buildCaveRevisionDiffPresentation(diff, previous, current, "FirstAvailable");

  expect(model.caveInformation[0].change?.previous).toBe("Kentucky (KY)");
  expect(model.caveInformation[0].change?.current).toBe("Tennessee (TN)");
  expect(model.caveInformation[1].change?.previous).toBe("Barren (009)");
  expect(model.caveInformation[1].change?.current).toBe("Franklin (026)");
  expect(model.caveInformation[2].change?.current).toBe("First available on approval");
});

it("keeps SRID separate and orders entrance fields like Cave detail", () => {
  const previous = snapshot();
  const current = snapshot();
  current.entrances[0] = { ...current.entrances[0], latitude: 36, description: "new", elevation: 0, srid: 4269 };
  const diff = emptyDiff();
  diff.changedEntrances = ["entrance"];
  diff.entranceChanges = [{ entranceId: "entrance", addedTags: [], removedTags: [], scalars: [
    { path: "Srid", previous: 4326, current: 4269 },
    { path: "Elevation", previous: null, current: 0 },
    { path: "Description", previous: "old description", current: "new" },
    { path: "Latitude", previous: 35, current: 36 },
  ] }];

  const fields = buildCaveRevisionDiffPresentation(diff, previous, current).entrances[0].fields;

  expect(fields.map(field => field.label)).toEqual([
    "Coordinates", "Coordinate Reference System", "Description", "Elevation",
  ]);
  expect(fields[3].current).toBe(0);
});

it("does not infer unreported file fields from snapshots", () => {
  const previous = snapshot();
  const current = snapshot();
  current.files[0] = { ...current.files[0], name: "Changed name", extension: ".jpg", fileTypeNameAtRevision: "Renamed" };
  const diff = emptyDiff();
  diff.changedFiles = ["file"];
  diff.fileChanges = [{ fileId: "file", scalars: [{ path: "Name", previous: "Map", current: "Changed name" }] }];

  const fields = buildCaveRevisionDiffPresentation(diff, previous, current).files[0].fields;

  expect(fields.map(field => field.label)).toEqual(["Name"]);
});

it("parses only the defined metadata grammar and attaches metadata to its field", () => {
  expect(parseReferenceMetadataPath("Entrances/e/Tags/EntranceHydrology")).toEqual({
    kind: "entranceField", id: "e", key: "EntranceHydrology",
  });
  expect(parseReferenceMetadataPath("unexpected/EntranceHydrology/value")).toEqual({ kind: "unknown" });
  const diff = emptyDiff();
  diff.changedEntrances = ["entrance"];
  diff.entranceChanges = [{ entranceId: "entrance", scalars: [], addedTags: [], removedTags: [] }];
  diff.referenceMetadataChanges = [
    { path: "Entrances/entrance/LocationQuality", stableId: "exact", property: "LocationQualityNameAtRevision", previousValue: "Exact", currentValue: "Survey Grade" },
    { path: "unexpected/EntranceHydrology/value", stableId: "x", property: "Name", previousValue: "Old", currentValue: "New" },
  ];

  const model = buildCaveRevisionDiffPresentation(diff, snapshot(), snapshot());

  expect(model.entrances[0].fields[0].label).toBe("Location Quality");
  expect(model.entrances[0].fields[0].metadata[0].currentLabel).toBe("Survey Grade");
  expect(model.fallbackMetadata[0].path).toBe("unexpected/EntranceHydrology/value");
});

it("attaches Cave, entrance-tag, and File Type metadata without association changes", () => {
  const diff = emptyDiff();
  diff.changedEntrances = ["entrance"];
  diff.entranceChanges = [{ entranceId: "entrance", scalars: [], addedTags: [], removedTags: [] }];
  diff.changedFiles = ["file"];
  diff.fileChanges = [{ fileId: "file", scalars: [] }];
  diff.referenceMetadataChanges = [
    { path: "State", stableId: "tn", property: "NameAtRevision", previousValue: "Tenn.", currentValue: "Tennessee" },
    { path: "County", stableId: "county", property: "NameAtRevision", previousValue: "Franklin Co.", currentValue: "Franklin" },
    { path: "Tags/Geology", stableId: "lime", property: "NameAtRevision", previousValue: "Limestone", currentValue: "Carbonate" },
    { path: "Entrances/entrance/Tags/EntranceHydrology", stableId: "wet", property: "NameAtRevision", previousValue: "Wet", currentValue: "Water present" },
    { path: "Entrances/entrance/Tags/FutureRole", stableId: "future", property: "NameAtRevision", previousValue: "Old future", currentValue: "New future" },
    { path: "Files/file/FileType", stableId: "map", property: "FileTypeNameAtRevision", previousValue: "Map", currentValue: "Cave Map" },
  ];

  const model = buildCaveRevisionDiffPresentation(diff, snapshot(), snapshot());

  expect(model.caveInformation.map(field => field.label)).toEqual(["State", "County", "Geology"]);
  expect(model.caveInformation.find(field => field.label === "Geology")?.tags?.added).toEqual([]);
  expect(model.entrances[0].tagGroups[0].label).toBe("Hydrology");
  expect(model.entrances[0].tagGroups[0].added).toEqual([]);
  expect(model.entrances[0].tagGroups[1].label).toBe("Future Role");
  expect(model.files[0].fields[0].label).toBe("File Type");
  expect(model.files[0].fields[0].metadata[0].currentLabel).toBe("Cave Map");
});

it("selects complete snapshots for added and removed items and exposes missing IDs", () => {
  const previous = snapshot();
  const current = snapshot();
  current.entrances[0] = { ...current.entrances[0], id: "added", name: "Added" };
  current.files[0] = { ...current.files[0], id: "added-file", name: "Added file" };
  const diff = emptyDiff();
  diff.addedEntrances = ["added"];
  diff.removedEntrances = ["entrance", "missing"];
  diff.addedFiles = ["added-file"];
  diff.removedFiles = ["file", "missing-file"];

  const model = buildCaveRevisionDiffPresentation(diff, previous, current);

  expect(model.entrances.find(item => item.status === "added")?.snapshot?.name).toBe("Added");
  expect(model.entrances.find(item => item.id === "entrance")?.snapshot?.name).toBe("Main");
  expect(model.entrances.find(item => item.id === "missing")?.detailsAvailable).toBe(false);
  expect(model.files.find(item => item.status === "added")?.snapshot?.name).toBe("Added file");
  expect(model.files.find(item => item.id === "file")?.snapshot?.name).toBe("Map");
  expect(model.files.find(item => item.id === "missing-file")?.detailsAvailable).toBe(false);
});

it("shows unknown authoritative scalar paths deterministically", () => {
  const diff = emptyDiff();
  diff.scalars = [
    { path: "Future.ZebraValue", previous: 1, current: 2 },
    { path: "Future.AlphaValue", previous: "a", current: "b" },
  ];
  expect(buildCaveRevisionDiffPresentation(diff).fallbackScalars.map(field => field.key)).toEqual([
    "Future.AlphaValue", "Future.ZebraValue",
  ]);
});
