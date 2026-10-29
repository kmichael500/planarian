import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { CaveService } from "../Service/CaveService";
import { AppService } from "../../../Shared/Services/AppService";
import { ReviseCaveChangeRequestPage } from "./ReviseCaveChangeRequestPage";

jest.mock("../Components/AddCaveComponent", () => {
  const { Form, Input } = require("antd");
  return {
    AddCaveComponent: ({ hideCountyNumberControls }: any) => (
      <>
        <Form.Item name="name" label="Name"><Input /></Form.Item>
        <Form.Item name="stateId" noStyle><Input type="hidden" /></Form.Item>
        <Form.Item name="countyId" noStyle><Input type="hidden" /></Form.Item>
        <Form.Item name="countyNumber" noStyle><Input type="hidden" /></Form.Item>
        <Form.Item name="isCountyNumberManuallySet" noStyle><Input type="hidden" /></Form.Item>
        <Form.Item name="useFirstAvailableCountyNumber" noStyle><Input type="hidden" /></Form.Item>
        <div data-testid="county-number-controls-hidden">{String(!!hideCountyNumberControls)}</div>
        <button type="submit">Review changes</button>
      </>
    ),
  };
});

jest.mock("../Components/CaveRevisionDiff", () => ({
  CaveRevisionDiff: () => <div data-testid="revision-diff" />,
}));

jest.mock("../../Files/Components/FileViewerComponent", () => ({ FileViewer: () => null }));
jest.mock("../Components/CaveLinePlotViewer", () => ({ CaveLinePlotViewer: () => null }));

jest.mock("../../../Shared/Components/Buttons/PlanarianButtton", () => ({
  PlanarianButton: ({ children, loading: _loading, icon: _icon, danger: _danger,
    alwaysShowChildren: _alwaysShowChildren, ...props }: any) => <button {...props}>{children}</button>,
}));

jest.mock("../../../Shared/Components/Buttons/PlanarianModal", () => ({
  PlanarianModal: ({ open, header, footer, children }: any) =>
    open ? <div role="dialog" aria-label={header}>{children}{footer}</div> : null,
}));

jest.mock("../Helpers/CaveFormMapper", () => ({
  caveToForm: (cave: any) => ({
    id: cave?.id ?? "cave000001", expectedRevisionId: "revision01", name: cave?.name ?? "Proposed name",
    depthFeet: null, biologyTagIds: ["biology-hidden"],
    files: [{ id: "file-hidden", name: "Existing map", extension: ".pdf", fileTypeTagId: "file-type", fileTypeKey: "Map" }],
    linePlots: [{ id: "line-hidden", name: "Existing traverse", geoJson: "{\"type\":\"FeatureCollection\",\"features\":[]}" }],
  }),
  snapshotToForm: (snapshot: any, countyNumberIntent: string, requestedCountyNumber?: number) => ({
    id: snapshot?.caveId ?? "cave000001",
    name: snapshot?.name ?? "Proposed name",
    stateId: snapshot?.state?.id,
    countyId: snapshot?.county?.id,
    countyNumber: countyNumberIntent === "Manual" ? requestedCountyNumber : snapshot?.countyNumber,
    isCountyNumberManuallySet: countyNumberIntent === "Manual",
    useFirstAvailableCountyNumber: countyNumberIntent === "FirstAvailable",
    depthFeet: null,
    biologyTagIds: ["biology-hidden"],
    files: [{ id: "file-hidden", name: "Existing map", extension: ".pdf", fileTypeTagId: "file-type", fileTypeKey: "Map" }],
    linePlots: [{ id: "line-hidden", name: "Existing traverse", geoJson: "{\"type\":\"FeatureCollection\",\"features\":[]}" }],
  }),
}));

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({
      matches: false,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }),
  });
});

afterEach(() => jest.restoreAllMocks());

const mockPendingRequest = () => {
  jest.spyOn(CaveService, "GetChangeRequest").mockResolvedValue({
    request: {
      id: "request0001", caveId: "cave000001", caveName: "Cave", caveExists: true,
      isCreation: false,
      status: "Pending", submitterUserId: "contributor", submittedOn: "2026-08-16T00:00:00Z",
      originalBaseRevisionId: "revision01", proposalBaseRevisionId: "revision01",
      currentRevisionId: "revision01", currentProposalVersionId: "proposal01",
      isStale: false, canEdit: true, canReview: false,
      changeSummary: { caveFields: 0, entrances: { added: 0, removed: 0, changed: 0, total: 0 }, files: { added: 0, removed: 0, changed: 0, total: 0 }, linePlots: { added: 0, removed: 0, changed: 0, total: 0 }, otherChanges: 0 },
    },
    base: {} as any, proposed: {} as any, current: {} as any, diff: {} as any,
    versions: [], countyNumberIntent: "Manual", linePlots: [], activeStagedFiles: [], unavailableStagedFileIds: [],
  });
  jest.spyOn(CaveService, "GetProposalVersion").mockResolvedValue({
    base: {} as any, proposed: {} as any, diffFromBase: {} as any,
    baseRevisionChanged: false, baseRevisionId: "revision01", countyNumberIntent: "Manual",
    linePlots: [], unavailableStagedFileIds: [],
  });
};

const renderPage = () => {
  const router = createMemoryRouter([
    { path: "/caves/requests/:requestId/revise", element: <ReviseCaveChangeRequestPage /> },
    { path: "/other", element: <div>Other page</div> },
    { path: "/caves/requests/:requestId", element: <div>Request detail</div> },
  ], { initialEntries: ["/caves/requests/request0001/revise"] });
  render(<RouterProvider router={router} />);
  return router;
};

it("keeps stale comparisons contextual and loads the latest Cave as the re-review baseline", async () => {
  const base = { caveId: "cave000001", accountId: "account", name: "Base Cave", alternateNames: [],
    state: { id: "tn", nameAtRevision: "Tennessee" }, county: { id: "county", nameAtRevision: "Coffee" },
    countyNumber: 1, isArchived: false, tags: [], entrances: [], files: [], linePlots: [] } as any;
  const emptyDiff = { scalars: [], addedTags: [], removedTags: [], addedEntrances: [], removedEntrances: [],
    changedEntrances: [], entranceChanges: [], addedFiles: [], removedFiles: [], changedFiles: [], fileChanges: [],
    addedLinePlots: [], removedLinePlots: [], changedLinePlots: [], linePlotChanges: [], referenceMetadataChanges: [] };
  jest.spyOn(CaveService, "GetChangeRequest").mockResolvedValue({
    request: { id: "request0001", caveId: "cave000001", caveName: "Cave", caveExists: true, isCreation: false, status: "Pending",
      submitterUserId: "contributor", submittedOn: "2026-08-16T00:00:00Z", originalBaseRevisionId: "revision01",
      proposalBaseRevisionId: "revision01", currentRevisionId: "revision02", currentProposalVersionId: "proposal01",
      isStale: true, canEdit: true, canReview: false,
      changeSummary: { caveFields: 1, entrances: { added: 0, removed: 0, changed: 0, total: 0 },
        files: { added: 0, removed: 0, changed: 0, total: 0 }, linePlots: { added: 0, removed: 0, changed: 0, total: 0 }, otherChanges: 0 } },
    base, proposed: { ...base, name: "Earlier proposal" }, current: { ...base, lengthFeet: 120 },
    diff: { ...emptyDiff, scalars: [{ path: "Name", previous: "Base Cave", current: "Earlier proposal" }] },
    publishedSinceBase: { ...emptyDiff, scalars: [{ path: "LengthFeet", previous: 100, current: 120 }] },
    versions: [], countyNumberIntent: "Manual", linePlots: [], activeStagedFiles: [], unavailableStagedFileIds: [],
  });
  const context = jest.spyOn(CaveService, "GetProposalAuthoringContext").mockResolvedValue({
    cave: { id: "cave000001", name: "Current Cave" } as any, expectedBaseRevisionId: "revision02", linePlots: [],
  });
  const version = jest.spyOn(CaveService, "GetProposalVersion");
  const preview = jest.spyOn(CaveService, "PreviewRevisedChanges").mockResolvedValue({
    base: { ...base, lengthFeet: 120 }, proposed: { ...base, name: "Proposed name", lengthFeet: 120 },
    diff: { ...emptyDiff, scalars: [{ path: "Name", previous: "Base Cave", current: "Proposed name" }] },
    countyNumberIntent: "Manual", linePlots: [], hasMeaningfulChanges: true,
  });

  renderPage();
  await screen.findByRole("textbox", { name: "Name" });
  expect(context).toHaveBeenCalledWith("cave000001");
  expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue("Current Cave");
  expect(version).not.toHaveBeenCalled();
  expect(screen.getByText("This cave changed after these edits were submitted.")).toBeInTheDocument();
  expect(screen.getByText("No parts of this submission were changed again")).toBeInTheDocument();
  expect(screen.getByRole("button", { name: /Original submission/ })).toBeInTheDocument();
  expect(screen.getByRole("button", { name: /Changes made after submission/ })).toBeInTheDocument();
  expect(screen.queryByText("Original proposal changes")).not.toBeInTheDocument();
  expect(screen.queryByText("Published changes since that proposal")).not.toBeInTheDocument();

  fireEvent.click(screen.getByRole("button", { name: "Review changes" }));
  await waitFor(() => expect(preview).toHaveBeenCalledWith(
    "request0001", expect.objectContaining({ name: "Current Cave" }), true, "revision02", "proposal01"));
});

it("realigns stale guidance and editor state when the Cave changes while the page is loading", async () => {
  const base = { caveId: "cave000001", accountId: "account", name: "Base Cave", alternateNames: [],
    state: { id: "tn", nameAtRevision: "Tennessee" }, county: { id: "county", nameAtRevision: "Coffee" },
    countyNumber: 1, isArchived: false, tags: [], entrances: [], files: [], linePlots: [] } as any;
  const emptyDiff = { scalars: [], addedTags: [], removedTags: [], addedEntrances: [], removedEntrances: [],
    changedEntrances: [], entranceChanges: [], addedFiles: [], removedFiles: [], changedFiles: [], fileChanges: [],
    addedLinePlots: [], removedLinePlots: [], changedLinePlots: [], linePlotChanges: [], referenceMetadataChanges: [] };
  const request = { id: "request0001", caveId: "cave000001", caveName: "Cave", caveExists: true, isCreation: false, status: "Pending" as const,
    submitterUserId: "contributor", submittedOn: "2026-08-16T00:00:00Z", originalBaseRevisionId: "revision01",
    proposalBaseRevisionId: "revision01", currentProposalVersionId: "proposal01", isStale: true, canEdit: true, canReview: false,
    changeSummary: { caveFields: 1, entrances: { added: 0, removed: 0, changed: 0, total: 0 },
      files: { added: 0, removed: 0, changed: 0, total: 0 }, linePlots: { added: 0, removed: 0, changed: 0, total: 0 }, otherChanges: 0 } };
  const initial = { request: { ...request, currentRevisionId: "revision02" }, base,
    proposed: { ...base, name: "Earlier proposal" }, current: { ...base, lengthFeet: 120 },
    diff: { ...emptyDiff, scalars: [{ path: "Name", previous: "Base Cave", current: "Earlier proposal" }] },
    publishedSinceBase: { ...emptyDiff, scalars: [{ path: "LengthFeet", previous: 100, current: 120 }] },
    versions: [], countyNumberIntent: "Manual" as const, linePlots: [], activeStagedFiles: [], unavailableStagedFileIds: [] };
  const refreshed = { ...initial, request: { ...request, currentRevisionId: "revision03" },
    current: { ...base, name: "Current Cave v3" },
    publishedSinceBase: { ...emptyDiff, scalars: [{ path: "Name", previous: "Base Cave", current: "Current Cave v3" }] } };
  jest.spyOn(CaveService, "GetChangeRequest")
    .mockResolvedValueOnce(initial).mockResolvedValueOnce(refreshed);
  const context = jest.spyOn(CaveService, "GetProposalAuthoringContext").mockResolvedValue({
    cave: { id: "cave000001", name: "Current Cave v3" } as any,
    expectedBaseRevisionId: "revision03", linePlots: [],
  });
  const preview = jest.spyOn(CaveService, "PreviewRevisedChanges").mockResolvedValue({
    base, proposed: { ...base, name: "Current Cave v3" }, diff: emptyDiff,
    countyNumberIntent: "Manual", linePlots: [], hasMeaningfulChanges: true,
  });

  renderPage();
  const name = await screen.findByRole("textbox", { name: "Name" });

  expect(name).toHaveValue("Current Cave v3");
  expect(context).toHaveBeenCalledTimes(1);
  expect(screen.getByText("1 part of this submission was changed again later")).toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "Review changes" }));
  await waitFor(() => expect(preview).toHaveBeenCalledWith(
    "request0001", expect.objectContaining({ name: "Current Cave v3" }), true, "revision03", "proposal01"));
});

it("prevents saving a proposal version when the authoritative preview is semantically unchanged", async () => {
  mockPendingRequest();
  jest.spyOn(CaveService, "PreviewRevisedChanges").mockResolvedValue({
    base: {} as any, proposed: {} as any, diff: {} as any,
    countyNumberIntent: "Manual", linePlots: [], hasMeaningfulChanges: false,
  });
  const revise = jest.spyOn(CaveService, "ReviseChanges").mockResolvedValue("proposal02");
  renderPage();

  fireEvent.click(await screen.findByRole("button", { name: "Review changes" }));

  expect(await screen.findByText(/would not create a meaningful new proposal version/i)).toBeInTheDocument();
  const save = screen.getByRole("button", { name: "Save proposal version" });
  expect(save).toBeDisabled();
  fireEvent.click(save);
  await waitFor(() => expect(revise).not.toHaveBeenCalled());
});

it("blocks navigation after revising a field and preserves the edit when the author keeps editing", async () => {
  mockPendingRequest();
  const router = renderPage();
  const name = await screen.findByRole("textbox", { name: "Name" });
  await waitFor(() => expect(document.querySelector(".ant-spin-spinning")).toBeNull());
  userEvent.clear(name);
  userEvent.type(name, "Unsaved revised name");

  await act(async () => { await router.navigate("/other"); });

  expect(await screen.findByRole("dialog", { name: "Discard unsaved changes?" })).toBeInTheDocument();
  userEvent.click(screen.getByRole("button", { name: "Keep editing" }));
  expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue("Unsaved revised name");
});

it("clears the dirty guard before navigating after a successful revision save", async () => {
  mockPendingRequest();
  jest.spyOn(CaveService, "PreviewRevisedChanges").mockResolvedValue({
    base: {} as any, proposed: {} as any, diff: {} as any,
    countyNumberIntent: "Manual", linePlots: [], hasMeaningfulChanges: true,
  });
  const revise = jest.spyOn(CaveService, "ReviseChanges").mockResolvedValue("proposal02");
  renderPage();

  const name = await screen.findByRole("textbox", { name: "Name" });
  await waitFor(() => expect(document.querySelector(".ant-spin-spinning")).toBeNull());
  userEvent.clear(name);
  userEvent.type(name, "Saved revised name");
  fireEvent.click(screen.getByRole("button", { name: "Review changes" }));
  fireEvent.click(await screen.findByRole("button", { name: "Save proposal version" }));

  expect(await screen.findByText("Request detail")).toBeInTheDocument();
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  expect(revise).toHaveBeenCalledTimes(1);
});

it("saves reviewer-requested edits as a draft without implicitly resubmitting", async () => {
  jest.spyOn(CaveService, "GetChangeRequest").mockResolvedValue({
    request: { id: "request0001", caveId: "cave000001", caveName: "Cave", caveExists: true,
      isCreation: false,
      status: "ChangesRequested", submitterUserId: "contributor", submittedOn: "2026-08-16T00:00:00Z",
      originalBaseRevisionId: "revision01", proposalBaseRevisionId: "revision01", currentRevisionId: "revision01",
      currentProposalVersionId: "proposal01", isStale: false, canEdit: true, canReview: false,
      changeSummary: { caveFields: 1, entrances: { added: 0, removed: 0, changed: 0, total: 0 }, files: { added: 0, removed: 0, changed: 0, total: 0 }, linePlots: { added: 0, removed: 0, changed: 0, total: 0 }, otherChanges: 0 } },
    base: {} as any, proposed: {} as any, current: {} as any, diff: {} as any, versions: [], countyNumberIntent: "Manual",
    linePlots: [], activeStagedFiles: [], unavailableStagedFileIds: [], reviewEvents: [{ id: "event01",
      proposalVersionId: "proposal01", action: "ChangesRequested", actorUserId: "reviewer",
      actorName: "Riley Reviewer", message: "Clarify the entrance description.", createdOn: "2026-08-17T18:00:00Z" }],
  });
  jest.spyOn(CaveService, "GetProposalVersion").mockResolvedValue({ base: {} as any, proposed: {} as any,
    diffFromBase: {} as any, baseRevisionChanged: false, baseRevisionId: "revision01", countyNumberIntent: "Manual",
    linePlots: [], unavailableStagedFileIds: [] });
  jest.spyOn(CaveService, "PreviewRevisedChanges").mockResolvedValue({ base: {} as any, proposed: {} as any,
    diff: {} as any, countyNumberIntent: "Manual", linePlots: [], hasMeaningfulChanges: true });
  const revise = jest.spyOn(CaveService, "ReviseChanges").mockResolvedValue("proposal02");
  const resubmit = jest.spyOn(CaveService, "ResubmitChangeRequest").mockResolvedValue({ result: "Resubmitted", requestId: "request0001" });
  renderPage();

  expect(await screen.findByText("Reviewer requested changes")).toBeInTheDocument();
  expect(screen.getByText("Clarify the entrance description.")).toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "Review changes" }));
  fireEvent.click(await screen.findByRole("button", { name: "Save draft version" }));
  await waitFor(() => expect(revise).toHaveBeenCalledTimes(1));
  expect(resubmit).not.toHaveBeenCalled();
});


it("previews and saves a revision with the same complete draft including unmounted values", async () => {
  mockPendingRequest();
  const preview = jest.spyOn(CaveService, "PreviewRevisedChanges").mockResolvedValue({
    base: {} as any, proposed: {} as any, diff: {} as any,
    countyNumberIntent: "Manual", linePlots: [], hasMeaningfulChanges: true,
  });
  const revise = jest.spyOn(CaveService, "ReviseChanges").mockResolvedValue("proposal02");
  renderPage();

  fireEvent.click(await screen.findByRole("button", { name: "Review changes" }));

  await waitFor(() => expect(preview).toHaveBeenCalledTimes(1));
  const expectedDraft = expect.objectContaining({
    id: "cave000001",
    name: "Proposed name",
    depthFeet: null,
    biologyTagIds: ["biology-hidden"],
    files: [expect.objectContaining({ id: "file-hidden", name: "Existing map" })],
    linePlots: [expect.objectContaining({ id: "line-hidden", name: "Existing traverse" })],
  });
  expect(preview).toHaveBeenCalledWith(
    "request0001", expectedDraft, false, "revision01", "proposal01"
  );

  fireEvent.click(await screen.findByRole("button", { name: "Save proposal version" }));

  await waitFor(() => expect(revise).toHaveBeenCalledTimes(1));
  expect(revise).toHaveBeenCalledWith(
    "request0001", expectedDraft, false, "revision01", "proposal01"
  );
});

it("sanitizes Manager-only county-number state when a View author revises a new-Cave proposal", async () => {
  const snapshot = {
    caveId: "cave000001", accountId: "account", name: "Proposed Cave", alternateNames: [],
    state: { id: "state", nameAtRevision: "Tennessee" },
    county: { id: "county", nameAtRevision: "Coffee" }, countyNumber: 0,
    isArchived: false, tags: [], entrances: [], files: [], linePlots: [],
  } as any;
  jest.spyOn(CaveService, "GetChangeRequest").mockResolvedValue({
    request: {
      id: "request0001", caveId: "cave000001", caveName: "Proposed Cave",
      caveExists: false, isCreation: true, status: "ChangesRequested",
      submitterUserId: "contributor", submittedOn: "2026-08-16T00:00:00Z",
      currentProposalVersionId: "proposal01", isStale: false, canEdit: true, canReview: false,
      changeSummary: { caveFields: 1, entrances: { added: 0, removed: 0, changed: 0, total: 0 },
        files: { added: 0, removed: 0, changed: 0, total: 0 },
        linePlots: { added: 0, removed: 0, changed: 0, total: 0 }, otherChanges: 0 },
    },
    base: { ...snapshot, name: "" }, proposed: snapshot, current: { ...snapshot, name: "" },
    diff: {} as any, versions: [], countyNumberIntent: "AutomaticNext",
    linePlots: [], activeStagedFiles: [], unavailableStagedFileIds: [],
  });
  jest.spyOn(CaveService, "GetProposalVersion").mockResolvedValue({
    base: { ...snapshot, name: "" }, proposed: snapshot, diffFromBase: {} as any,
    baseRevisionChanged: false, baseRevisionId: undefined, countyNumberIntent: "FirstAvailable",
    linePlots: [], unavailableStagedFileIds: [],
  });
  const canManage = jest.spyOn(AppService, "HasCavePermission").mockResolvedValue(false);
  const preview = jest.spyOn(CaveService, "PreviewRevisedChanges").mockResolvedValue({
    base: { ...snapshot, name: "" }, proposed: snapshot, diff: {} as any,
    countyNumberIntent: "AutomaticNext", linePlots: [], hasMeaningfulChanges: true,
  });

  renderPage();
  await screen.findByRole("textbox", { name: "Name" });
  await waitFor(() => expect(canManage).toHaveBeenCalledWith("Manager", null, "county"));
  expect(screen.getByTestId("county-number-controls-hidden")).toHaveTextContent("true");
  fireEvent.click(screen.getByRole("button", { name: "Review changes" }));

  await waitFor(() => expect(preview).toHaveBeenCalledWith(
    "request0001",
    expect.objectContaining({
      id: "cave000001",
      countyNumber: null,
      isCountyNumberManuallySet: false,
      useFirstAvailableCountyNumber: false,
    }),
    false, undefined, "proposal01"));
});

it("preserves first-available numbering when the creation author still manages the destination", async () => {
  const snapshot = {
    caveId: "cave000001", accountId: "account", name: "Proposed Cave", alternateNames: [],
    state: { id: "state", nameAtRevision: "Tennessee" },
    county: { id: "county", nameAtRevision: "Coffee" }, countyNumber: 0,
    isArchived: false, tags: [], entrances: [], files: [], linePlots: [],
  } as any;
  jest.spyOn(CaveService, "GetChangeRequest").mockResolvedValue({
    request: {
      id: "request0001", caveId: "cave000001", caveName: "Proposed Cave",
      caveExists: false, isCreation: true, status: "Pending",
      submitterUserId: "manager", submittedOn: "2026-08-16T00:00:00Z",
      currentProposalVersionId: "proposal01", isStale: false, canEdit: true, canReview: true,
      changeSummary: { caveFields: 1, entrances: { added: 0, removed: 0, changed: 0, total: 0 },
        files: { added: 0, removed: 0, changed: 0, total: 0 },
        linePlots: { added: 0, removed: 0, changed: 0, total: 0 }, otherChanges: 0 },
    },
    base: { ...snapshot, name: "" }, proposed: snapshot, current: { ...snapshot, name: "" },
    diff: {} as any, versions: [], countyNumberIntent: "FirstAvailable",
    linePlots: [], activeStagedFiles: [], unavailableStagedFileIds: [],
  });
  jest.spyOn(CaveService, "GetProposalVersion").mockResolvedValue({
    base: { ...snapshot, name: "" }, proposed: snapshot, diffFromBase: {} as any,
    baseRevisionChanged: false, baseRevisionId: undefined, countyNumberIntent: "FirstAvailable",
    linePlots: [], unavailableStagedFileIds: [],
  });
  const canManage = jest.spyOn(AppService, "HasCavePermission").mockResolvedValue(true);
  const preview = jest.spyOn(CaveService, "PreviewRevisedChanges").mockResolvedValue({
    base: { ...snapshot, name: "" }, proposed: snapshot, diff: {} as any,
    countyNumberIntent: "FirstAvailable", linePlots: [], hasMeaningfulChanges: true,
  });

  renderPage();
  await screen.findByRole("textbox", { name: "Name" });
  await waitFor(() => expect(canManage).toHaveBeenCalledWith("Manager", null, "county"));
  await waitFor(() =>
    expect(screen.getByTestId("county-number-controls-hidden")).toHaveTextContent("false")
  );

  fireEvent.click(screen.getByRole("button", { name: "Review changes" }));

  await waitFor(() => expect(preview).toHaveBeenCalledWith(
    "request0001",
    expect.objectContaining({
      id: "cave000001",
      isCountyNumberManuallySet: false,
      useFirstAvailableCountyNumber: true,
    }),
    false, undefined, "proposal01"));
});
