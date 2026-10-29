import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { FileTypeKey } from "../Models/FileTypeKey";
import { FileListItemComponent } from "./FileListItemComponent";

jest.mock("../../../Shared/Components/Display/PlanarianTag", () => ({
  PlanarianTag: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
}));

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }),
  });
});

it("renders the editable name and derives the file-type label from the extension", () => {
  render(<FileListItemComponent
    file={{
      id: "file-1",
      name: "Entrance Survey.final",
      extension: ".PDF",
      fileTypeTagId: "map",
      fileTypeKey: FileTypeKey.Map,
    }}
    onView={() => undefined}
  />);

  expect(screen.getByText("Entrance Survey.final")).toBeInTheDocument();
  expect(screen.getByText("PDF")).toBeInTheDocument();
});

it("exposes file actions as keyboard-accessible buttons", async () => {
  const onView = jest.fn();
  const onDownload = jest.fn();
  render(<FileListItemComponent
    file={{ id: "file-1", name: "survey.pdf", extension: ".pdf" }}
    onView={onView}
    onDownload={onDownload}
  />);

  const view = screen.getByRole("button", { name: "View survey.pdf" });
  const download = screen.getByRole("button", { name: "Download survey.pdf" });

  view.focus();
  await userEvent.keyboard("{Enter}");
  download.focus();
  await userEvent.keyboard("{Enter}");

  expect(onView).toHaveBeenCalledTimes(1);
  expect(onDownload).toHaveBeenCalledTimes(1);
});
