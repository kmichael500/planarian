import { fireEvent, render, waitFor } from "@testing-library/react";
import { CaveService } from "../Service/CaveService";
import { CaveChangeRequestsPage } from "./CaveChangeRequestsPage";

jest.mock("../Components/CaveChangeRequestList", () => ({
  CaveChangeRequestList: ({ requests }: { requests: { id: string }[] }) =>
    <div data-testid="requests">{requests.map(request => request.id).join(",")}</div>,
}));

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({ matches: false, addListener: () => undefined, removeListener: () => undefined }),
  });
});

it("loads bounded pages and requests the selected page", async () => {
  const getMine = jest.spyOn(CaveService, "GetMyChangeRequests")
    .mockResolvedValueOnce({ pageNumber: 1, pageSize: 10, totalCount: 11, totalPages: 2,
      results: [{ id: "first" } as any] })
    .mockResolvedValueOnce({ pageNumber: 2, pageSize: 10, totalCount: 11, totalPages: 2,
      results: [{ id: "second" } as any] });

  const view = render(<CaveChangeRequestsPage />);
  await waitFor(() => expect(view.getByTestId("requests")).toHaveTextContent("first"));
  expect(getMine).toHaveBeenCalledWith(1, 10);

  fireEvent.click(view.getByTitle("2"));
  await waitFor(() => expect(view.getByTestId("requests")).toHaveTextContent("second"));
  expect(getMine).toHaveBeenLastCalledWith(2, 10);
});

it("returns to the authoritative server page when the list shrinks", async () => {
  const getMine = jest.spyOn(CaveService, "GetMyChangeRequests")
    .mockResolvedValueOnce({ pageNumber: 1, pageSize: 10, totalCount: 11, totalPages: 2,
      results: [{ id: "first" } as any] })
    .mockResolvedValueOnce({ pageNumber: 1, pageSize: 10, totalCount: 1, totalPages: 1,
      results: [{ id: "remaining" } as any] })
    .mockResolvedValueOnce({ pageNumber: 1, pageSize: 10, totalCount: 1, totalPages: 1,
      results: [{ id: "remaining" } as any] });

  const view = render(<CaveChangeRequestsPage />);
  await waitFor(() => expect(view.getByTestId("requests")).toHaveTextContent("first"));
  fireEvent.click(view.getByTitle("2"));

  await waitFor(() => expect(view.getByTestId("requests")).toHaveTextContent("remaining"));
  await waitFor(() => expect(getMine).toHaveBeenCalledTimes(3));
  expect(getMine).toHaveBeenLastCalledWith(1, 10);
  expect(view.queryByTitle("2")).not.toBeInTheDocument();
});
