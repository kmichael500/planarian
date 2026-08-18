import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { CaveChangeRequestSummaryVm } from "../Models/CaveChangeRequestVm";
import { CaveChangeRequestList } from "./CaveChangeRequestList";


beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({ matches: false, addListener: () => undefined, removeListener: () => undefined,
      addEventListener: () => undefined, removeEventListener: () => undefined, dispatchEvent: () => false }),
  });
});

const request = (overrides: Partial<CaveChangeRequestSummaryVm> = {}): CaveChangeRequestSummaryVm => ({
  id: "request001",
  caveId: "cave000001",
  caveName: "Alpha Cave",
  caveExists: true,
  status: "Pending",
  submitterUserId: "user000001",
  submitterName: "Casey Contributor",
  submittedOn: "2026-08-17T18:00:00Z",
  updatedOn: "2026-08-17T19:00:00Z",
  originalBaseRevisionId: "revision01",
  proposalBaseRevisionId: "revision01",
  currentRevisionId: "revision01",
  currentProposalVersionId: "proposal01",
  isStale: false,
  canEdit: true,
  canReview: false,
  changeSummary: {
    caveFields: 3,
    entrances: { added: 1, removed: 0, changed: 1, total: 2 },
    files: { added: 1, removed: 0, changed: 0, total: 1 },
    linePlots: { added: 0, removed: 0, changed: 0, total: 0 },
    otherChanges: 0,
  },
  ...overrides,
});

const renderList = (requests: CaveChangeRequestSummaryVm[], review = false) => render(
  <MemoryRouter><CaveChangeRequestList requests={requests} review={review} /></MemoryRouter>
);

describe("CaveChangeRequestList", () => {
  it("renders a compact deterministic scope summary and useful updated metadata", () => {
    renderList([request()]);

    expect(screen.getByRole("link", { name: /Alpha Cave/ })).toHaveAttribute("href", "/caves/requests/request001");
    expect(screen.getByText("3 cave fields · 2 entrances · 1 file")).toBeVisible();
    expect(screen.getByText(/Updated/)).toBeVisible();
  });

  it("uses needs re-review instead of conflict language", () => {
    renderList([request({ isStale: true })], true);

    expect(screen.getByText("Needs re-review")).toBeVisible();
    expect(screen.queryByText(/Conflict/)).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Casey Contributor/ })).toBeVisible();
  });

  it("does not repeat pending status in the review queue", () => {
    renderList([request()], true);
    expect(screen.queryByText("Pending")).not.toBeInTheDocument();
  });

  it("has intentional empty states", () => {
    const view = renderList([], false);
    expect(screen.getByText("No submissions yet")).toBeVisible();
    view.unmount();
    renderList([], true);
    expect(screen.getByText("Nothing is waiting for review")).toBeVisible();
  });
});
