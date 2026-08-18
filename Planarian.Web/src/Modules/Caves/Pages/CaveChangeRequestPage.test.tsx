import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { createMemoryRouter, MemoryRouter, RouterProvider } from "react-router-dom";
import { CaveAvailability, CaveChangeRequestPage, getDownloadableProposalFiles, ProposalVersionComparison,
  UnavailableProposalFilesAlert } from "./CaveChangeRequestPage";
import { CaveChangeRequestDetailVm, CaveChangeRequestSummaryVm, CaveProposalVersionDetailVm } from "../Models/CaveChangeRequestVm";
import { CaveRevisionDiffVm, CaveSnapshotVm } from "../Models/CaveRevisionVm";
import { CaveService } from "../Service/CaveService";

type MockPlanarianModalProps = {
  open: boolean;
  header?: ReactNode;
  footer?: ReactNode | ReactNode[];
  children?: ReactNode;
};

jest.mock("../../../Shared/Components/Buttons/PlanarianModal", () => ({
  PlanarianModal: ({ open, header, footer, children }: MockPlanarianModalProps) => open ? (
    <div role="dialog" aria-label={typeof header === "string" ? header : undefined}>
      {children}
      {footer}
    </div>
  ) : null,
}));

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

  expect(screen.getByText("Cave no longer available")).toBeInTheDocument();
  expect(screen.queryByRole("link", { name: "Open Cave" })).not.toBeInTheDocument();
});

it("renders the live Cave link while the Cave is available", () => {
  render(<MemoryRouter><CaveAvailability request={request(true)} /></MemoryRouter>);

  expect(screen.getByRole("link", { name: "Open Cave" })).toHaveAttribute("href", "/caves/cave");
});

it("offers downloads only for live staged files, not historical attachment placeholders", () => {
  const live = { id: "live", fileTypeTagId: "map", fileTypeNameAtRevision: "Map",
    name: "Live survey", extension: ".pdf" };
  const unavailable = { ...live, id: "unavailable", name: "Historical survey",
    extension: ".pdf" };
  const baseSnapshot = snapshot("Original");
  const detail = {
    request: request(true), base: baseSnapshot, current: baseSnapshot,
    proposed: { ...baseSnapshot, files: [live, unavailable] }, diff: emptyDiff(), versions: [],
    countyNumberIntent: "Manual", activeStagedFiles: [live],
    unavailableStagedFileIds: [unavailable.id],
  } as CaveChangeRequestDetailVm;

  expect(getDownloadableProposalFiles(detail)).toEqual([live]);
  expect(getDownloadableProposalFiles(detail)).not.toContainEqual(unavailable);

  render(<UnavailableProposalFilesAlert fileIds={detail.unavailableStagedFileIds}
    snapshots={[detail.proposed]} />);
  expect(screen.getByText("Historical attachment unavailable")).toBeInTheDocument();
  expect(screen.getByText("Historical survey.pdf")).toBeInTheDocument();
  expect(screen.queryByRole("link")).not.toBeInTheDocument();
});

const snapshot = (name: string, tags: CaveSnapshotVm["tags"] = []): CaveSnapshotVm => ({
  caveId: "cave", accountId: "account", name, alternateNames: [], countyNumber: 1,
  state: { id: "tn", nameAtRevision: "Tennessee", abbreviationAtRevision: "TN" },
  county: { id: "county", nameAtRevision: "Coffee", displayIdAtRevision: "016" },
  isArchived: false, tags, entrances: [], files: [], linePlots: [],
});

const emptyDiff = (): CaveRevisionDiffVm => ({
  scalars: [], addedTags: [], removedTags: [], addedEntrances: [], removedEntrances: [],
  changedEntrances: [], entranceChanges: [], addedFiles: [], removedFiles: [], changedFiles: [],
  fileChanges: [], addedLinePlots: [], removedLinePlots: [], changedLinePlots: [], linePlotChanges: [], referenceMetadataChanges: [],
});

const pageRequest = (overrides: Partial<CaveChangeRequestSummaryVm> = {}): CaveChangeRequestSummaryVm => ({
  id: "request0001", caveId: "cave000001", caveName: "Helms Deep Cave", caveExists: true,
  status: "Pending", submitterUserId: "submitter", submitterName: "Michael Test",
  submittedOn: "2026-08-17T12:00:00", updatedOn: "2026-08-17T13:00:00",
  originalBaseRevisionId: "base000001", proposalBaseRevisionId: "base000001",
  currentRevisionId: "base000001", currentProposalVersionId: "version0002", isStale: false,
  canEdit: true, canReview: true, ...overrides,
});

const pageDetail = (overrides: Partial<CaveChangeRequestDetailVm> = {}): CaveChangeRequestDetailVm => {
  const base = snapshot("Helms Deep Cave");
  return {
    request: pageRequest(), base, current: base,
    proposed: { ...base, name: "Helms Deep Cave Updated" },
    diff: { ...emptyDiff(), scalars: [{ path: "Name", previous: "Helms Deep Cave", current: "Helms Deep Cave Updated" }] },
    versions: [
      { id: "version0001", baseRevisionId: "base000001", createdByUserId: "submitter",
        createdByName: "Michael Test", createdOn: "2026-08-17T12:00:00", isCurrent: false },
      { id: "version0002", previousProposalVersionId: "version0001", baseRevisionId: "base000001",
        createdByUserId: "reviewer", createdByName: "Riley Reviewer", createdOn: "2026-08-17T17:47:00", isCurrent: true },
    ],
    countyNumberIntent: "Manual", activeStagedFiles: [], unavailableStagedFileIds: [], ...overrides,
  };
};

const renderRequestPage = (detail: CaveChangeRequestDetailVm,
  refreshed: CaveChangeRequestDetailVm = detail) => {
  const getRequest = jest.spyOn(CaveService, "GetChangeRequest")
    .mockResolvedValueOnce(detail).mockResolvedValue(refreshed);
  const router = createMemoryRouter([{ path: "/caves/requests/:requestId", element: <CaveChangeRequestPage /> }],
    { initialEntries: ["/caves/requests/request0001"] });
  render(<RouterProvider router={router} />);
  return { router, getRequest };
};

afterEach(() => jest.restoreAllMocks());

it("shows an intentional loading state while the request is being fetched", () => {
  jest.spyOn(CaveService, "GetChangeRequest").mockImplementation(() => new Promise(() => undefined));
  const router = createMemoryRouter([{ path: "/caves/requests/:requestId", element: <CaveChangeRequestPage /> }],
    { initialEntries: ["/caves/requests/request0001"] });

  render(<RouterProvider router={router} />);

  expect(screen.getByRole("status", { name: "Loading change request" })).toHaveTextContent("Loading change request…");
});

it("shows an explicit unavailable state when the request cannot be loaded", async () => {
  jest.spyOn(CaveService, "GetChangeRequest").mockRejectedValue(new Error("load failed"));
  const router = createMemoryRouter([{ path: "/caves/requests/:requestId", element: <CaveChangeRequestPage /> }],
    { initialEntries: ["/caves/requests/request0001"] });

  render(<RouterProvider router={router} />);

  expect(await screen.findByText("Change request unavailable")).toBeInTheDocument();
  expect(screen.getByText("The change request could not be loaded.")).toBeInTheDocument();
});

it("presents pending request context and requested changes together using reviewer vocabulary", async () => {
  renderRequestPage(pageDetail());

  const requestRegion = await screen.findByRole("region", { name: "Change request" });
  expect(within(requestRegion).getByText("Helms Deep Cave")).toBeInTheDocument();
  expect(within(requestRegion).getByText("Pending")).toBeInTheDocument();
  expect(within(requestRegion).getByText(/Submitted by Michael Test/)).toBeInTheDocument();
  expect(within(requestRegion).getByText(/Submitted .*2026 Aug-17/)).toBeInTheDocument();
  expect(within(requestRegion).getByText(/Updated .*2026 Aug-17/)).toBeInTheDocument();
  expect(within(requestRegion).getByRole("link", { name: "Open Cave" })).toHaveAttribute("href", "/caves/cave000001");
  expect(within(requestRegion).getByRole("button", { name: "Revise changes" })).toBeInTheDocument();
  expect(within(requestRegion).getByRole("heading", { name: "Requested changes" })).toBeInTheDocument();
  expect(requestRegion).not.toHaveTextContent("Base → proposed");
});

it("keeps reviewer and revise actions explicitly named at the mobile breakpoint", async () => {
  renderRequestPage(pageDetail());

  const requestRegion = await screen.findByRole("region", { name: "Change request" });
  const actions = screen.getByRole("region", { name: "Reviewer actions" });
  expect(within(requestRegion).getByRole("button", { name: "Revise changes" })).toBeInTheDocument();
  expect(within(actions).getByRole("button", { name: "Approve and publish" })).toBeInTheDocument();
  expect(within(actions).getByRole("button", { name: "Reject" })).toBeInTheDocument();
});

it("makes stale requests non-approvable and directs the reviewer to revise against the current Cave", async () => {
  renderRequestPage(pageDetail({ request: pageRequest({ isStale: true }) }));

  expect(await screen.findByRole("button", { name: "Revise against current Cave" })).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Approve and publish" })).toBeDisabled();
  expect(screen.getByRole("button", { name: "Reject" })).toBeEnabled();
  expect(screen.getByText("These requested changes need to be revised.")).toBeInTheDocument();
});

it("asks for confirmation before approval and submits the current proposal version only after confirmation", async () => {
  const pending = pageDetail();
  const approved = pageDetail({ request: pageRequest({ status: "Approved", canEdit: false, canReview: false,
    reviewerName: "Riley Reviewer", reviewedOn: "2026-08-17T18:00:00", reviewerNotes: "Looks correct" }) });
  const approve = jest.spyOn(CaveService, "ApproveChangeRequest").mockResolvedValue({
    result: "Approved", requestId: "request0001", publishedRevisionId: "revision0002", currentRevisionId: "revision0002",
  });
  renderRequestPage(pending, approved);

  fireEvent.click(await screen.findByRole("button", { name: "Approve and publish" }));
  const dialog = screen.getByRole("dialog", { name: "Approve and publish?" });
  expect(within(dialog).getByText(/publish the requested changes/i)).toBeInTheDocument();
  expect(approve).not.toHaveBeenCalled();

  fireEvent.change(within(dialog).getByRole("textbox", { name: "Reviewer note (optional)" }),
    { target: { value: "Looks correct" } });
  fireEvent.click(within(dialog).getByRole("button", { name: "Approve and publish" }));

  await waitFor(() => expect(approve).toHaveBeenCalledWith("request0001", "version0002", "Looks correct"));
  expect(await screen.findByText(/Approved by Riley Reviewer/)).toBeInTheDocument();
});

it.each(["", "   "])("blocks rejection when the reason is %p", async (reasonValue) => {
  const reject = jest.spyOn(CaveService, "RejectChangeRequest").mockResolvedValue({
    result: "Rejected", requestId: "request0001", currentRevisionId: "base000001",
  });
  renderRequestPage(pageDetail());

  fireEvent.click(await screen.findByRole("button", { name: "Reject" }));
  const dialog = screen.getByRole("dialog", { name: "Reject requested changes" });
  const reason = within(dialog).getByRole("textbox", { name: "Reason for rejection" });
  if (reasonValue) fireEvent.change(reason, { target: { value: reasonValue } });
  fireEvent.click(within(dialog).getByRole("button", { name: "Reject changes" }));

  expect(within(dialog).getByRole("alert")).toHaveTextContent("Reason for rejection is required.");
  expect(reject).not.toHaveBeenCalled();
});

it("trims and submits a valid rejection reason after confirmation", async () => {
  const rejected = pageDetail({ request: pageRequest({ status: "Rejected", canEdit: false, canReview: false,
    reviewerName: "Riley Reviewer", reviewedOn: "2026-08-17T18:00:00", reviewerNotes: "Not enough evidence" }) });
  const reject = jest.spyOn(CaveService, "RejectChangeRequest").mockResolvedValue({
    result: "Rejected", requestId: "request0001", currentRevisionId: "base000001",
  });
  renderRequestPage(pageDetail(), rejected);

  fireEvent.click(await screen.findByRole("button", { name: "Reject" }));
  const dialog = screen.getByRole("dialog", { name: "Reject requested changes" });
  fireEvent.change(within(dialog).getByRole("textbox", { name: "Reason for rejection" }),
    { target: { value: "  Not enough evidence  " } });
  fireEvent.click(within(dialog).getByRole("button", { name: "Reject changes" }));

  await waitFor(() => expect(reject).toHaveBeenCalledWith("request0001", "version0002", "Not enough evidence"));
  expect(await screen.findByText("Rejected by Riley Reviewer")).toBeInTheDocument();
});

it.each([
  ["Approved", "Approved by Riley Reviewer", "Reviewer note", "Looks correct"],
  ["Rejected", "Rejected by Riley Reviewer", "Reason for rejection", "Not enough evidence"],
] as const)("shows a concise %s history without active reviewer controls", async (status, summary, noteLabel, note) => {
  renderRequestPage(pageDetail({ request: pageRequest({ status, canEdit: false, canReview: false,
    reviewerName: "Riley Reviewer", reviewedOn: "2026-08-17T18:00:00", reviewerNotes: note }) }));

  expect(await screen.findByText(summary)).toBeInTheDocument();
  expect(screen.getByText(noteLabel)).toBeInTheDocument();
  expect(screen.getByText(note)).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Approve and publish" })).not.toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Reject" })).not.toBeInTheDocument();
});

it("uses friendly proposal version labels and keeps raw IDs out of the primary history UI", async () => {
  const getVersion = jest.spyOn(CaveService, "GetProposalVersion").mockResolvedValue(versionDetail(true, true));
  renderRequestPage(pageDetail());

  const history = await screen.findByRole("region", { name: "Proposal history" });
  expect(within(history).getByText(/Version 2.*Riley Reviewer.*2026 Aug-17 5:47 PM/)).toBeInTheDocument();
  expect(within(history).getByText("Current")).toBeInTheDocument();
  expect(history).not.toHaveTextContent("version0002");
  expect(history).not.toHaveTextContent("base000001");

  fireEvent.click(within(history).getByRole("button", { name: "View changes for Version 2" }));
  await waitFor(() => expect(getVersion).toHaveBeenCalledWith("request0001", "version0002"));
  expect(await within(history).findByRole("heading", { name: "Changes in this version" })).toBeInTheDocument();
  expect(history).not.toHaveTextContent("revision-one");
  expect(history).not.toHaveTextContent("revision-two");
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
    countyNumberIntent: "Manual", linePlots: [], unavailableStagedFileIds: [],
  };
};

it("labels the first proposal version using reviewer-facing language", () => {
  render(<ProposalVersionComparison detail={versionDetail(false)} />);

  expect(screen.getByRole("heading", { name: "Initial requested changes" })).toBeInTheDocument();
  expect(screen.queryByText("Published base → proposal")).not.toBeInTheDocument();
  expect(screen.queryByText("Changes from previous proposal version")).not.toBeInTheDocument();
});

it("shows a later version delta with removed tags and keeps the published comparison separate", () => {
  render(<ProposalVersionComparison detail={versionDetail(true)} />);

  expect(screen.getByRole("heading", { name: "Changes in this version" })).toBeInTheDocument();
  expect(screen.getByText("Biology")).toBeInTheDocument();
  expect(screen.getByLabelText("Removed Cricket")).toHaveTextContent("− Cricket");
  expect(screen.getByText("Compare this version with the published Cave")).toBeInTheDocument();
});

it("explains a changed published base without exposing revision IDs as primary UI", () => {
  render(<ProposalVersionComparison detail={versionDetail(true, true)} />);

  expect(screen.getByText("This version was revised against a newer published Cave.")).toBeInTheDocument();
  expect(screen.queryByText("revision-one")).not.toBeInTheDocument();
  expect(screen.queryByText("revision-two")).not.toBeInTheDocument();
});

it("renders the authoritative proposal County Number transition without a duplicate effective row", () => {
  const detail = versionDetail(true);
  detail.diffFromPreviousVersion!.scalars.push({ path: "CountyNumber", previous: 1, current: 123 });
  detail.countyNumberIntent = "Manual";
  detail.requestedCountyNumber = 123;
  detail.countyNumberChange = {
    previous: { mode: "FirstAvailable" },
    current: { mode: "Manual", number: 123 },
  };

  render(<ProposalVersionComparison detail={detail} />);

  const countyNumberChange = screen.getByRole("group", { name: "County Number change" });
  expect(countyNumberChange).toHaveTextContent("− First available on approval");
  expect(countyNumberChange).toHaveTextContent("+ 123");
  expect(countyNumberChange).not.toHaveTextContent("Before");
  expect(countyNumberChange).not.toHaveTextContent("After");
  expect(screen.getAllByText("County Number")).toHaveLength(1);
});

it("renders a rebased proposal as preserving its County Number", () => {
  const detail = versionDetail(true);
  detail.diffFromPreviousVersion!.scalars.push({ path: "CountyNumber", previous: 0, current: 47 });
  detail.countyNumberIntent = "AutomaticNext";
  detail.countyNumberChange = {
    previous: { mode: "FirstAvailable" },
    current: { mode: "PreserveExisting", number: 47 },
  };

  render(<ProposalVersionComparison detail={detail} />);

  const countyNumberChange = screen.getByRole("group", { name: "County Number change" });
  expect(countyNumberChange).toHaveTextContent("− First available on approval");
  expect(countyNumberChange).toHaveTextContent("+ 47");
  expect(countyNumberChange).not.toHaveTextContent("Before");
  expect(countyNumberChange).not.toHaveTextContent("After");
  expect(screen.queryByText("Auto-assigned on approval")).not.toBeInTheDocument();
  expect(screen.getAllByText("County Number")).toHaveLength(1);
});
