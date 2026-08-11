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
  addedFiles: [], removedFiles: [], changedFiles: ["file"], referenceMetadataChanges: [],
};

it("shows the actual nested and reference values needed for review", () => {
  render(<CaveRevisionDiff diff={diff} previous={snapshot(false)} current={snapshot(true)} />);

  for (const text of [
    "Tennessee (TN)", "Kentucky (KY)", "Franklin (026)", "Barren (009)",
    "Added Geology: Limestone", "Removed Geology: Sandstone",
    "New entrance description", "Old entrance description", "Survey Grade", "Estimated",
    "New Reporter", "Old Reporter", "35", "34", "-86", "-85", "500", "450", "2026", "2025",
    "42", "12", "Added: Open", "Removed: Closed", "Added: Wet", "Removed: Dry",
    "Added: Sinkhole", "Removed: Spring",
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
