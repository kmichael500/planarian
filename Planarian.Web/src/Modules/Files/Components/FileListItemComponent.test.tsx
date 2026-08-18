import { render, screen } from "@testing-library/react";
import { FileTypeKey } from "../Models/FileTypeKey";
import { FileListItemComponent } from "./FileListItemComponent";

jest.mock("../../../Shared/Components/Display/PlanarianTag", () => ({
  PlanarianTag: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
}));

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
