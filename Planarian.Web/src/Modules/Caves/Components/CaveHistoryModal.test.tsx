import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { CaveHistoryModal } from "./CaveHistoryModal";
import { CaveService } from "../Service/CaveService";
import { CaveRevisionHistoryVm } from "../Models/CaveRevisionVm";

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
