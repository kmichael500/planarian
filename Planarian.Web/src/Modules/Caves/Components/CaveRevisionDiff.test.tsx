import { render, screen } from "@testing-library/react";
import { CaveRevisionDiff } from "./CaveRevisionDiff";
import { CaveRevisionDiffVm, CaveSnapshotVm } from "../Models/CaveRevisionVm";

let desktopLayout = false;

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({
      matches: desktopLayout,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }),
  });
});

beforeEach(() => {
  desktopLayout = false;
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
        { role: "EntranceReportedBy", tagTypeId: "new-reporter", nameAtRevision: "New Reporter" },
      ]
      : [
        { role: "EntranceStatus", tagTypeId: "closed", nameAtRevision: "Closed" },
        { role: "EntranceHydrology", tagTypeId: "dry", nameAtRevision: "Dry" },
        { role: "FieldIndication", tagTypeId: "spring", nameAtRevision: "Spring" },
        { role: "EntranceReportedBy", tagTypeId: "old-reporter", nameAtRevision: "Old Reporter" },
      ],
  }],
  files: [{
    id: "file", fileTypeTagId: current ? "map" : "report",
    fileTypeNameAtRevision: current ? "Map" : "Report",
    name: current ? "Survey Map" : "Survey",
    extension: ".pdf",
  }],
  linePlots: [{ id: "line", name: current ? "Main line" : "Old line", contentHash: current ? "newhash" : "oldhash" }],
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
      { role: "EntranceReportedBy", tagTypeId: "old-reporter", nameAtRevision: "Old Reporter" },
    ],
    addedTags: [
      { role: "EntranceStatus", tagTypeId: "open", nameAtRevision: "Open" },
      { role: "EntranceHydrology", tagTypeId: "wet", nameAtRevision: "Wet" },
      { role: "FieldIndication", tagTypeId: "sink", nameAtRevision: "Sinkhole" },
      { role: "EntranceReportedBy", tagTypeId: "new-reporter", nameAtRevision: "New Reporter" },
    ],
  }],
  addedFiles: [], removedFiles: [], changedFiles: ["file"],
  fileChanges: [{ fileId: "file", scalars: [
    { path: "Name", previous: "Survey", current: "Survey Map" },
    { path: "FileTypeTagId", previous: "report", current: "map" },
  ] }],
  addedLinePlots: [], removedLinePlots: [], changedLinePlots: ["line"],
  linePlotChanges: [{ linePlotId: "line", scalars: [
    { path: "Name", previous: "Old line", current: "Main line" },
    { path: "ContentHash", previous: "oldhash", current: "newhash" },
  ] }],
  referenceMetadataChanges: [],
};

it("shows the actual nested and reference values needed for review", () => {
  render(<CaveRevisionDiff diff={diff} previous={snapshot(false)} current={snapshot(true)} />);

  for (const text of [
    "Tennessee (TN)", "Kentucky (KY)", "Franklin (026)", "Barren (009)",
    "Geology", "+ Limestone", "− Sandstone",
    "New entrance description", "Survey Grade", "Estimated",
    "New Reporter", "Old Reporter", "35", "34", "-86", "-85", "500", "450", "2026", "2025",
    "42 ft", "12 ft", "+ Open", "− Closed", "+ Wet", "− Dry",
    "+ Sinkhole", "− Spring",
    "Survey Map", "Survey", "Map", "Report",
    "Line Plots", "Main line", "Old line", "Updated content", "Previous content",
  ]) expect(document.body).toHaveTextContent(text);
});

it("renders a broad scope summary with separate entrance statuses", () => {
  const broadDiff: CaveRevisionDiffVm = {
    ...diff,
    scalars: [{ path: "Name", previous: "Old name", current: "New name" }],
    addedTags: [], removedTags: [],
    changedEntrances: ["changed-1", "changed-2"], addedEntrances: ["added"], removedEntrances: ["removed"],
    entranceChanges: [],
    addedFiles: ["file"], removedFiles: [], changedFiles: [], fileChanges: [],
    addedLinePlots: ["line"], removedLinePlots: [], changedLinePlots: [], linePlotChanges: [],
  };

  render(<CaveRevisionDiff diff={broadDiff} />);

  expect(screen.getByText(
    "1 cave field · 2 entrances changed · 1 entrance added · 1 entrance removed · 1 file · 1 line plot"
  )).toBeInTheDocument();
});

it("uses one Before / After header for desktop Cave field comparison", () => {
  desktopLayout = true;
  const scalarDiff: CaveRevisionDiffVm = {
    ...diff,
    scalars: [
      { path: "Name", previous: "Old name", current: "New name" },
      { path: "NumberOfPits", previous: 2, current: 3 },
    ],
    addedTags: [], removedTags: [],
    addedEntrances: [], removedEntrances: [], changedEntrances: [], entranceChanges: [],
    addedFiles: [], removedFiles: [], changedFiles: [], fileChanges: [],
    addedLinePlots: [], removedLinePlots: [], changedLinePlots: [], linePlotChanges: [],
  };

  render(<CaveRevisionDiff diff={scalarDiff} previous={snapshot(false)} current={snapshot(true)} />);

  expect(screen.getAllByRole("columnheader", { name: "Before" })).toHaveLength(1);
  expect(screen.getAllByRole("columnheader", { name: "After" })).toHaveLength(1);
  expect(screen.getByRole("row", { name: "Name Old name New name" })).toBeInTheDocument();
  expect(screen.getByRole("row", { name: "Number of Pits 2 3" })).toBeInTheDocument();
});

it("shows Alternative Names as collection additions and removals", () => {
  const previous = { ...snapshot(false), alternateNames: ["Keep", "Old alias"] };
  const current = { ...snapshot(true), alternateNames: ["Keep", "New alias"] };
  const aliasDiff: CaveRevisionDiffVm = {
    ...diff,
    scalars: [{ path: "AlternateNames", previous: previous.alternateNames, current: current.alternateNames }],
    addedTags: [], removedTags: [],
    addedEntrances: [], removedEntrances: [], changedEntrances: [], entranceChanges: [],
    addedFiles: [], removedFiles: [], changedFiles: [], fileChanges: [],
    addedLinePlots: [], removedLinePlots: [], changedLinePlots: [], linePlotChanges: [],
  };

  render(<CaveRevisionDiff diff={aliasDiff} previous={previous} current={current} />);

  expect(screen.getByLabelText("Removed Old alias")).toHaveTextContent("− Old alias");
  expect(screen.getByLabelText("Added New alias")).toHaveTextContent("+ New alias");
  expect(screen.queryByText("Keep")).not.toBeInTheDocument();
});

it("uses a unified signed scalar diff on mobile without repeated Before / After labels", () => {
  const scalarDiff: CaveRevisionDiffVm = {
    ...diff,
    scalars: [{ path: "Name", previous: "Old name", current: "New name" }],
    addedTags: [], removedTags: [],
    addedEntrances: [], removedEntrances: [], changedEntrances: [], entranceChanges: [],
    addedFiles: [], removedFiles: [], changedFiles: [], fileChanges: [],
    addedLinePlots: [], removedLinePlots: [], changedLinePlots: [], linePlotChanges: [],
  };

  render(<CaveRevisionDiff diff={scalarDiff} previous={snapshot(false)} current={snapshot(true)} />);

  const change = screen.getByRole("group", { name: "Name change" });
  expect(change).toHaveTextContent("− Old name");
  expect(change).toHaveTextContent("+ New name");
  expect(change).not.toHaveTextContent("Before");
  expect(change).not.toHaveTextContent("After");
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

it("renders concise signed tag changes while preserving accessible operation names", () => {
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
  expect(screen.getByLabelText("Removed Mississippian")).toHaveTextContent("− Mississippian");
  expect(screen.getByLabelText("Added Cricket")).toHaveTextContent("+ Cricket");
  expect(document.body).not.toHaveTextContent("Removed Mississippian");
  expect(document.body).not.toHaveTextContent("Added Cricket");
});

it("renders complete added and removed entrance and file state", () => {
  const previous = snapshot(false);
  const current = snapshot(true);
  previous.entrances[0] = { ...previous.entrances[0], id: "removed", name: "Old North Entrance" };
  current.entrances[0] = { ...current.entrances[0], id: "added", name: "Carr Entrance" };
  previous.files[0] = { ...previous.files[0], id: "removed-file", extension: ".txt" };
  current.files[0] = { ...current.files[0], id: "added-file" };
  render(<CaveRevisionDiff diff={{ ...diff,
    scalars: [], addedTags: [], removedTags: [],
    addedEntrances: ["added"], removedEntrances: ["removed"], changedEntrances: [], entranceChanges: [],
    addedFiles: ["added-file"], removedFiles: ["removed-file"], changedFiles: [], fileChanges: [],
  }} previous={previous} current={current} />);

  expect(document.body).toHaveTextContent("Carr EntranceAdded");
  expect(document.body).toHaveTextContent("Old North EntranceRemoved");
  expect(document.body).toHaveTextContent("New entrance description");
  expect(document.body).toHaveTextContent("Old entrance description");
  expect(document.body).toHaveTextContent("Survey MapAdded");
  expect(document.body).toHaveTextContent("SurveyRemoved");
  expect(document.body).toHaveTextContent(".pdf");
  expect(document.body).toHaveTextContent(".txt");
  expect(document.body).toHaveTextContent("Map");
  expect(document.body).toHaveTextContent("Report");
});

it("keeps added entity snapshots readable without inventing Before / After values", () => {
  const current = snapshot(true);
  current.entrances[0] = { ...current.entrances[0], id: "added-entrance", name: "Carr Entrance" };
  current.files[0] = { ...current.files[0], id: "added-file" };
  current.linePlots[0] = { ...current.linePlots[0], id: "added-line" };
  const entityDiff: CaveRevisionDiffVm = {
    ...diff,
    scalars: [], addedTags: [], removedTags: [],
    addedEntrances: ["added-entrance"], removedEntrances: [], changedEntrances: [], entranceChanges: [],
    addedFiles: ["added-file"], removedFiles: [], changedFiles: [], fileChanges: [],
    addedLinePlots: ["added-line"], removedLinePlots: [], changedLinePlots: [], linePlotChanges: [],
  };

  render(<CaveRevisionDiff diff={entityDiff} current={current} />);

  expect(document.body).toHaveTextContent("Carr EntranceAdded");
  expect(document.body).toHaveTextContent("Survey MapAdded");
  expect(document.body).toHaveTextContent("Main lineAdded");
  expect(screen.getByText("GeoJSON content")).toBeInTheDocument();
  expect(document.body).not.toHaveTextContent("newhash");
  expect(screen.queryByText("Before")).not.toBeInTheDocument();
  expect(screen.queryByText("After")).not.toBeInTheDocument();
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
  expect(screen.getByText("Narrative")).toBeInTheDocument();
  expect(screen.getAllByRole("radio", { name: "Changes" })).toHaveLength(2);
});

it("retains initial-publication and no-visible-change states", () => {
  const { rerender } = render(<CaveRevisionDiff current={snapshot(true)} />);
  expect(document.body).toHaveTextContent("Initial publication");
  rerender(<CaveRevisionDiff diff={{ ...diff, scalars: [], addedTags: [], removedTags: [],
    addedEntrances: [], removedEntrances: [], changedEntrances: [], entranceChanges: [],
    addedFiles: [], removedFiles: [], changedFiles: [], fileChanges: [],
    addedLinePlots: [], removedLinePlots: [], changedLinePlots: [], linePlotChanges: [],
    referenceMetadataChanges: [] }} />);
  expect(document.body).toHaveTextContent("No visible field changes");
});
