import { useEffect, useMemo, useRef, useState } from "react";
import { Result } from "antd";
import type { PdfjsViewerElement } from "pdfjs-viewer-element";
import {
  createPdfDocumentRequestOptions,
  createPdfStreamSessionId,
} from "./PdfViewerRequest";
import "./PdfViewer.scss";

interface PdfViewerProps {
  fileUrl: string;
  onRequestClose?: () => void;
}

const pdfAssetUrl = (path: string) =>
  `${process.env.PUBLIC_URL ?? ""}/pdfjs/${path}`;

const PDF_VIEWER_ELEMENT_TAG = "pdfjs-viewer-element";
const PDF_VIEWER_READ_ONLY_OPTIONS = {
  // Render annotations, but keep interactive form controls non-editable.
  annotationMode: 1,
  annotationEditorMode: -1,
  disableHistory: true,
  enableAltText: false,
  enableAltTextModelDownload: false,
  enableComment: false,
  enableGuessAltText: false,
  enableHighlightFloatingButton: false,
  enableMerge: false,
  enableNewAltTextWhenAddingImage: false,
  enableSignatureEditor: false,
  enableSplitMerge: false,
  historyUpdateUrl: false,
  supportsDownloading: false,
};

const PDF_VIEWER_READ_ONLY_STYLES = `
  #editorModeButtons,
  #editorModeSeparator,
  #downloadButton,
  #secondaryDownload,
  #downloadFromUrl,
  #openFile,
  #secondaryOpenFile,
  #presentationMode,
  #viewBookmark,
  #viewBookmarkSeparator,
  #scrollPage,
  #scrollVertical,
  #scrollHorizontal,
  #scrollWrapped,
  #spreadNone,
  #spreadOdd,
  #spreadEven,
  #imageAltTextSettings,
  #imageAltTextSettingsSeparator,
  #documentProperties,
  #attachmentsViewMenu,
  #attachmentsView,
  #viewsManagerAddFileButton,
  #viewsManagerStatus,
  #editorCommentsSidebar,
  #editorUndoBar {
    display: none !important;
  }

  #viewerContainer {
    /*
     * PDF.js handles pinch zoom itself. Keep single-finger panning native, but
     * prevent the browser from starting a competing viewport pinch gesture.
     */
    touch-action: pan-x pan-y;
  }
`;

let pdfViewerElementLoadPromise: Promise<void> | null = null;

const loadPdfViewerElement = () => {
  if (customElements.get(PDF_VIEWER_ELEMENT_TAG)) {
    return Promise.resolve();
  }

  if (!pdfViewerElementLoadPromise) {
    const loadPromise = new Promise<void>((resolve, reject) => {
      const script = document.createElement("script");
      script.type = "module";
      script.src = pdfAssetUrl("viewer/pdfjs-viewer-element.js");
      script.dataset.pdfjsViewerElement = "true";
      script.addEventListener(
        "error",
        () => {
          script.remove();
          reject(new Error("Unable to load PDF.js viewer."));
        },
        { once: true }
      );
      document.head.appendChild(script);
      void customElements
        .whenDefined(PDF_VIEWER_ELEMENT_TAG)
        .then(() => resolve());
    });

    pdfViewerElementLoadPromise = loadPromise;
    void loadPromise.catch(() => {
      if (pdfViewerElementLoadPromise === loadPromise) {
        pdfViewerElementLoadPromise = null;
      }
    });
  }

  return pdfViewerElementLoadPromise;
};

const configureViewer = (viewer: PdfjsViewerElement) => {
  viewer.className = "pdf-viewer__element";
  viewer.setAttribute("iframe-title", "PDF document viewer");
  viewer.setAttribute("zoom", "page-width");
  viewer.setAttribute("pagemode", "none");
  viewer.setAttribute("c-map-url", pdfAssetUrl("cmaps/"));
  viewer.setAttribute("icc-url", pdfAssetUrl("iccs/"));
  viewer.setAttribute("image-resources-path", pdfAssetUrl("viewer/images/"));
  viewer.setAttribute("sandbox-bundle-src", pdfAssetUrl("pdf.sandbox.mjs"));
  viewer.setAttribute("standard-font-data-url", pdfAssetUrl("standard_fonts/"));
  viewer.setAttribute("wasm-url", pdfAssetUrl("wasm/"));
};

export function PdfViewer({ fileUrl, onRequestClose }: PdfViewerProps) {
  const hostRef = useRef<HTMLDivElement | null>(null);
  const onRequestCloseRef = useRef(onRequestClose);
  const [initializationError, setInitializationError] = useState(false);

  useEffect(() => {
    onRequestCloseRef.current = onRequestClose;
  }, [onRequestClose]);
  const documentOptions = useMemo(
    () => createPdfDocumentRequestOptions(fileUrl, createPdfStreamSessionId()),
    [fileUrl]
  );

  useEffect(() => {
    const host = hostRef.current;
    if (!host) {
      return;
    }

    let cancelled = false;
    let viewer: PdfjsViewerElement | null = null;
    let viewerWindow: Window | null = null;
    const handleViewerKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "Escape") {
        return;
      }

      event.preventDefault();
      onRequestCloseRef.current?.();
    };

    setInitializationError(false);

    const initialize = async () => {
      await loadPdfViewerElement();
      if (cancelled) {
        return;
      }
      viewer = document.createElement("pdfjs-viewer-element") as PdfjsViewerElement;
      configureViewer(viewer);
      host.replaceChildren(viewer);

      const optionsPromise = viewer.setViewerOptions(PDF_VIEWER_READ_ONLY_OPTIONS);
      const stylesPromise = viewer.injectViewerStyles(PDF_VIEWER_READ_ONLY_STYLES);
      const { viewerApp } = await viewer.initPromise;
      await Promise.all([optionsPromise, stylesPromise]);
      if (cancelled || !viewerApp) {
        return;
      }

      viewerWindow = viewer.iframe?.contentWindow ?? null;
      viewerWindow?.addEventListener("keydown", handleViewerKeyDown, true);
      await viewerApp.open(documentOptions);
    };

    void initialize().catch(() => {
      if (!cancelled) {
        setInitializationError(true);
      }
    });

    return () => {
      cancelled = true;
      viewerWindow?.removeEventListener("keydown", handleViewerKeyDown, true);
      viewer?.remove();
      host.replaceChildren();
    };
  }, [documentOptions]);

  return (
    <div className="pdf-viewer" aria-label="PDF viewer">
      <div
        ref={hostRef}
        className={`pdf-viewer__host${
          initializationError ? " pdf-viewer__host--hidden" : ""
        }`}
      />
      {initializationError && (
        <Result
          status="warning"
          title="Unable to render this PDF in the app."
        />
      )}
    </div>
  );
}
