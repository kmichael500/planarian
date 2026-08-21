import { render, screen } from "@testing-library/react";
import { CaveChangeRequestDetailVm } from "../Models/CaveChangeRequestVm";
import { CaveRevisionDiffVm, CaveSnapshotVm } from "../Models/CaveRevisionVm";
import { analyzeStaleReview, CaveStaleReviewContext } from "./CaveStaleReviewContext";

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({ matches: false, addListener: () => undefined, removeListener: () => undefined,
      addEventListener: () => undefined, removeEventListener: () => undefined, dispatchEvent: () => false }),
  });
});

const snapshot = (): CaveSnapshotVm => ({
  caveId: "cave", accountId: "account", name: "Cave", alternateNames: [],
  state: { id: "tn", nameAtRevision: "Tennessee" },
  county: { id: "county", nameAtRevision: "Coffee" }, countyNumber: 1,
  isArchived: false, tags: [],
  entrances: [{ id: "entrance", name: "Main Entrance", isPrimary: true, srid: 4326,
    locationQualityTagId: "quality", locationQualityNameAtRevision: "Surveyed", tags: [] }],
  files: [{ id: "file", name: "Survey Map", extension: ".pdf", fileTypeTagId: "map",
    fileTypeNameAtRevision: "Map" }],
  linePlots: [{ id: "line", name: "Main Survey", contentHash: "hash" }],
});

const emptyDiff = (): CaveRevisionDiffVm => ({
  scalars: [], addedTags: [], removedTags: [], addedEntrances: [], removedEntrances: [],
  changedEntrances: [], entranceChanges: [], addedFiles: [], removedFiles: [], changedFiles: [],
  fileChanges: [], addedLinePlots: [], removedLinePlots: [], changedLinePlots: [], linePlotChanges: [],
  referenceMetadataChanges: [],
});
const detail = (proposalDiff: CaveRevisionDiffVm, publishedDiff: CaveRevisionDiffVm): CaveChangeRequestDetailVm => {
  const base = snapshot();
  return {
    request: { id: "request", caveId: "cave", caveName: "Cave", caveExists: true, status: "Pending",
      submitterUserId: "user", submittedOn: "2026-08-21T00:00:00Z", originalBaseRevisionId: "base",
      proposalBaseRevisionId: "base", currentRevisionId: "current", currentProposalVersionId: "proposal",
      isStale: true, canEdit: true, canReview: true,
      changeSummary: { caveFields: 0, entrances: { added: 0, removed: 0, changed: 0, total: 0 },
        files: { added: 0, removed: 0, changed: 0, total: 0 },
        linePlots: { added: 0, removed: 0, changed: 0, total: 0 }, otherChanges: 0 } },
    base, proposed: base, current: base, diff: proposalDiff, publishedSinceBase: publishedDiff,
    versions: [], countyNumberIntent: "Manual", linePlots: [], activeStagedFiles: [], unavailableStagedFileIds: [],
  };
};

it("separates non-overlapping submitted areas from later published changes", () => {
  const proposal = { ...emptyDiff(), scalars: [{ path: "Name", previous: "Old", current: "Proposed" }] };
  const published = { ...emptyDiff(), scalars: [{ path: "LengthFeet", previous: 100, current: 120 }] };

  const analysis = analyzeStaleReview(detail(proposal, published));

  expect(analysis.attentionItems).toEqual([]);
  expect(analysis.nonOverlappingSubmittedAreas).toBe(1);
});
it("flags Cave fields and matching asset identities changed on both sides", () => {
  const proposal = { ...emptyDiff(),
    scalars: [{ path: "Narrative", previous: "Old", current: "Proposed" }],
    changedEntrances: ["entrance"], entranceChanges: [{ entranceId: "entrance",
      scalars: [{ path: "Description", previous: "Old", current: "Proposed" }], addedTags: [], removedTags: [] }],
    changedFiles: ["file"], fileChanges: [{ fileId: "file",
      scalars: [{ path: "Name", previous: "Survey", current: "Proposal map" }] }],
    changedLinePlots: ["line"], linePlotChanges: [{ linePlotId: "line",
      scalars: [{ path: "Name", previous: "Main Survey", current: "Proposal survey" }] }],
  };
  const published = { ...emptyDiff(),
    scalars: [{ path: "Narrative", previous: "Old", current: "Published" }],
    changedEntrances: ["entrance"], entranceChanges: [{ entranceId: "entrance",
      scalars: [{ path: "Description", previous: "Old", current: "Published" }], addedTags: [], removedTags: [] }],
    removedFiles: ["file"], changedLinePlots: ["line"], linePlotChanges: [{ linePlotId: "line",
      scalars: [{ path: "Name", previous: "Main Survey", current: "Published survey" }] }],
  };

  const analysis = analyzeStaleReview(detail(proposal, published));

  expect(analysis.attentionItems.map(item => `${item.kind}: ${item.label}`)).toEqual([
    "Cave field: Narrative", "Entrance: Main Entrance — Description", "File: Survey Map — Name", "Line plot: Main Survey — Name",
  ]);
  expect(analysis.nonOverlappingSubmittedAreas).toBe(0);
});

it("does not flag different fields on the same entrance as a direct overlap", () => {
  const proposal = { ...emptyDiff(), changedEntrances: ["entrance"], entranceChanges: [{ entranceId: "entrance",
    scalars: [{ path: "Description", previous: "Old", current: "Submitted" }], addedTags: [], removedTags: [] }] };
  const published = { ...emptyDiff(), changedEntrances: ["entrance"], entranceChanges: [{ entranceId: "entrance",
    scalars: [{ path: "Elevation", previous: 100, current: 110 }], addedTags: [], removedTags: [] }] };

  const analysis = analyzeStaleReview(detail(proposal, published));

  expect(analysis.attentionItems).toEqual([]);
  expect(analysis.nonOverlappingSubmittedAreas).toBe(1);
});

it("keeps comparison details collapsed so the re-review editor remains primary", () => {
  const proposal = { ...emptyDiff(), scalars: [{ path: "Name", previous: "Old", current: "Submitted" }] };
  const published = { ...emptyDiff(), scalars: [{ path: "LengthFeet", previous: 100, current: 120 }] };

  render(<CaveStaleReviewContext detail={detail(proposal, published)} showOriginalSubmission />);

  expect(screen.getByRole("button", { name: /Original submission/ })).toHaveAttribute("aria-expanded", "false");
  expect(screen.getByRole("button", { name: /Changes made after submission/ })).toHaveAttribute("aria-expanded", "false");
});

it("explains overlapping edits in plain language without internal field-type labels", () => {
  const proposal = { ...emptyDiff(),
    scalars: [{ path: "Narrative", previous: "Old", current: "Submitted" }] };
  const published = { ...emptyDiff(),
    scalars: [{ path: "Narrative", previous: "Old", current: "Newer" }] };

  render(<CaveStaleReviewContext detail={detail(proposal, published)} />);

  expect(screen.getByText("1 part of this submission was changed again later")).toBeInTheDocument();
  expect(screen.getByText("Narrative")).toBeInTheDocument();
  expect(screen.queryByText("Cave field")).not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: /Changes made after submission/ })).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: /Original submission/ })).not.toBeInTheDocument();
});
