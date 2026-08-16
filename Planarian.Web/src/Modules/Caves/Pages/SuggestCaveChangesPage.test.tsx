import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { CaveService } from "../Service/CaveService";
import { SuggestCaveChangesPage } from "./SuggestCaveChangesPage";

jest.mock("../Components/AddCaveComponent", () => ({
  AddCaveComponent: () => <button type="submit">Preview changes</button>,
}));

jest.mock("../Components/CaveRevisionDiff", () => ({
  CaveRevisionDiff: () => <div data-testid="revision-diff" />,
}));

jest.mock("../../../Shared/Components/Buttons/PlanarianButtton", () => ({
  PlanarianButton: ({ children, loading: _loading, icon: _icon, ...props }: any) =>
    <button {...props}>{children}</button>,
}));

jest.mock("../Helpers/CaveFormMapper", () => ({
  caveToForm: () => ({ id: "cave000001", name: "Published cave" }),
}));

afterEach(() => jest.restoreAllMocks());

it("prevents submitting a change request when the authoritative preview is semantically unchanged", async () => {
  jest.spyOn(CaveService, "GetProposalAuthoringContext").mockResolvedValue({
    cave: { id: "cave000001", name: "Published cave" } as any,
    expectedBaseRevisionId: "revision01",
    linePlots: [],
  });
  jest.spyOn(CaveService, "PreviewChanges").mockResolvedValue({
    base: {} as any, proposed: {} as any, diff: {} as any,
    countyNumberIntent: "Manual", hasMeaningfulChanges: false,
  });
  const submit = jest.spyOn(CaveService, "SubmitChanges").mockResolvedValue("request0001");

  render(
    <MemoryRouter initialEntries={["/caves/cave000001/suggest-changes"]}>
      <Routes>
        <Route path="/caves/:caveId/suggest-changes" element={<SuggestCaveChangesPage />} />
      </Routes>
    </MemoryRouter>
  );

  fireEvent.click(await screen.findByRole("button", { name: "Preview changes" }));

  expect(await screen.findByText(/make at least one meaningful change/i)).toBeInTheDocument();
  const submitButton = screen.getByRole("button", { name: "Submit for review" });
  expect(submitButton).toBeDisabled();
  fireEvent.click(submitButton);
  await waitFor(() => expect(submit).not.toHaveBeenCalled());
});
