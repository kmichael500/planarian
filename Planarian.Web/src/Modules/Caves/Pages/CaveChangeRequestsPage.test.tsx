import userEvent from "@testing-library/user-event";
import { render, screen, waitFor } from "@testing-library/react";
import React, { useContext } from "react";
import { MemoryRouter } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { CaveService } from "../Service/CaveService";
import { CaveChangeRequestsPage } from "./CaveChangeRequestsPage";

jest.mock("../Components/CaveChangeRequestList", () => ({
  CaveChangeRequestList: ({ requests, review }: { requests: { id: string }[]; review?: boolean }) =>
    <div data-testid="requests" data-review={review ? "yes" : "no"}>{requests.map(request => request.id).join(",")}</div>,
}));

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({ matches: false, addListener: () => undefined, removeListener: () => undefined,
      addEventListener: () => undefined, removeEventListener: () => undefined, dispatchEvent: () => false }),
  });
});

const page = (id: string, totalCount = 1) => ({ pageNumber: 1, pageSize: 10, totalCount,
  totalPages: Math.max(1, Math.ceil(totalCount / 10)), results: [{ id } as any] });
const setHeaderTitle = jest.fn();

const AppContextOverride: React.FC<{ manager?: boolean; children: React.ReactNode }> = ({ manager = false, children }) => {
  const defaults = useContext(AppContext);
  return <AppContext.Provider value={{ ...defaults, setHeaderTitle,
    hasPermission: permission => manager && permission === PermissionKey.Manager }}>{children}</AppContext.Provider>;
};

const renderPage = (manager = false, initialEntry = "/caves/changes") => render(
  <MemoryRouter initialEntries={[initialEntry]}>
    <AppContextOverride manager={manager}><CaveChangeRequestsPage /></AppContextOverride>
  </MemoryRouter>
);

describe("CaveChangeRequestsPage", () => {
  beforeEach(() => {
    jest.restoreAllMocks();
    setHeaderTitle.mockClear();
  });

  it("shows a simple Submissions workspace to users who cannot review", async () => {
    jest.spyOn(CaveService, "GetMyChangeRequests").mockResolvedValue(page("mine"));
    renderPage(false);

    await screen.findByText("mine");
    expect(setHeaderTitle).toHaveBeenLastCalledWith(["Submissions"]);
    expect(screen.queryByText("My submissions")).not.toBeInTheDocument();
    expect(screen.queryByText("Review submissions")).not.toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Submission status" })).toBeInTheDocument();
  });
  it("defaults managers to Review submissions and keeps My submissions available", async () => {
    const getReview = jest.spyOn(CaveService, "GetReviewQueue").mockResolvedValue(page("review"));
    jest.spyOn(CaveService, "GetMyChangeRequests").mockResolvedValue(page("mine"));
    renderPage(true);

    await screen.findByText("review");
    expect(setHeaderTitle).toHaveBeenLastCalledWith(["Cave Changes"]);
    expect(screen.getByText("Review submissions")).toBeVisible();
    expect(screen.getByText("My submissions")).toBeVisible();
    expect(getReview).toHaveBeenCalledWith(1, 10, undefined, "SubmittedOn", false);
    expect(screen.getByTestId("requests")).toHaveAttribute("data-review", "yes");
  });

  it("filters Review submissions on the server", async () => {
    const getReview = jest.spyOn(CaveService, "GetReviewQueue").mockResolvedValue(page("review", 11));
    renderPage(true);
    await screen.findByText("review");

    await userEvent.click(screen.getByRole("combobox", { name: "Submission status" }));
    await userEvent.click(await screen.findByText("Approved"));

    await waitFor(() => expect(getReview).toHaveBeenLastCalledWith(1, 10, "Approved", "SubmittedOn", false));
  });

  it("switches to My submissions with its own default sort", async () => {
    jest.spyOn(CaveService, "GetReviewQueue").mockResolvedValue(page("review"));
    const getMine = jest.spyOn(CaveService, "GetMyChangeRequests").mockResolvedValue(page("mine"));
    renderPage(true);
    await screen.findByText("review");

    await userEvent.click(screen.getByText("My submissions"));
    await screen.findByText("mine");
    expect(getMine).toHaveBeenLastCalledWith(1, 10, undefined, "UpdatedOn", true);
    expect(screen.getByTestId("requests")).toHaveAttribute("data-review", "no");
  });
  it("sorts through the server-paged Review query", async () => {
    const getReview = jest.spyOn(CaveService, "GetReviewQueue").mockResolvedValue(page("review", 11));
    renderPage(true);
    await screen.findByText("review");

    await userEvent.click(screen.getByRole("button", { name: /Submitted/i }));
    await userEvent.click(await screen.findByText("Cave name"));

    await waitFor(() => expect(getReview).toHaveBeenLastCalledWith(1, 10, undefined, "CaveName", false));
  });
});

it("offers Changes requested only in My submissions, not the reviewer queue", async () => {
  jest.spyOn(CaveService, "GetReviewQueue").mockResolvedValue(page("review"));
  const getMine = jest.spyOn(CaveService, "GetMyChangeRequests").mockResolvedValue(page("mine"));
  renderPage(true);
  await screen.findByText("review");

  await userEvent.click(screen.getByRole("combobox", { name: "Submission status" }));
  expect(screen.queryByText("Changes requested")).not.toBeInTheDocument();
  await userEvent.keyboard("{Escape}");
  await userEvent.click(screen.getByText("My submissions"));
  await screen.findByText("mine");

  await userEvent.click(screen.getByRole("combobox", { name: "Submission status" }));
  await userEvent.click(await screen.findByText("Changes requested"));
  await waitFor(() => expect(getMine).toHaveBeenLastCalledWith(
    1, 10, "ChangesRequested", "UpdatedOn", true));
});
