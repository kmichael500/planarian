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

const summary = (status: CaveChangeRequestSummaryVm["status"] = "Pending"): CaveChangeRequestSummaryVm => ({
  id: "request001", caveId: "cave000001", caveName: "Helms Deep Cave", caveExists: true, isCreation: false,
  status, submitterUserId: "user000001", submitterName: "Michael Test",
  submittedOn: "2026-08-19T12:00:00Z", updatedOn: "2026-08-19T12:10:00Z",
  originalBaseRevisionId: "revision01", proposalBaseRevisionId: "revision01",
  currentRevisionId: "revision01", currentProposalVersionId: "version001",
  isStale: false, canEdit: false, canReview: true,
  changeSummary: {
    caveFields: 2,
    entrances: { added: 0, removed: 0, changed: 0, total: 0 },
    files: { added: 1, removed: 0, changed: 0, total: 1 },
    linePlots: { added: 0, removed: 0, changed: 0, total: 0 },
    otherChanges: 0,
  },
});
it("does not repeat Pending review on ordinary reviewer-queue rows", () => {
  render(<MemoryRouter><CaveChangeRequestList requests={[summary()]} review /></MemoryRouter>);

  expect(screen.queryByText("Pending review")).not.toBeInTheDocument();
  expect(document.body).toHaveTextContent(/Michael Test · Submitted 2026 Aug-19/);
});

it("still shows Pending review in My submissions", () => {
  render(<MemoryRouter><CaveChangeRequestList requests={[summary()]} /></MemoryRouter>);

  expect(screen.getByText("Pending review")).toBeInTheDocument();
});

it("uses a neutral empty-state explanation for My submissions", () => {
  render(<MemoryRouter><CaveChangeRequestList requests={[]} /></MemoryRouter>);

  expect(screen.getByText("No submissions found")).toBeInTheDocument();
  expect(screen.getByText("Try a different status filter, or submit changes to a Cave.")).toBeInTheDocument();
});

it("labels submissions that are waiting on the author as Changes requested", () => {
  render(<MemoryRouter><CaveChangeRequestList requests={[summary("ChangesRequested")]} /></MemoryRouter>);

  expect(screen.getByText("Changes requested")).toBeInTheDocument();
  expect(screen.queryByText("Pending review")).not.toBeInTheDocument();
});

it("makes the whole request row navigate to the request detail and summarizes semantic scope", () => {
  render(<MemoryRouter><CaveChangeRequestList requests={[summary()]} /></MemoryRouter>);

  expect(screen.getByRole("link", { name: /Helms Deep Cave/ })).toHaveAttribute(
    "href", "/caves/requests/request001");
  expect(screen.getByText("2 cave fields · 1 file")).toBeInTheDocument();
});

it("surfaces stale requests as needing re-review without hiding their scope", () => {
  render(<MemoryRouter><CaveChangeRequestList requests={[{ ...summary(), isStale: true }]} review /></MemoryRouter>);

  expect(screen.getByText("Needs re-review")).toBeInTheDocument();
  expect(screen.getByText("2 cave fields · 1 file")).toBeInTheDocument();
});

it("uses an intentional reviewer-queue empty state", () => {
  render(<MemoryRouter><CaveChangeRequestList requests={[]} review /></MemoryRouter>);

  expect(screen.getByText("No review submissions found")).toBeInTheDocument();
  expect(screen.getByText("Try a different status filter, or check back later.")).toBeInTheDocument();
});
