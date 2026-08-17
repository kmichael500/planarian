import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { CaveService } from "../Service/CaveService";
import { SuggestCaveChangesPage } from "./SuggestCaveChangesPage";

jest.mock("../Components/AddCaveComponent", () => {
  const { Form, Input } = require("antd");
  return {
    AddCaveComponent: () => (
      <>
        <Form.Item name="name" label="Name">
          <Input />
        </Form.Item>
        <button type="submit">Review changes</button>
      </>
    ),
  };
});

jest.mock("../Components/CaveRevisionDiff", () => ({
  CaveRevisionDiff: () => <div data-testid="revision-diff" />,
}));

jest.mock("../../../Shared/Components/Buttons/PlanarianButtton", () => ({
  PlanarianButton: ({ children, loading: _loading, icon: _icon, danger: _danger, ...props }: any) =>
    <button {...props}>{children}</button>,
}));

jest.mock("../../../Shared/Components/Buttons/PlanarianModal", () => ({
  PlanarianModal: ({ open, header, footer, children }: any) =>
    open ? (
      <div role="dialog" aria-label={typeof header === "string" ? header : undefined}>
        {children}
        {footer}
      </div>
    ) : null,
}));

jest.mock("../Helpers/CaveFormMapper", () => ({
  caveToForm: () => ({ id: "cave000001", name: "Published cave" }),
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

const mockAuthoringContext = () =>
  jest.spyOn(CaveService, "GetProposalAuthoringContext").mockResolvedValue({
    cave: { id: "cave000001", name: "Published cave" } as any,
    expectedBaseRevisionId: "revision01",
    linePlots: [],
  });

const renderWithDataRouter = () => {
  const router = createMemoryRouter(
    [
      {
        path: "/caves/:caveId/suggest",
        element: <SuggestCaveChangesPage />,
      },
      { path: "/other", element: <div>Other page</div> },
      {
        path: "/caves/requests/:requestId",
        element: <div>Submitted request</div>,
      },
    ],
    { initialEntries: ["/caves/cave000001/suggest"] }
  );

  render(<RouterProvider router={router} />);
  return router;
};

afterEach(() => jest.restoreAllMocks());

it("prevents submitting a change request when the authoritative preview is semantically unchanged", async () => {
  mockAuthoringContext();
  jest.spyOn(CaveService, "PreviewChanges").mockResolvedValue({
    base: {} as any,
    proposed: {} as any,
    diff: {} as any,
    countyNumberIntent: "Manual",
    hasMeaningfulChanges: false,
  });
  const submit = jest
    .spyOn(CaveService, "SubmitChanges")
    .mockResolvedValue("request0001");

  renderWithDataRouter();

  fireEvent.click(await screen.findByRole("button", { name: "Review changes" }));

  expect(
    await screen.findByText(/make at least one meaningful change/i)
  ).toBeInTheDocument();
  const submitButton = screen.getByRole("button", { name: "Submit for review" });
  expect(submitButton).toBeDisabled();
  fireEvent.click(submitButton);
  await waitFor(() => expect(submit).not.toHaveBeenCalled());
});

it("blocks in-app navigation after the author changes a field and lets them keep editing", async () => {
  mockAuthoringContext();
  const router = renderWithDataRouter();

  const name = await screen.findByRole("textbox", { name: "Name" });
  userEvent.clear(name);
  userEvent.type(name, "Unsaved cave name");
  await act(async () => {
    await router.navigate("/other");
  });

  expect(
    await screen.findByRole("dialog", { name: "Discard unsaved changes?" })
  ).toBeInTheDocument();
  expect(screen.queryByText("Other page")).not.toBeInTheDocument();

  userEvent.click(screen.getByRole("button", { name: "Keep editing" }));
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue(
    "Unsaved cave name"
  );
});

it("allows the author to discard unsaved changes and continue the blocked navigation", async () => {
  mockAuthoringContext();
  const router = renderWithDataRouter();

  const name = await screen.findByRole("textbox", { name: "Name" });
  userEvent.clear(name);
  userEvent.type(name, "Unsaved cave name");
  await act(async () => {
    await router.navigate("/other");
  });

  await screen.findByRole("dialog", { name: "Discard unsaved changes?" });
  userEvent.click(screen.getByRole("button", { name: "Discard changes" }));

  expect(await screen.findByText("Other page")).toBeInTheDocument();
});

it("does not block navigation when the author has not changed the loaded form", async () => {
  mockAuthoringContext();
  const router = renderWithDataRouter();

  await screen.findByRole("textbox", { name: "Name" });
  await act(async () => {
    await router.navigate("/other");
  });

  expect(await screen.findByText("Other page")).toBeInTheDocument();
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
});

it("clears the dirty guard before navigating after a successful submission", async () => {
  mockAuthoringContext();
  jest.spyOn(CaveService, "PreviewChanges").mockResolvedValue({
    base: {} as any,
    proposed: {} as any,
    diff: {} as any,
    countyNumberIntent: "Manual",
    hasMeaningfulChanges: true,
  });
  const submit = jest
    .spyOn(CaveService, "SubmitChanges")
    .mockResolvedValue("request0001");
  renderWithDataRouter();

  const name = await screen.findByRole("textbox", { name: "Name" });
  userEvent.clear(name);
  userEvent.type(name, "A real change");
  fireEvent.click(screen.getByRole("button", { name: "Review changes" }));
  fireEvent.click(await screen.findByRole("button", { name: "Submit for review" }));

  expect(await screen.findByText("Submitted request")).toBeInTheDocument();
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  expect(submit).toHaveBeenCalledTimes(1);
});

it("only protects browser unload after the author makes a change", async () => {
  mockAuthoringContext();
  renderWithDataRouter();

  const name = await screen.findByRole("textbox", { name: "Name" });
  const pristineUnload = new Event("beforeunload", { cancelable: true });
  window.dispatchEvent(pristineUnload);
  expect(pristineUnload.defaultPrevented).toBe(false);

  userEvent.clear(name);
  userEvent.type(name, "Unsaved cave name");
  const dirtyUnload = new Event("beforeunload", { cancelable: true });
  window.dispatchEvent(dirtyUnload);
  expect(dirtyUnload.defaultPrevented).toBe(true);
});
