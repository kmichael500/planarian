import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ReactNode } from "react";
import { FileAccessAction, FileService } from "../Services/FileService";
import { FileViewer } from "./FileViewerComponent";

jest.mock("./GpxViewer", () => ({ GpxViewer: () => null }));
jest.mock("./PltViewer", () => ({ PltViewer: () => null }));
jest.mock("./VectorDatasetViewer", () => ({ VectorDatasetViewer: () => null }));
jest.mock("./PdfViewer", () => ({ PdfViewer: () => null }));

jest.mock("../../../Shared/Components/Buttons/PlanarianModal", () => ({
  PlanarianModal: ({ open, header, children }: { open: boolean; header?: ReactNode; children?: ReactNode }) =>
    open ? <div role="dialog"><div>{header}</div>{children}</div> : null,
}));

jest.mock("../../../Shared/Components/Buttons/PlanarianButtton", () => ({
  PlanarianButton: ({ children, icon: _icon, alwaysShowChildren: _alwaysShowChildren, ...props }: any) =>
    <button {...props}>{children}</button>,
}));

jest.mock("../../../Shared/Components/Display/PlanarianTag", () => ({
  PlanarianTag: ({ children }: { children: ReactNode }) => <span>{children}</span>,
}));

afterEach(() => jest.restoreAllMocks());

it("uses request-scoped view and download overrides instead of published-file access", async () => {
  const getDefaultUrl = jest.spyOn(FileService, "getFileAccessUrl");
  const defaultDownload = jest.spyOn(FileService, "startFileDownload");
  const customDownload = jest.fn();

  render(<FileViewer fileId="staged-file" name="Survey.png" fileType="png" open
    viewUrl="/change-request/staged-file/view" onDownload={customDownload} />);

  await waitFor(() => expect(screen.getByAltText("file")).toHaveAttribute(
    "src", "/change-request/staged-file/view"));
  expect(getDefaultUrl).not.toHaveBeenCalled();

  await userEvent.click(screen.getByRole("button", { name: "Download" }));
  expect(customDownload).toHaveBeenCalledTimes(1);
  expect(defaultDownload).not.toHaveBeenCalled();
});

it("falls back to published-file view and download access when no overrides are supplied", async () => {
  const getDefaultUrl = jest.spyOn(FileService, "getFileAccessUrl").mockReturnValue("/api/files/file-1/view");
  const defaultDownload = jest.spyOn(FileService, "startFileDownload").mockImplementation(() => undefined);

  render(<FileViewer fileId="file-1" name="Photo.png" fileType="png" open />);

  await waitFor(() => expect(screen.getByAltText("file")).toHaveAttribute("src", "/api/files/file-1/view"));
  expect(getDefaultUrl).toHaveBeenCalledWith("file-1", FileAccessAction.View);

  await userEvent.click(screen.getByRole("button", { name: "Download" }));
  expect(defaultDownload).toHaveBeenCalledWith("file-1");
});
