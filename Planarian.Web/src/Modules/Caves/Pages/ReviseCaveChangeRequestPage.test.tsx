import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { CaveService } from "../Service/CaveService";
import { ReviseCaveChangeRequestPage } from "./ReviseCaveChangeRequestPage";

jest.mock("../Components/AddCaveComponent", () => ({
  AddCaveComponent: () => <button type="submit">Preview revision</button>,
}));

jest.mock("../Components/CaveRevisionDiff", () => ({
  CaveRevisionDiff: () => <div data-testid="revision-diff" />,
}));

jest.mock("../../../Shared/Components/Buttons/PlanarianButtton", () => ({
  PlanarianButton: ({ children, loading: _loading, icon: _icon, ...props }: any) =>
    <button {...props}>{children}</button>,
}));

jest.mock("../Helpers/CaveFormMapper", () => ({
  caveToForm: () => ({ id: "cave000001", name: "Proposed name" }),
  snapshotToForm: () => ({ id: "cave000001", name: "Proposed name" }),
}));

afterEach(() => jest.restoreAllMocks());

it("prevents saving a proposal version when the authoritative preview is semantically unchanged", async () => {
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
  jest.spyOn(CaveService, "PreviewRevisedChanges").mockResolvedValue({
    base: {} as any, proposed: {} as any, diff: {} as any,
    countyNumberIntent: "Manual", hasMeaningfulChanges: false,
  });
  const revise = jest.spyOn(CaveService, "ReviseChanges").mockResolvedValue("proposal02");

  render(
    <MemoryRouter initialEntries={["/caves/requests/request0001/revise"]}>
      <Routes>
        <Route path="/caves/requests/:requestId/revise" element={<ReviseCaveChangeRequestPage />} />
      </Routes>
    </MemoryRouter>
  );

  fireEvent.click(await screen.findByRole("button", { name: "Preview revision" }));

  expect(await screen.findByText(/would not create a meaningful new proposal version/i)).toBeInTheDocument();
  const save = screen.getByRole("button", { name: "Save proposal version" });
  expect(save).toBeDisabled();
  fireEvent.click(save);
  await waitFor(() => expect(revise).not.toHaveBeenCalled());
});
