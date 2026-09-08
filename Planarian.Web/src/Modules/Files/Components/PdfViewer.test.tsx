import { render, waitFor } from "@testing-library/react";
import { PdfViewer } from "./PdfViewer";
import { PDF_STREAM_SESSION_HEADER } from "./PdfViewerRequest";

const mockOpen = jest.fn().mockResolvedValue(undefined);
class MockPdfViewerElement extends HTMLElement {
  iframe = document.createElement("iframe");
  initPromise = Promise.resolve({
    viewerApp: {
      initializedPromise: Promise.resolve(),
      initialized: true,
      eventBus: {},
      open: mockOpen,
    },
  });
}

if (!customElements.get("pdfjs-viewer-element")) {
  customElements.define("pdfjs-viewer-element", MockPdfViewerElement);
}

describe("PdfViewer", () => {
  beforeEach(() => {
    mockOpen.mockClear();
  });

  it("delegates rendering and interactions to the full PDF.js viewer", async () => {
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

    const viewer = document.querySelector("pdfjs-viewer-element");
    expect(viewer).not.toBeNull();
    expect(viewer).toHaveAttribute("zoom", "page-width");
    expect(viewer).toHaveAttribute("pagemode", "none");
  });
});
