import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { AppService } from "../../../Shared/Services/AppService";
import { CaveService } from "../Service/CaveService";
import { AddCavesPage } from "./AddCavePage";

jest.mock("../Components/AddCaveComponent", () => {
  const { Form, Input } = require("antd");
  return {
    AddCaveComponent: ({ form, submitLabel }: { form: any; submitLabel?: string }) => (
      <>
        <Form.Item name="name" label="Name"><Input /></Form.Item>
        <button type="button" onClick={() => form.setFieldsValue({
          stateId: "state00001",
          countyId: "county0001",
          biologyTagIds: ["biology-a"],
          files: [{ id: "file-a", name: "Survey", extension: ".pdf", fileTypeTagId: "file-type-a", fileTypeKey: "Survey" }],
          linePlots: [{ id: "line-a", name: "Traverse", geoJson: "{\"type\":\"FeatureCollection\",\"features\":[]}" }],
        })}>Populate hidden authoring values</button>
        <button type="button" onClick={() => form.setFieldsValue({
          countyNumber: 42,
          isCountyNumberManuallySet: true,
          useFirstAvailableCountyNumber: true,
        })}>Populate stale county number</button>
        <button type="submit">{submitLabel ?? "Submit"}</button>
      </>
    ),
  };
});

jest.mock("../Components/CaveRevisionDiff", () => ({
  CaveRevisionDiff: () => <div>Proposal diff</div>,
}));

jest.mock("../../../Shared/Components/Buttons/BackButtonComponent", () => ({
  BackButtonComponent: () => null,
}));

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({
      matches: false,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }),
  });
});

afterEach(() => jest.restoreAllMocks());

const renderPage = () => {
  const router = createMemoryRouter([
    { path: "/caves/add", element: <AddCavesPage /> },
    { path: "/caves", element: <div>Cave list</div> },
    { path: "/caves/requests/:requestId", element: <div>Request detail</div> },
  ], { initialEntries: ["/caves/add"] });
  render(<RouterProvider router={router} />);
  return router;
};

const populateForm = async () => {
  await userEvent.type(screen.getByRole("textbox", { name: "Name" }), "Created Cave");
  fireEvent.click(screen.getByRole("button", { name: "Populate hidden authoring values" }));
};

const completeValues = () => expect.objectContaining({
  name: "Created Cave",
  stateId: "state00001",
  countyId: "county0001",
  biologyTagIds: ["biology-a"],
  files: [expect.objectContaining({ id: "file-a", name: "Survey", fileTypeTagId: "file-type-a" })],
  linePlots: [expect.objectContaining({ id: "line-a", name: "Traverse" })],
});

it("publishes directly when the user manages the selected County", async () => {
  jest.spyOn(AppService, "HasCavePermission").mockResolvedValue(true);
  const add = jest.spyOn(CaveService, "AddCave").mockResolvedValue("cave-created");
  const preview = jest.spyOn(CaveService, "PreviewNewCave");
  renderPage();

  await populateForm();
  fireEvent.click(screen.getByRole("button", { name: /Add cave|Review submission/ }));

  await waitFor(() => expect(add).toHaveBeenCalledTimes(1));
  expect(add).toHaveBeenCalledWith(completeValues());
  expect(preview).not.toHaveBeenCalled();
  expect(await screen.findByText("Cave list")).toBeInTheDocument();
});

it("submits the complete new-Cave proposal when the selected County is not managed", async () => {
  jest.spyOn(AppService, "HasCavePermission").mockResolvedValue(false);
  const preview = jest.spyOn(CaveService, "PreviewNewCave").mockResolvedValue({
    base: { caveId: "preview", accountId: "account", name: "", state: { id: "", nameAtRevision: "" },
      county: { id: "", nameAtRevision: "" }, countyNumber: 0, isArchived: false,
      alternateNames: [], tags: [], entrances: [], files: [], linePlots: [] },
    proposed: { caveId: "preview", accountId: "account", name: "Created Cave",
      state: { id: "state00001", nameAtRevision: "State" },
      county: { id: "county0001", nameAtRevision: "County" }, countyNumber: 0, isArchived: false,
      alternateNames: [], tags: [], entrances: [], files: [], linePlots: [] },
    diff: {} as any,
    countyNumberIntent: "AutomaticNext",
    likelyCountyNumber: 56,
    hasMeaningfulChanges: true,
  } as any);
  const submit = jest.spyOn(CaveService, "SubmitNewCave").mockResolvedValue("request001");
  const add = jest.spyOn(CaveService, "AddCave");
  renderPage();

  await populateForm();
  fireEvent.click(screen.getByRole("button", { name: /Add cave|Review submission/ }));

  await screen.findByText("Review new Cave submission");
  expect(preview).toHaveBeenCalledWith(completeValues());
  expect(add).not.toHaveBeenCalled();

  const submitForReview = await screen.findByText("Submit for review");
  fireEvent.click(submitForReview.closest("button")!);
  await waitFor(() => expect(submit).toHaveBeenCalledWith(completeValues()));
  expect(await screen.findByText("Request detail")).toBeInTheDocument();
});

it("clears stale Manager-only county-number state before creating a proposal", async () => {
  jest.spyOn(AppService, "HasCavePermission").mockResolvedValue(false);
  const preview = jest.spyOn(CaveService, "PreviewNewCave").mockResolvedValue({
    base: { caveId: "preview", accountId: "account", name: "", state: { id: "", nameAtRevision: "" },
      county: { id: "", nameAtRevision: "" }, countyNumber: 0, isArchived: false,
      alternateNames: [], tags: [], entrances: [], files: [], linePlots: [] },
    proposed: { caveId: "preview", accountId: "account", name: "Created Cave",
      state: { id: "state00001", nameAtRevision: "State" },
      county: { id: "county0001", nameAtRevision: "County" }, countyNumber: 0, isArchived: false,
      alternateNames: [], tags: [], entrances: [], files: [], linePlots: [] },
    diff: {} as any, countyNumberIntent: "AutomaticNext", hasMeaningfulChanges: true,
  } as any);
  renderPage();

  await populateForm();
  fireEvent.click(screen.getByRole("button", { name: "Populate stale county number" }));
  fireEvent.click(screen.getByRole("button", { name: /Add cave|Review submission/ }));

  await waitFor(() => expect(preview).toHaveBeenCalledWith(expect.objectContaining({
    countyId: "county0001",
    countyNumber: null,
    isCountyNumberManuallySet: false,
    useFirstAvailableCountyNumber: false,
  })));
});
