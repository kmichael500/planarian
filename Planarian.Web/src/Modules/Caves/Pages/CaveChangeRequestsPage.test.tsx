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

const AppContextOverride: React.FC<{ manager?: boolean; children: React.ReactNode }> = ({ manager = false, children }) => {
  const defaults = useContext(AppContext);
  return <AppContext.Provider value={{ ...defaults,
    hasPermission: permission => manager && permission === PermissionKey.Manager }}>{children}</AppContext.Provider>;
};

const renderPage = (manager = false, initialEntry = "/caves/changes") => render(
  <MemoryRouter initialEntries={[initialEntry]}>
    <AppContextOverride manager={manager}><CaveChangeRequestsPage /></AppContextOverride>
  </MemoryRouter>
);

describe("CaveChangeRequestsPage", () => {
  beforeEach(() => jest.restoreAllMocks());

  it("uses one Cave Changes workspace and only shows the review tab to managers", async () => {
    jest.spyOn(CaveService, "GetMyChangeRequests").mockResolvedValue(page("mine"));
    const view = renderPage(false);
    await screen.findByText("mine");
    await screen.findByText("1 submission");
    expect(screen.getByRole("tab", { name: "My submissions" })).toBeVisible();
    expect(screen.queryByRole("tab", { name: "Review queue" })).not.toBeInTheDocument();
    view.unmount();

    jest.spyOn(CaveService, "GetReviewQueue").mockResolvedValue(page("review"));
    renderPage(true);
    await screen.findByText("mine");
    expect(screen.getByRole("tab", { name: "Review queue" })).toBeVisible();
  });

  it("switches to the review queue without mixing its paging state with My submissions", async () => {
    jest.spyOn(CaveService, "GetMyChangeRequests").mockResolvedValue(page("mine"));
    const getReview = jest.spyOn(CaveService, "GetReviewQueue").mockResolvedValue(page("review"));
    renderPage(true);
    await screen.findByText("mine");
    await screen.findByText("1 submission");

    await userEvent.click(screen.getByRole("tab", { name: "Review queue" }));
    await screen.findByText("review");
    expect(getReview).toHaveBeenCalledWith(1, 10);
    expect(screen.getByTestId("requests")).toHaveAttribute("data-review", "yes");
  });

  it("filters My submissions on the server and resets to page one", async () => {
    const getMine = jest.spyOn(CaveService, "GetMyChangeRequests").mockResolvedValue(page("mine", 11));
    renderPage(true);
    await screen.findByText("mine");
    await screen.findByText("11 submissions");

    await userEvent.click(screen.getByRole("combobox", { name: "Submission status" }));
    await userEvent.click(await screen.findByText("Approved"));

    await waitFor(() => expect(getMine).toHaveBeenLastCalledWith(1, 10, "Approved"));
  });
});
