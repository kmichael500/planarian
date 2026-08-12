import { render } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { CaveAvailability, ProposalVersionComparison } from "./CaveChangeRequestPage";
import { CaveChangeRequestSummaryVm, CaveProposalVersionDetailVm } from "../Models/CaveChangeRequestVm";
import { CaveRevisionDiffVm, CaveSnapshotVm } from "../Models/CaveRevisionVm";

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({ matches: false, addListener: () => undefined, removeListener: () => undefined }),
  });
});

const request = (caveExists: boolean): CaveChangeRequestSummaryVm => ({
  id: "request", caveId: "cave", caveName: "Historical Cave", caveExists,
  status: "Rejected", submitterUserId: "submitter", submittedOn: "2026-08-11T00:00:00Z",
  originalBaseRevisionId: "revision", proposalBaseRevisionId: "revision",
  currentProposalVersionId: "proposal", isStale: false, canEdit: false, canReview: false,
});

it("does not render an Open Cave link when the historical Cave is unavailable", () => {
  render(<MemoryRouter><CaveAvailability request={request(false)} /></MemoryRouter>);

  expect(document.body).toHaveTextContent("Cave no longer available");
  expect(document.querySelector("a")).not.toBeInTheDocument();
});

it("renders the live Cave link while the Cave is available", () => {
  render(<MemoryRouter><CaveAvailability request={request(true)} /></MemoryRouter>);

  expect(document.querySelector("a")).toHaveAttribute("href", "/caves/cave");
});

const snapshot = (name: string, tags: CaveSnapshotVm["tags"] = []): CaveSnapshotVm => ({
  caveId: "cave", accountId: "account", name, alternateNames: [], countyNumber: 1,
  state: { id: "tn", nameAtRevision: "Tennessee", abbreviationAtRevision: "TN" },
  county: { id: "county", nameAtRevision: "Coffee", displayIdAtRevision: "016" },
  isArchived: false, tags, entrances: [], files: [],
});

const emptyDiff = (): CaveRevisionDiffVm => ({
  scalars: [], addedTags: [], removedTags: [], addedEntrances: [], removedEntrances: [],
  changedEntrances: [], entranceChanges: [], addedFiles: [], removedFiles: [], changedFiles: [],
  fileChanges: [], referenceMetadataChanges: [],
});

const versionDetail = (later: boolean, baseChanged = false): CaveProposalVersionDetailVm => {
  const cricket = { role: "Biology", tagTypeId: "cricket", nameAtRevision: "Cricket" };
  const base = snapshot("Original");
  const previousProposed = snapshot("Proposed name", [cricket]);
  const proposed = snapshot("Revised proposed name");
  return {
    base, proposed,
    diffFromBase: { ...emptyDiff(), scalars: [{ path: "Name", previous: "Original", current: "Revised proposed name" }] },
    previousProposed: later ? previousProposed : undefined,
    diffFromPreviousVersion: later ? {
      ...emptyDiff(),
      scalars: [{ path: "Name", previous: "Proposed name", current: "Revised proposed name" }],
      removedTags: [cricket],
    } : undefined,
    baseRevisionChanged: baseChanged,
    previousBaseRevisionId: baseChanged ? "revision-one" : undefined,
    baseRevisionId: baseChanged ? "revision-two" : "revision-one",
    countyNumberIntent: "Manual", unavailableStagedFileIds: [],
  };
};

it("labels the first proposal version as an initial base-relative proposal", () => {
  render(<ProposalVersionComparison detail={versionDetail(false)} />);

  expect(document.body).toHaveTextContent("Initial proposal");
  expect(document.body).toHaveTextContent("Published base → proposal");
  expect(document.body).not.toHaveTextContent("Changes from previous proposal version");
});

it("shows a later version delta with removed tags and keeps the base comparison separate", () => {
  render(<ProposalVersionComparison detail={versionDetail(true)} />);

  expect(document.body).toHaveTextContent("Changes from previous proposal version");
  expect(document.body).toHaveTextContent("Biology");
  expect(document.body).toHaveTextContent("− Removed Cricket");
  expect(document.body).toHaveTextContent("This version vs published base");
});

it("shows when a proposal version changed published bases", () => {
  render(<ProposalVersionComparison detail={versionDetail(true, true)} />);

  expect(document.body).toHaveTextContent("This proposal version is based on a newer published Cave revision.");
  expect(document.body).toHaveTextContent("Previous base: revision-one. This base: revision-two.");
});
