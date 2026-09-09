import { render, waitFor } from "@testing-library/react";
import { PdfViewer } from "./PdfViewer";
import { PDF_STREAM_SESSION_HEADER } from "./PdfViewerRequest";

const mockOpen = jest.fn().mockResolvedValue(undefined);
const mockSetViewerOptions = jest.fn().mockResolvedValue({ viewerOptions: {} });

class MockPdfViewerElement extends HTMLElement {
  initPromise = Promise.resolve({ viewerApp: { open: mockOpen } });
  setViewerOptions = mockSetViewerOptions;

  async injectViewerStyles() {}
}

if (!customElements.get("pdfjs-viewer-element")) {
  customElements.define("pdfjs-viewer-element", MockPdfViewerElement);
}

describe("PdfViewer", () => {
  beforeEach(() => {
    mockOpen.mockClear();
    mockSetViewerOptions.mockClear();
  });

  it("opens the requested PDF through the configured read-only PDF.js viewer", async () => {
    render(
      <PdfViewer fileUrl="https://planarian.test/api/files/file/view?account_id=account" />
    );

    await waitFor(() => expect(mockOpen).toHaveBeenCalledTimes(1));

    expect(mockOpen).toHaveBeenCalledWith(
      expect.objectContaining({
        url: "https://planarian.test/api/files/file/view?account_id=account",
        withCredentials: true,
        httpHeaders: expect.objectContaining({
          [PDF_STREAM_SESSION_HEADER]: expect.stringMatching(/^[0-9a-f-]{36}$/i),
        }),
      })
    );

    expect(mockSetViewerOptions).toHaveBeenCalledWith(
      expect.objectContaining({
        annotationEditorMode: -1,
        enableAltText: false,
        enableComment: false,
        enableMerge: false,
        enableSignatureEditor: false,
        enableSplitMerge: false,
        supportsDownloading: false,
      })
    );

    const viewer = document.querySelector("pdfjs-viewer-element");
    expect(viewer).not.toBeNull();
    expect(viewer).toHaveAttribute("zoom", "page-width");
    expect(viewer).toHaveAttribute("pagemode", "none");
  });
});
