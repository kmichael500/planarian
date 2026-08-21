import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { CaveHistoryModal } from "./CaveHistoryModal";
import { CaveService } from "../Service/CaveService";
import { CaveRevisionHistoryVm } from "../Models/CaveRevisionVm";
import { MemoryRouter } from "react-router-dom";

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({ matches: false, addListener: () => undefined, removeListener: () => undefined }),
  });
  Object.defineProperty(HTMLDialogElement.prototype, "showModal", {
    value() { this.setAttribute("open", ""); },
  });
  Object.defineProperty(HTMLDialogElement.prototype, "close", {
    value() { this.removeAttribute("open"); },
  });
  Object.defineProperty(window, "scrollTo", { value: () => undefined });
});

const history = (caveId: string, actorName: string): CaveRevisionHistoryVm => ({
  caveId,
  currentRevisionId: `${caveId}-revision`,
  revisions: [{
    id: `${caveId}-revision`, caveId, sequence: 1, publishedOn: "2026-08-11T12:00:00Z",
    actorName, source: "ManagerEdit", operation: "Update", isCurrent: true,
  }],
});

it("clears cached history and loads the new Cave when caveId changes while open", async () => {
  const load = jest.spyOn(CaveService, "GetRevisionHistory")
    .mockResolvedValueOnce(history("cave-a", "Actor A"))
    .mockResolvedValueOnce(history("cave-b", "Actor B"));
  const view = render(<CaveHistoryModal caveId="cave-a" />);

  fireEvent.click(document.querySelector("button")!);
  await waitFor(() => expect(document.body).toHaveTextContent("Actor A"));

  view.rerender(<CaveHistoryModal caveId="cave-b" />);
  await waitFor(() => expect(document.body).toHaveTextContent("Actor B"));
  expect(document.body).not.toHaveTextContent("Actor A");
  expect(load).toHaveBeenNthCalledWith(1, "cave-a");
  expect(load).toHaveBeenNthCalledWith(2, "cave-b");
});

it("shows a revision load error inside the expanded revision and stops its spinner", async () => {
  jest.spyOn(CaveService, "GetRevisionHistory").mockResolvedValue(history("cave-a", "Actor A"));
  jest.spyOn(CaveService, "GetRevision").mockRejectedValue(new Error("load failed"));
  render(<CaveHistoryModal caveId="cave-a" />);

  fireEvent.click(screen.getByRole("button", { name: /history/i }));
  await screen.findByText(/Actor A/);
  fireEvent.click(screen.getByText("Edited"));

  const error = await screen.findByText("That revision could not be loaded.");
  const revisionPanel = error.closest(".ant-collapse-content");
  expect(revisionPanel).not.toBeNull();
  expect(revisionPanel).not.toHaveClass("ant-collapse-content-hidden");
  expect(error.closest(".ant-alert")).toBe(revisionPanel!.querySelector(".ant-alert"));
  expect(document.querySelectorAll(".ant-spin-spinning")).toHaveLength(0);
});


it("shows approved-request attribution without exposing a Review link to an ordinary viewer", async () => {
  const approved: CaveRevisionHistoryVm = {
    caveId: "cave-a", currentRevisionId: "revision-approved",
    revisions: [{ id: "revision-approved", caveId: "cave-a", sequence: 2, publishedOn: "2026-08-12T12:00:00Z",
      actorName: "Riley Reviewer", source: "UserSubmission", operation: "Update", isCurrent: true,
      changeRequestId: "request001", changeRequestSubmitterUserId: "submitter1",
      changeRequestSubmitterName: "Casey Submitter", changeRequestReviewerName: "Riley Reviewer" }],
  };
  jest.spyOn(CaveService, "GetRevisionHistory").mockResolvedValue(approved);
  jest.spyOn(CaveService, "GetRevision").mockResolvedValue({ revision: approved.revisions[0], current: {} as any, diff: undefined } as any);

  render(<MemoryRouter><CaveHistoryModal caveId="cave-a" /></MemoryRouter>);
  fireEvent.click(screen.getByRole("button", { name: /history/i }));

  expect(await screen.findByText("Submitted by Casey Submitter · Approved by Riley Reviewer")).toBeInTheDocument();
  fireEvent.click(screen.getByText("Changes approved"));
  await waitFor(() => expect(CaveService.GetRevision).toHaveBeenCalled());
  expect(screen.queryByRole("link", { name: "Review" })).not.toBeInTheDocument();
});

it("shows approved-request attribution and links authorized reviewers back to the review record", async () => {
  const approved: CaveRevisionHistoryVm = {
    caveId: "cave-a",
    currentRevisionId: "revision-approved",
    revisions: [{
      id: "revision-approved", caveId: "cave-a", sequence: 2, publishedOn: "2026-08-12T12:00:00Z",
      actorName: "Riley Reviewer", source: "UserSubmission", operation: "Update", isCurrent: true,
      changeRequestId: "request001", changeRequestSubmitterUserId: "submitter1",
      changeRequestSubmitterName: "Casey Submitter", changeRequestReviewerName: "Riley Reviewer",
    }],
  };
  jest.spyOn(CaveService, "GetRevisionHistory").mockResolvedValue(approved);
  jest.spyOn(CaveService, "GetRevision").mockResolvedValue({
    revision: approved.revisions[0], current: {} as any, diff: undefined,
  } as any);

  render(<MemoryRouter><CaveHistoryModal caveId="cave-a" canReviewChangeRequests /></MemoryRouter>);
  fireEvent.click(screen.getByRole("button", { name: /history/i }));

  expect(await screen.findByText("Changes approved")).toBeInTheDocument();
  expect(screen.getByText("Submitted by Casey Submitter · Approved by Riley Reviewer")).toBeInTheDocument();
  fireEvent.click(screen.getByText("Changes approved"));
  expect(await screen.findByRole("link", { name: "Review" })).toHaveAttribute("href", "/caves/requests/request001");
});
