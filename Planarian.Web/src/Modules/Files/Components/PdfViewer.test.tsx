import { render, screen, waitFor } from "@testing-library/react";
import { PdfViewer } from "./PdfViewer";
import { PDF_STREAM_SESSION_QUERY_PARAMETER } from "./PdfViewerRequest";

const mockOpen = jest.fn().mockResolvedValue(undefined);
const mockSetViewerOptions = jest.fn().mockResolvedValue({ viewerOptions: {} });
const mockInjectViewerStyles = jest.fn().mockResolvedValue(undefined);

class MockPdfViewerElement extends HTMLElement {
  iframeWindow = new EventTarget();
  iframe = { contentWindow: this.iframeWindow };
  initPromise = Promise.resolve({ viewerApp: { open: mockOpen } });
  setViewerOptions = mockSetViewerOptions;
  injectViewerStyles = mockInjectViewerStyles;
}

if (!customElements.get("pdfjs-viewer-element")) {
  customElements.define("pdfjs-viewer-element", MockPdfViewerElement);
}

const fileUrl = "https://planarian.test/api/files/file/view?account_id=account";

describe("PdfViewer", () => {
  beforeEach(() => {
    mockOpen.mockReset().mockResolvedValue(undefined);
    mockSetViewerOptions.mockClear();
    mockInjectViewerStyles.mockClear();
  });

  afterEach(() => {
    jest.restoreAllMocks();
    document
      .querySelectorAll('script[data-pdfjs-viewer-element="true"]')
      .forEach((script) => script.remove());
  });

  it("opens the PDF with authenticated streaming and non-editable viewer options", async () => {
    render(<PdfViewer fileUrl={fileUrl} />);

    await waitFor(() => expect(mockOpen).toHaveBeenCalledTimes(1));

    const openOptions = mockOpen.mock.calls[0][0];
    const requestUrl = new URL(openOptions.url);
    expect(requestUrl.origin + requestUrl.pathname).toBe(
      "https://planarian.test/api/files/file/view"
    );
    expect(requestUrl.searchParams.get("account_id")).toBe("account");
    expect(
      requestUrl.searchParams.get(PDF_STREAM_SESSION_QUERY_PARAMETER)
    ).toMatch(/^[0-9a-f-]{36}$/i);
    expect(openOptions.withCredentials).toBe(true);
    expect(openOptions).not.toHaveProperty("httpHeaders");

    expect(mockSetViewerOptions).toHaveBeenCalledWith(
      expect.objectContaining({
        annotationMode: 1,
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

  it("requests close when Escape is pressed while focus is inside the PDF iframe", async () => {
    const onRequestClose = jest.fn();
    const { unmount } = render(
      <PdfViewer fileUrl={fileUrl} onRequestClose={onRequestClose} />
    );

    await waitFor(() => expect(mockOpen).toHaveBeenCalledTimes(1));
    const viewer = document.querySelector(
      "pdfjs-viewer-element"
    ) as MockPdfViewerElement;

    viewer.iframeWindow.dispatchEvent(
      new KeyboardEvent("keydown", { key: "ArrowLeft" })
    );
    expect(onRequestClose).not.toHaveBeenCalled();

    viewer.iframeWindow.dispatchEvent(
      new KeyboardEvent("keydown", { key: "Escape" })
    );
    expect(onRequestClose).toHaveBeenCalledTimes(1);

    unmount();
    viewer.iframeWindow.dispatchEvent(
      new KeyboardEvent("keydown", { key: "Escape" })
    );
    expect(onRequestClose).toHaveBeenCalledTimes(1);
  });

  it("recovers when the next PDF is selected after one document fails to open", async () => {
    mockOpen
      .mockRejectedValueOnce(new Error("bad PDF"))
      .mockResolvedValueOnce(undefined);
    const { rerender } = render(<PdfViewer fileUrl={`${fileUrl}&file=bad`} />);

    expect(
      await screen.findByText("Unable to render this PDF in the app.")
    ).toBeInTheDocument();

    rerender(<PdfViewer fileUrl={`${fileUrl}&file=good`} />);
    await waitFor(() => expect(mockOpen).toHaveBeenCalledTimes(2));

    const secondRequestUrl = new URL(mockOpen.mock.calls[1][0].url);
    expect(secondRequestUrl.searchParams.get("file")).toBe("good");
    await waitFor(() =>
      expect(
        screen.queryByText("Unable to render this PDF in the app.")
      ).not.toBeInTheDocument()
    );
  });

  it("retries loading the viewer module after a transient script failure", async () => {
    jest.spyOn(customElements, "get").mockReturnValue(undefined);
    jest
      .spyOn(customElements, "whenDefined")
      .mockImplementation(
        () => new Promise<CustomElementConstructor>(() => undefined)
      );

    const firstRender = render(<PdfViewer fileUrl={fileUrl} />);
    const firstScript = await waitFor(() => {
      const script = document.querySelector(
        'script[data-pdfjs-viewer-element="true"]'
      ) as HTMLScriptElement | null;
      expect(script).not.toBeNull();
      return script!;
    });

    firstScript.dispatchEvent(new Event("error"));
    expect(
      await screen.findByText("Unable to render this PDF in the app.")
    ).toBeInTheDocument();
    expect(firstScript.isConnected).toBe(false);
    firstRender.unmount();

    render(<PdfViewer fileUrl={fileUrl} />);
    const secondScript = await waitFor(() => {
      const script = document.querySelector(
        'script[data-pdfjs-viewer-element="true"]'
      ) as HTMLScriptElement | null;
      expect(script).not.toBeNull();
      return script!;
    });
    expect(secondScript).not.toBe(firstScript);

    secondScript.dispatchEvent(new Event("error"));
    expect(
      await screen.findByText("Unable to render this PDF in the app.")
    ).toBeInTheDocument();
  });
});
