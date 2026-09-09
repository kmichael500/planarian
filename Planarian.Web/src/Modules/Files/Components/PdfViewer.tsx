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
}

const pdfAssetUrl = (path: string) =>
  `${process.env.PUBLIC_URL ?? ""}/pdfjs/${path}`;

const PDF_VIEWER_ELEMENT_TAG = "pdfjs-viewer-element";
const PDF_VIEWER_READ_ONLY_OPTIONS = {
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
`;

let pdfViewerElementLoadPromise: Promise<void> | null = null;

const loadPdfViewerElement = () => {
  if (customElements.get(PDF_VIEWER_ELEMENT_TAG)) {
    return Promise.resolve();
  }

  pdfViewerElementLoadPromise ??= new Promise<void>((resolve, reject) => {
    const script = document.createElement("script");
    script.type = "module";
    script.src = pdfAssetUrl("viewer/pdfjs-viewer-element.js");
    script.dataset.pdfjsViewerElement = "true";
    script.addEventListener("error", () => reject(new Error("Unable to load PDF.js viewer.")), {
      once: true,
    });
    document.head.appendChild(script);
    void customElements.whenDefined(PDF_VIEWER_ELEMENT_TAG).then(() => resolve());
  });

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

export function PdfViewer({ fileUrl }: PdfViewerProps) {
  const hostRef = useRef<HTMLDivElement | null>(null);
  const [initializationError, setInitializationError] = useState(false);
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

      await viewerApp.open(documentOptions);
    };

    void initialize().catch(() => {
      if (!cancelled) {
        setInitializationError(true);
      }
    });

    return () => {
      cancelled = true;
      viewer?.remove();
      host.replaceChildren();
    };
  }, [documentOptions]);

  if (initializationError) {
    return <Result status="warning" title="Unable to render this PDF in the app." />;
  }

  return <div ref={hostRef} className="pdf-viewer" aria-label="PDF viewer" />;
}
