import { render } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { CaveAvailability } from "./CaveChangeRequestPage";
import { CaveChangeRequestSummaryVm } from "../Models/CaveChangeRequestVm";

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
