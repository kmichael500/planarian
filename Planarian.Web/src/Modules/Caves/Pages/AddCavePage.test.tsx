import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { CaveService } from "../Service/CaveService";
import { AddCavesPage } from "./AddCavePage";

jest.mock("../Components/AddCaveComponent", () => {
  const { Form, Input } = require("antd");
  return {
    AddCaveComponent: ({ form }: { form: any }) => (
      <>
        <Form.Item name="name" label="Name"><Input /></Form.Item>
        <button type="button" onClick={() => form.setFieldsValue({
          biologyTagIds: ["biology-a"],
          files: [{ id: "file-a", name: "Survey", extension: ".pdf", fileTypeTagId: "file-type-a", fileTypeKey: "Survey" }],
          linePlots: [{ id: "line-a", name: "Traverse", geoJson: "{\"type\":\"FeatureCollection\",\"features\":[]}" }],
        })}>Populate hidden authoring values</button>
        <button type="submit">Add cave</button>
      </>
    ),
  };
});

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

it("submits the complete new-Cave form store including authoring values outside rendered fields", async () => {
  const add = jest.spyOn(CaveService, "AddCave").mockResolvedValue("cave-created");
  const router = createMemoryRouter([
    { path: "/caves/new", element: <AddCavesPage /> },
    { path: "/caves", element: <div>Cave list</div> },
  ], { initialEntries: ["/caves/new"] });
  render(<RouterProvider router={router} />);

  await userEvent.type(screen.getByRole("textbox", { name: "Name" }), "Created Cave");
  fireEvent.click(screen.getByRole("button", { name: "Populate hidden authoring values" }));
  fireEvent.click(screen.getByRole("button", { name: "Add cave" }));

  await waitFor(() => expect(add).toHaveBeenCalledTimes(1));
  expect(add).toHaveBeenCalledWith(expect.objectContaining({
    name: "Created Cave",
    biologyTagIds: ["biology-a"],
    files: [expect.objectContaining({ id: "file-a", name: "Survey", fileTypeTagId: "file-type-a" })],
    linePlots: [expect.objectContaining({ id: "line-a", name: "Traverse" })],
  }));
  expect(await screen.findByText("Cave list")).toBeInTheDocument();
});
