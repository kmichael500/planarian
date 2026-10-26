import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { CaveService } from "../Service/CaveService";
import { EditCavePage } from "./EditCavePage";

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: (query: string) => ({
      matches: false, media: query, onchange: null,
      addListener: () => undefined, removeListener: () => undefined,
      addEventListener: () => undefined, removeEventListener: () => undefined, dispatchEvent: () => false,
    }),
  });
});

jest.mock("../Components/AddCaveComponent", () => {
  const { Form, Input } = require("antd");
  return {
    AddCaveComponent: ({ onAuthoringChange }: { onAuthoringChange?: () => void }) => (
      <>
        <Form.Item name="name" label="Name"><Input /></Form.Item>
        <button type="button" onClick={onAuthoringChange}>Simulate asset change</button>
        <button type="submit">Save cave</button>
      </>
    ),
  };
});

jest.mock("../../../Shared/Components/Buttons/DeleteButtonComponent", () => ({
  DeleteButtonComponent: () => null,
}));

jest.mock("../../../Shared/Components/Buttons/BackButtonComponent", () => ({
  BackButtonComponent: () => null,
}));

jest.mock("../../../Shared/Components/Buttons/PlanarianButtton", () => ({
  PlanarianButton: ({ children, icon: _icon, ...props }: any) => <button {...props}>{children}</button>,
}));

jest.mock("../../../Shared/Components/Buttons/PlanarianModal", () => ({
  PlanarianModal: ({ open, header, footer, children }: any) =>
    open ? <div role="dialog" aria-label={header}>{children}{footer}</div> : null,
}));

jest.mock("../Helpers/CaveFormMapper", () => ({
  caveToForm: () => ({
    id: "cave000001", expectedRevisionId: "revision01", name: "Published cave", depthFeet: null,
    biologyTagIds: ["biology-hidden"],
    files: [{ id: "file-hidden", name: "Existing map", extension: ".pdf", fileTypeTagId: "file-type", fileTypeKey: "Map" }],
    linePlots: [{ id: "line-hidden", name: "Existing traverse", geoJson: "{\"type\":\"FeatureCollection\",\"features\":[]}" }],
  }),
}));

const cave = {
  id: "cave000001", currentRevisionId: "revision01", displayId: "DAV-1", countyId: "davidson", stateId: "tn",
  countyDisplayId: "019", countyNumber: 1, name: "Published cave", alternateNames: [], lengthFeet: null,
  depthFeet: null, maxPitDepthFeet: null, numberOfPits: null, narrative: null, reportedOn: null, isArchived: false,
  primaryEntrance: null, mapIds: [], entrances: [], geologyTagIds: [], files: [], reportedByNameTagIds: [],
  biologyTagIds: [], archeologyTagIds: [], cartographerNameTagIds: [], mapStatusTagIds: [], geologicAgeTagIds: [],
  physiographicProvinceTagIds: [], otherTagIds: [],
} as any;

const renderPage = () => {
  jest.spyOn(CaveService, "GetEditAuthoringContext").mockResolvedValue({
    cave, expectedRevisionId: "revision01", linePlots: [],
  } as any);
  const router = createMemoryRouter([
    { path: "/caves/:caveId/edit", element: <EditCavePage /> },
    { path: "/caves/:caveId", element: <div>Cave detail</div> },
    { path: "/other", element: <div>Other page</div> },
  ], { initialEntries: ["/caves/cave000001/edit"] });
  render(<RouterProvider router={router} />);
  return router;
};

afterEach(() => jest.restoreAllMocks());

it("blocks in-app navigation after a direct field edit", async () => {
  const router = renderPage();
  const name = await screen.findByRole("textbox", { name: "Name" });
  userEvent.clear(name);
  userEvent.type(name, "Unsaved direct edit");

  await act(async () => { await router.navigate("/other"); });

  expect(await screen.findByRole("dialog", { name: "Discard unsaved changes?" })).toBeInTheDocument();
  expect(screen.queryByText("Other page")).not.toBeInTheDocument();
  userEvent.click(screen.getByRole("button", { name: "Keep editing" }));
  expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue("Unsaved direct edit");
});

it("treats an asset-only authoring change as unsaved", async () => {
  const router = renderPage();
  await screen.findByRole("textbox", { name: "Name" });
  fireEvent.click(screen.getByRole("button", { name: "Simulate asset change" }));

  await act(async () => { await router.navigate("/other"); });

  expect(await screen.findByRole("dialog", { name: "Discard unsaved changes?" })).toBeInTheDocument();
  expect(screen.queryByText("Other page")).not.toBeInTheDocument();
});

it("clears the shared guard before navigating after a successful direct save", async () => {
  const update = jest.spyOn(CaveService, "UpdateCave").mockResolvedValue();
  renderPage();
  const name = await screen.findByRole("textbox", { name: "Name" });
  userEvent.clear(name);
  userEvent.type(name, "Saved direct edit");
  fireEvent.click(screen.getByRole("button", { name: "Save cave" }));

  await waitFor(() => expect(update).toHaveBeenCalledTimes(1));
  expect(await screen.findByText("Cave detail")).toBeInTheDocument();
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
});


it("submits preserved unmounted authoring values instead of only the currently rendered fields", async () => {
  const update = jest.spyOn(CaveService, "UpdateCave").mockResolvedValue();
  renderPage();

  fireEvent.click(await screen.findByRole("button", { name: "Save cave" }));

  await waitFor(() => expect(update).toHaveBeenCalledTimes(1));
  expect(update).toHaveBeenCalledWith(expect.objectContaining({
    id: "cave000001",
    expectedRevisionId: "revision01",
    name: "Published cave",
    depthFeet: null,
    biologyTagIds: ["biology-hidden"],
    files: [expect.objectContaining({ id: "file-hidden", name: "Existing map", fileTypeTagId: "file-type" })],
    linePlots: [expect.objectContaining({ id: "line-hidden", name: "Existing traverse" })],
  }));
});

it("keeps the complete dirty draft after a stale direct-save conflict", async () => {
  const update = jest.spyOn(CaveService, "UpdateCave").mockRejectedValue({
    statusCode: 409,
    conflictKind: "PublishedCaveChanged",
    message: "The Cave changed.",
  });
  renderPage();
  const name = await screen.findByRole("textbox", { name: "Name" });
  fireEvent.change(name, { target: { value: "My conflicted draft" } });

  fireEvent.click(screen.getByRole("button", { name: "Save cave" }));

  await waitFor(() => expect(update).toHaveBeenCalledTimes(1));
  expect(update).toHaveBeenCalledWith(expect.objectContaining({
    name: "My conflicted draft",
    biologyTagIds: ["biology-hidden"],
    files: [expect.objectContaining({ id: "file-hidden" })],
    linePlots: [expect.objectContaining({ id: "line-hidden" })],
  }));
  expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue("My conflicted draft");
  expect(screen.queryByText("Cave detail")).not.toBeInTheDocument();
});
