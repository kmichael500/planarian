import { render } from "@testing-library/react";
import { CaveRevisionDiff } from "./CaveRevisionDiff";
import { CaveRevisionDiffVm, CaveSnapshotVm } from "../Models/CaveRevisionVm";

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({ matches: false, addListener: () => undefined, removeListener: () => undefined }),
  });
});

const snapshot = (current: boolean): CaveSnapshotVm => ({
  caveId: "cave", accountId: "account", name: "Cave", alternateNames: [], countyNumber: 1,
  state: current
    ? { id: "tn", nameAtRevision: "Tennessee", abbreviationAtRevision: "TN" }
    : { id: "ky", nameAtRevision: "Kentucky", abbreviationAtRevision: "KY" },
  county: current
    ? { id: "franklin", nameAtRevision: "Franklin", displayIdAtRevision: "026" }
    : { id: "barren", nameAtRevision: "Barren", displayIdAtRevision: "009" },
  isArchived: false,
  tags: current
    ? [{ role: "Geology", tagTypeId: "limestone", nameAtRevision: "Limestone" }]
    : [{ role: "Geology", tagTypeId: "sandstone", nameAtRevision: "Sandstone" }],
  entrances: [{
    id: "entrance", name: current ? "Main Entrance" : "Old Entrance", isPrimary: current,
    description: current ? "New entrance description" : "Old entrance description",
    reportedByUserId: current ? "new-user" : "old-user",
    reportedByNameAtRevision: current ? "New Reporter" : "Old Reporter",
    latitude: current ? 35 : 34, longitude: current ? -86 : -85, elevation: current ? 500 : 450, srid: 4326,
    locationQualityTagId: current ? "surveyed" : "estimated",
    locationQualityNameAtRevision: current ? "Survey Grade" : "Estimated",
    reportedOn: current ? "2026-08-10T00:00:00Z" : "2025-08-10T00:00:00Z",
    pitDepthFeet: current ? 42 : 12,
    tags: current
      ? [
        { role: "EntranceStatus", tagTypeId: "open", nameAtRevision: "Open" },
        { role: "EntranceHydrology", tagTypeId: "wet", nameAtRevision: "Wet" },
        { role: "FieldIndication", tagTypeId: "sink", nameAtRevision: "Sinkhole" },
      ]
      : [
        { role: "EntranceStatus", tagTypeId: "closed", nameAtRevision: "Closed" },
        { role: "EntranceHydrology", tagTypeId: "dry", nameAtRevision: "Dry" },
        { role: "FieldIndication", tagTypeId: "spring", nameAtRevision: "Spring" },
      ],
  }],
  files: [{
    id: "file", fileTypeTagId: current ? "map" : "report",
    fileTypeNameAtRevision: current ? "Map" : "Report",
    fileName: current ? "survey-map.pdf" : "survey.pdf",
    displayName: current ? "Survey Map" : "Survey",
  }],
});

const diff: CaveRevisionDiffVm = {
  scalars: [
    { path: "State.Id", previous: "ky", current: "tn" },
    { path: "County.Id", previous: "barren", current: "franklin" },
  ],
  addedTags: [{ role: "Geology", tagTypeId: "limestone", nameAtRevision: "Limestone" }],
  removedTags: [{ role: "Geology", tagTypeId: "sandstone", nameAtRevision: "Sandstone" }],
  addedEntrances: [], removedEntrances: [], changedEntrances: ["entrance"],
  entranceChanges: [{
    entranceId: "entrance",
    scalars: [
      { path: "Description", previous: "Old entrance description", current: "New entrance description" },
      { path: "ReportedByUserId", previous: "old-user", current: "new-user" },
      { path: "Latitude", previous: 34, current: 35 },
      { path: "Longitude", previous: -85, current: -86 },
      { path: "Elevation", previous: 450, current: 500 },
      { path: "LocationQualityTagId", previous: "estimated", current: "surveyed" },
      { path: "ReportedOn", previous: "2025-08-10T00:00:00Z", current: "2026-08-10T00:00:00Z" },
      { path: "PitDepthFeet", previous: 12, current: 42 },
    ],
    removedTags: [
      { role: "EntranceStatus", tagTypeId: "closed", nameAtRevision: "Closed" },
      { role: "EntranceHydrology", tagTypeId: "dry", nameAtRevision: "Dry" },
      { role: "FieldIndication", tagTypeId: "spring", nameAtRevision: "Spring" },
    ],
    addedTags: [
      { role: "EntranceStatus", tagTypeId: "open", nameAtRevision: "Open" },
      { role: "EntranceHydrology", tagTypeId: "wet", nameAtRevision: "Wet" },
      { role: "FieldIndication", tagTypeId: "sink", nameAtRevision: "Sinkhole" },
    ],
  }],
  addedFiles: [], removedFiles: [], changedFiles: ["file"],
  fileChanges: [{ fileId: "file", scalars: [
    { path: "DisplayName", previous: "Survey", current: "Survey Map" },
    { path: "FileName", previous: "survey.pdf", current: "survey-map.pdf" },
    { path: "FileTypeTagId", previous: "report", current: "map" },
  ] }],
  referenceMetadataChanges: [],
};

it("shows the actual nested and reference values needed for review", () => {
  render(<CaveRevisionDiff diff={diff} previous={snapshot(false)} current={snapshot(true)} />);

  for (const text of [
    "Tennessee (TN)", "Kentucky (KY)", "Franklin (026)", "Barren (009)",
    "Geology", "+ Added Limestone", "− Removed Sandstone",
    "New entrance description", "Survey Grade", "Estimated",
    "New Reporter", "Old Reporter", "35", "34", "-86", "-85", "500", "450", "2026", "2025",
    "42 ft", "12 ft", "+ Added Open", "− Removed Closed", "+ Added Wet", "− Removed Dry",
    "+ Added Sinkhole", "− Removed Spring",
    "Survey Map", "Survey", "survey-map.pdf", "survey.pdf", "Map", "Report",
  ]) expect(document.body).toHaveTextContent(text);
});

it.each([
  ["AutomaticNext", "Auto-assigned on approval"],
  ["FirstAvailable", "First available on approval"],
] as const)("presents %s county-number intent without a fake zero", (countyNumberIntent, label) => {
  render(<CaveRevisionDiff
    diff={{ ...diff, scalars: [{ path: "CountyNumber", previous: 12, current: 0 }] }}
    previous={snapshot(false)} current={{ ...snapshot(true), countyNumber: 0 }}
    countyNumberIntent={countyNumberIntent} />);
  expect(document.body).toHaveTextContent(label);
  expect(document.body).not.toHaveTextContent("County Number0");
});

it("renders motivating Cave tags as distinct grouped field rows", () => {
  const groupedDiff: CaveRevisionDiffVm = {
    ...diff,
    scalars: [], addedEntrances: [], removedEntrances: [], changedEntrances: [], entranceChanges: [],
    addedFiles: [], removedFiles: [], changedFiles: [], fileChanges: [],
    removedTags: [
      { role: "GeologicAge", tagTypeId: "miss", nameAtRevision: "Mississippian" },
      { role: "PhysiographicProvince", tagTypeId: "west", nameAtRevision: "Western Cumberland Plateau Escarpment" },
    ],
    addedTags: [
      { role: "PhysiographicProvince", tagTypeId: "high", nameAtRevision: "Highland Rim Escarpment" },
      { role: "PhysiographicProvince", tagTypeId: "east", nameAtRevision: "Eastern Cumberland Plateau Escarpment" },
      { role: "Biology", tagTypeId: "cricket", nameAtRevision: "Cricket" },
      { role: "Cartographer", tagTypeId: "david", nameAtRevision: "David Parr" },
      { role: "Cartographer", tagTypeId: "oliver", nameAtRevision: "Oliver Dattilo" },
    ],
  };
  render(<CaveRevisionDiff diff={groupedDiff} previous={snapshot(false)} current={snapshot(true)} />);

  for (const label of ["Geologic Age", "Physiographic Province", "Biology", "Cartographers"])
    expect(document.body).toHaveTextContent(label);
  expect(document.body).not.toHaveTextContent("Added Biology: Cricket");
  expect(document.body).not.toHaveTextContent("Added Cartographer: David Parr");
});

it("renders complete added and removed entrance and file state", () => {
  const previous = snapshot(false);
  const current = snapshot(true);
  previous.entrances[0] = { ...previous.entrances[0], id: "removed", name: "Old North Entrance" };
  current.entrances[0] = { ...current.entrances[0], id: "added", name: "Carr Entrance" };
  previous.files[0] = { ...previous.files[0], id: "removed-file" };
  current.files[0] = { ...current.files[0], id: "added-file" };
  render(<CaveRevisionDiff diff={{ ...diff,
    scalars: [], addedTags: [], removedTags: [],
    addedEntrances: ["added"], removedEntrances: ["removed"], changedEntrances: [], entranceChanges: [],
    addedFiles: ["added-file"], removedFiles: ["removed-file"], changedFiles: [], fileChanges: [],
  }} previous={previous} current={current} />);

  expect(document.body).toHaveTextContent("CARR ENTRANCEAdded");
  expect(document.body).toHaveTextContent("OLD NORTH ENTRANCERemoved");
  expect(document.body).toHaveTextContent("New entrance description");
  expect(document.body).toHaveTextContent("Old entrance description");
  expect(document.body).toHaveTextContent("Survey MapAdded");
  expect(document.body).toHaveTextContent("SurveyRemoved");
});

it("renders reference label updates neutrally and missing snapshots visibly", () => {
  render(<CaveRevisionDiff diff={{ ...diff,
    scalars: [], addedTags: [], removedTags: [],
    addedEntrances: [], removedEntrances: ["missing"], changedEntrances: [], entranceChanges: [],
    addedFiles: [], removedFiles: [], changedFiles: [], fileChanges: [],
    referenceMetadataChanges: [{ path: "Tags/Geology", stableId: "limestone", property: "NameAtRevision",
      previousValue: "Limestone Formation", currentValue: "Monteagle Limestone" }],
  }} previous={snapshot(false)} current={snapshot(true)} />);

  expect(document.body).toHaveTextContent("Label updated");
  expect(document.body).toHaveTextContent("Limestone Formation → Monteagle Limestone");
  expect(document.body).toHaveTextContent("Same referenced value");
  expect(document.body).not.toHaveTextContent("Removed Limestone Formation");
  expect(document.body).toHaveTextContent("Entrance details unavailable");
  expect(document.body).toHaveTextContent("Entrance ID: missing");
});

it("renders Narrative and changed Description with prose diff controls", () => {
  render(<CaveRevisionDiff diff={{ ...diff,
    scalars: [{ path: "Narrative", previous: "Old cave narrative", current: "New cave narrative" }],
  }} previous={snapshot(false)} current={snapshot(true)} />);
  expect(document.body).toHaveTextContent("Narrative");
  expect(Array.from(document.querySelectorAll('input[type="radio"]')).filter(input =>
    input.parentElement?.textContent === "Changes")).toHaveLength(2);
});

it("retains initial-publication and no-visible-change states", () => {
  const { rerender } = render(<CaveRevisionDiff current={snapshot(true)} />);
  expect(document.body).toHaveTextContent("Initial publication");
  rerender(<CaveRevisionDiff diff={{ ...diff, scalars: [], addedTags: [], removedTags: [],
    addedEntrances: [], removedEntrances: [], changedEntrances: [], entranceChanges: [],
    addedFiles: [], removedFiles: [], changedFiles: [], fileChanges: [], referenceMetadataChanges: [] }} />);
  expect(document.body).toHaveTextContent("No visible field changes");
});
