import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { useContext } from "react";
import type { ReactElement, ReactNode } from "react";
import { createMemoryRouter, MemoryRouter, RouterProvider } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { CaveAvailability, CaveChangeRequestPage, ProposalVersionComparison,
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

jest.mock("../../../Shared/Components/Display/PlanarianTag", () => ({
  PlanarianTag: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
}));

jest.mock("../../../Shared/Components/Buttons/PlanarianModal", () => ({
  PlanarianModal: ({ open, header, footer, children }: MockPlanarianModalProps) => open ? (
    <div role="dialog" aria-label={typeof header === "string" ? header : undefined}>
      {children}
      {footer}
    </div>
  ) : null,
}));

jest.mock("../../Files/Components/FileViewerComponent", () => ({
  FileViewer: ({ open, name }: { open: boolean; name?: string }) =>
    open ? <div role="dialog" aria-label={`File viewer ${name}`} /> : null,
}));

jest.mock("../Components/CaveLinePlotViewer", () => ({
  CaveLinePlotViewer: ({ open, linePlot }: { open: boolean; linePlot?: { name: string } }) =>
    open ? <div role="dialog" aria-label={`Line plot viewer ${linePlot?.name}`} /> : null,
}));

let setHeaderTitleSpy = jest.fn();
let setHeaderButtonsSpy = jest.fn();

const AppHeaderHarness = ({ children }: { children: ReactNode }) => {
  const baseContext = useContext(AppContext);
  return <AppContext.Provider value={{
    ...baseContext, setHeaderTitle: setHeaderTitleSpy, setHeaderButtons: setHeaderButtonsSpy,
  }}>{children}</AppContext.Provider>;
};

const renderWithHeader = (node: ReactNode) => render(<AppHeaderHarness>{node}</AppHeaderHarness>);
const latestHeaderButtons = () => {
  const calls = setHeaderButtonsSpy.mock.calls;
  return (calls[calls.length - 1]?.[0] ?? []) as ReactElement[];
};
const headerButton = (ariaLabel: string) => latestHeaderButtons()
  .find(button => button.props?.["aria-label"] === ariaLabel);

beforeEach(() => {
  setHeaderTitleSpy = jest.fn();
  setHeaderButtonsSpy = jest.fn();
});

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
  changeSummary: { caveFields: 0, entrances: { added: 0, removed: 0, changed: 0, total: 0 }, files: { added: 0, removed: 0, changed: 0, total: 0 }, linePlots: { added: 0, removed: 0, changed: 0, total: 0 }, otherChanges: 0 },
});

it("does not render an Open Cave link when the historical Cave is unavailable", () => {
  render(<MemoryRouter><CaveAvailability request={request(false)} /></MemoryRouter>);

  expect(screen.getByText("Cave no longer available")).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Open Cave" })).not.toBeInTheDocument();
});

it("renders the live Cave as a button while the Cave is available", () => {
  render(<MemoryRouter><CaveAvailability request={request(true)} /></MemoryRouter>);

  expect(screen.getByRole("button", { name: "Open Cave" })).toBeInTheDocument();
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
  canEdit: true, canReview: true,
  changeSummary: { caveFields: 0, entrances: { added: 0, removed: 0, changed: 0, total: 0 }, files: { added: 0, removed: 0, changed: 0, total: 0 }, linePlots: { added: 0, removed: 0, changed: 0, total: 0 }, otherChanges: 0 },
  ...overrides,
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
    countyNumberIntent: "Manual", linePlots: [], activeStagedFiles: [], unavailableStagedFileIds: [], ...overrides,
  };
};

const renderRequestPage = (detail: CaveChangeRequestDetailVm,
  refreshed: CaveChangeRequestDetailVm = detail) => {
  const getRequest = jest.spyOn(CaveService, "GetChangeRequest")
    .mockResolvedValueOnce(detail).mockResolvedValue(refreshed);
  const router = createMemoryRouter([{ path: "/caves/requests/:requestId", element: <CaveChangeRequestPage /> }],
    { initialEntries: ["/caves/requests/request0001"] });
  renderWithHeader(<RouterProvider router={router} />);
  return { router, getRequest };
};

afterEach(() => jest.restoreAllMocks());

it("shows an intentional loading state while the request is being fetched", () => {
  jest.spyOn(CaveService, "GetChangeRequest").mockImplementation(() => new Promise(() => undefined));
  const router = createMemoryRouter([{ path: "/caves/requests/:requestId", element: <CaveChangeRequestPage /> }],
    { initialEntries: ["/caves/requests/request0001"] });

  renderWithHeader(<RouterProvider router={router} />);

  expect(screen.getByRole("status", { name: "Loading change request" })).toHaveTextContent("Loading change request…");
});

it("shows an explicit unavailable state when the request cannot be loaded", async () => {
  jest.spyOn(CaveService, "GetChangeRequest").mockRejectedValue(new Error("load failed"));
  const router = createMemoryRouter([{ path: "/caves/requests/:requestId", element: <CaveChangeRequestPage /> }],
    { initialEntries: ["/caves/requests/request0001"] });

  renderWithHeader(<RouterProvider router={router} />);

  expect(await screen.findByText("Change request unavailable")).toBeInTheDocument();
  expect(screen.getByText("The change request could not be loaded.")).toBeInTheDocument();
});

it("keeps the request header compact without repeating the changes title", async () => {
  renderRequestPage(pageDetail({ request: pageRequest({
    changeSummary: { caveFields: 6, entrances: { added: 0, removed: 0, changed: 0, total: 0 },
      files: { added: 1, removed: 0, changed: 0, total: 1 },
      linePlots: { added: 2, removed: 0, changed: 0, total: 2 }, otherChanges: 0 },
  }) }));

  const requestRegion = await screen.findByRole("region", { name: "Change request" });
  await waitFor(() => expect(setHeaderTitleSpy).toHaveBeenLastCalledWith(["Review Helms Deep Cave"]));
  expect(latestHeaderButtons().some(button => button.type === CaveAvailability)).toBe(true);
  expect(headerButton("Revise changes")).toBeDefined();
  expect(within(requestRegion).queryByText("Review Helms Deep Cave")).not.toBeInTheDocument();
  expect(within(requestRegion).queryByText("Pending review")).not.toBeInTheDocument();
  expect(within(requestRegion).getByText("6 cave fields · 1 file · 2 line plots")).toBeInTheDocument();
  expect(requestRegion).toHaveTextContent("Michael Test · submitted 2026 Aug-17");
  expect(requestRegion).toHaveTextContent("updated 2026 Aug-17");
  expect(within(requestRegion).queryByRole("heading", { name: "Requested changes" })).not.toBeInTheDocument();
  expect(requestRegion).not.toHaveTextContent("Submitted by");
  expect(requestRegion).not.toHaveTextContent("Base → proposed");
});

it("falls back to the existing county-number endpoint when an older detail response omits the likely number", async () => {
  const base = snapshot("Helms Deep Cave");
  const proposed = { ...base, county: { id: "new-county", nameAtRevision: "Millard", displayIdAtRevision: "M" }, countyNumber: 0 };
  const nextCountyNumber = jest.spyOn(CaveService, "GetNextCountyNumber").mockResolvedValue(48);
  renderRequestPage(pageDetail({
    base, current: base, proposed, countyNumberIntent: "AutomaticNext", likelyCountyNumber: undefined,
    diff: { ...emptyDiff(), scalars: [
      { path: "County.Id", previous: base.county.id, current: proposed.county.id },
      { path: "CountyNumber", previous: base.countyNumber, current: 0 },
    ] },
  }));

  await waitFor(() => expect(nextCountyNumber).toHaveBeenCalledWith("new-county", false));
  await waitFor(() => expect(document.body).toHaveTextContent("Likely 48 · assigned on approval"));
});

it("does not crash when an older detail response omits proposal line plots", async () => {
  const detail = pageDetail();
  delete (detail as Partial<CaveChangeRequestDetailVm>).linePlots;

  renderRequestPage(detail);

  expect(await screen.findByRole("region", { name: "Change request" })).toBeInTheDocument();
  await waitFor(() => expect(setHeaderTitleSpy).toHaveBeenLastCalledWith(["Review Helms Deep Cave"]));
});

it("keeps staged file and line-plot actions with the assets instead of a redundant attachment section", async () => {
  const liveFile = { id: "livefile01", fileTypeTagId: "map", fileTypeNameAtRevision: "Map",
    name: "Entrance map", extension: ".pdf" };
  const base = snapshot("Helms Deep Cave");
  const proposed = { ...base, files: [liveFile], linePlots: [{ id: "lineplot01", name: "Main survey", contentHash: "hash" }] };
  const detail = pageDetail({
    base, current: base, proposed, activeStagedFiles: [liveFile],
    linePlots: [{ id: "lineplot01", name: "Main survey", geoJson: '{"type":"FeatureCollection","features":[]}' }],
    diff: { ...emptyDiff(), addedFiles: [liveFile.id], addedLinePlots: ["lineplot01"] },
  });

  renderRequestPage(detail);

  const fileRow = await screen.findByRole("group", { name: "Entrance map file" });
  expect(within(fileRow).getByText("View")).toBeInTheDocument();
  expect(within(fileRow).getByText("Download")).toBeInTheDocument();
  const linePlotRow = screen.getByRole("group", { name: "Main survey line plot" });
  expect(within(linePlotRow).getByText("View")).toBeInTheDocument();
  expect(within(linePlotRow).getByText("Download")).toBeInTheDocument();
  expect(screen.queryByText("Files included in this request")).not.toBeInTheDocument();
  expect(screen.queryByText("These files will be published if the request is approved.")).not.toBeInTheDocument();

  fireEvent.click(within(fileRow).getByText("View"));
  expect(screen.getByRole("dialog", { name: "File viewer Entrance map.pdf" })).toBeInTheDocument();
  fireEvent.click(within(linePlotRow).getByText("View"));
  expect(screen.getByRole("dialog", { name: "Line plot viewer Main survey" })).toBeInTheDocument();
});

it("keeps reviewer and revise actions explicitly named at the mobile breakpoint", async () => {
  renderRequestPage(pageDetail());

  const requestRegion = await screen.findByRole("region", { name: "Change request" });
  const actions = within(requestRegion).getByRole("region", { name: "Reviewer actions" });
  await waitFor(() => expect(headerButton("Revise changes")).toBeDefined());
  expect(within(actions).getByRole("button", { name: "Approve and publish" })).toBeInTheDocument();
  expect(within(actions).getByRole("button", { name: "Reject" })).toBeInTheDocument();
});

it("makes stale requests non-approvable and directs the reviewer to revise against the current Cave", async () => {
  renderRequestPage(pageDetail({ request: pageRequest({ isStale: true }) }));

  await waitFor(() => expect(headerButton("Revise against current Cave")).toBeDefined());
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
  expect(await screen.findByText("Approved")).toBeInTheDocument();
  expect(screen.getByText(/Riley Reviewer · reviewed/)).toBeInTheDocument();
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
  expect(await screen.findByText("Rejected")).toBeInTheDocument();
  expect(screen.getByText(/Riley Reviewer · reviewed/)).toBeInTheDocument();
});

it.each([
  ["Approved", "Reviewer note", "Looks correct"],
  ["Rejected", "Reason for rejection", "Not enough evidence"],
] as const)("shows a concise %s history without active reviewer controls", async (status, noteLabel, note) => {
  renderRequestPage(pageDetail({ request: pageRequest({ status, canEdit: false, canReview: false,
    reviewerName: "Riley Reviewer", reviewedOn: "2026-08-17T18:00:00", reviewerNotes: note }) }));

  expect(await screen.findByText(status)).toBeInTheDocument();
  expect(screen.getByText(/Riley Reviewer · reviewed/)).toBeInTheDocument();
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

it("lets reviewers open published files and line plots that the request removes", async () => {
  const base = snapshot("Helms Deep Cave");
  base.files = [{ id: "removed-file", fileTypeTagId: "type", fileTypeNameAtRevision: "Map",
    name: "Old map", extension: ".pdf" }];
  base.linePlots = [{ id: "removed-line", name: "Old survey", contentHash: "hash" }];
  const proposed = snapshot("Helms Deep Cave");
  renderRequestPage(pageDetail({
    base, current: base, proposed,
    diff: { ...emptyDiff(), removedFiles: ["removed-file"], removedLinePlots: ["removed-line"] },
    removedLinePlots: [{ id: "removed-line", name: "Old survey",
      geoJson: '{"type":"FeatureCollection","features":[]}' }],
  }));

  const fileRow = await screen.findByRole("group", { name: "Old map file" });
  expect(fileRow).toHaveTextContent("Removed");
  fireEvent.click(within(fileRow).getByText("View"));
  expect(screen.getByRole("dialog", { name: "File viewer Old map.pdf" })).toBeInTheDocument();

  const linePlotRow = screen.getByRole("group", { name: "Old survey line plot" });
  expect(linePlotRow).toHaveTextContent("Removed");
  fireEvent.click(within(linePlotRow).getByText("View"));
  expect(screen.getByRole("dialog", { name: "Line plot viewer Old survey" })).toBeInTheDocument();
});