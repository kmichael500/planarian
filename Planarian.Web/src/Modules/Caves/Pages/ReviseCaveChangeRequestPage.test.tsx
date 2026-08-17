import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { CaveService } from "../Service/CaveService";
import { ReviseCaveChangeRequestPage } from "./ReviseCaveChangeRequestPage";

jest.mock("../Components/AddCaveComponent", () => {
  const { Form, Input } = require("antd");
  return {
    AddCaveComponent: () => (
      <>
        <Form.Item name="name" label="Name"><Input /></Form.Item>
        <button type="submit">Review changes</button>
      </>
    ),
  };
});

jest.mock("../Components/CaveRevisionDiff", () => ({
  CaveRevisionDiff: () => <div data-testid="revision-diff" />,
}));

jest.mock("../../../Shared/Components/Buttons/PlanarianButtton", () => ({
  PlanarianButton: ({ children, loading: _loading, icon: _icon, danger: _danger, ...props }: any) =>
    <button {...props}>{children}</button>,
}));

jest.mock("../../../Shared/Components/Buttons/PlanarianModal", () => ({
  PlanarianModal: ({ open, header, footer, children }: any) =>
    open ? <div role="dialog" aria-label={header}>{children}{footer}</div> : null,
}));

jest.mock("../Helpers/CaveFormMapper", () => ({
  caveToForm: () => ({ id: "cave000001", name: "Proposed name" }),
  snapshotToForm: () => ({ id: "cave000001", name: "Proposed name" }),
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
      status: "Pending", submitterUserId: "contributor", submittedOn: "2026-08-16T00:00:00Z",
      originalBaseRevisionId: "revision01", proposalBaseRevisionId: "revision01",
      currentRevisionId: "revision01", currentProposalVersionId: "proposal01",
      isStale: false, canEdit: true, canReview: false,
    },
    base: {} as any, proposed: {} as any, current: {} as any, diff: {} as any,
    versions: [], countyNumberIntent: "Manual", activeStagedFiles: [], unavailableStagedFileIds: [],
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

it("prevents saving a proposal version when the authoritative preview is semantically unchanged", async () => {
  mockPendingRequest();
  jest.spyOn(CaveService, "PreviewRevisedChanges").mockResolvedValue({
    base: {} as any, proposed: {} as any, diff: {} as any,
    countyNumberIntent: "Manual", hasMeaningfulChanges: false,
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
    countyNumberIntent: "Manual", hasMeaningfulChanges: true,
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
