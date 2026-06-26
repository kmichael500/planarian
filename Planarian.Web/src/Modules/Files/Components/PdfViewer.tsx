import { useEffect, useMemo, useRef, useState } from "react";
import { Document, Page, pdfjs } from "react-pdf";
import { Grid, Result, Space, Spin, Typography } from "antd";
import {
  MinusOutlined,
  EyeOutlined,
  PlusOutlined,
  VerticalAlignMiddleOutlined,
} from "@ant-design/icons";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import "react-pdf/dist/Page/AnnotationLayer.css";
import "react-pdf/dist/Page/TextLayer.css";
import "./PdfViewer.scss";

type PromiseWithResolversResult<T> = {
  promise: Promise<T>;
  resolve: (value: T | PromiseLike<T>) => void;
  reject: (reason?: unknown) => void;
};

type PromiseConstructorWithResolvers = PromiseConstructor & {
  withResolvers?: <T>() => PromiseWithResolversResult<T>;
};

const promiseWithResolversSupport =
  Promise as PromiseConstructorWithResolvers;

if (typeof promiseWithResolversSupport.withResolvers !== "function") {
  promiseWithResolversSupport.withResolvers = function withResolvers<T>() {
    let resolve!: (value: T | PromiseLike<T>) => void;
    let reject!: (reason?: unknown) => void;

    const promise = new Promise<T>((resolvePromise, rejectPromise) => {
      resolve = resolvePromise;
      reject = rejectPromise;
    });

    return { promise, resolve, reject };
  };
}

pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  "../../../../node_modules/react-pdf/node_modules/pdfjs-dist/build/pdf.worker.min.mjs",
  import.meta.url
).toString();

interface PdfViewerProps {
  file: Blob;
  openUrl?: string;
  downloadButton?: React.ReactNode;
}

type PdfDocumentLoadSuccess = {
  numPages: number;
};

const MIN_ZOOM = 0.75;
const MAX_ZOOM = 2.5;
const ZOOM_STEP = 0.25;

export function PdfViewer({
  file,
  openUrl,
  downloadButton,
}: PdfViewerProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [numPages, setNumPages] = useState(0);
  const [scale, setScale] = useState(1);
  const [fitToWidth, setFitToWidth] = useState(true);
  const [pageWidth, setPageWidth] = useState<number | undefined>(undefined);
  const [renderError, setRenderError] = useState(false);
  const [documentLoading, setDocumentLoading] = useState(true);

  useEffect(() => {
    setNumPages(0);
    setScale(1);
    setFitToWidth(true);
    setRenderError(false);
    setDocumentLoading(true);
  }, [file]);

  useEffect(() => {
    const element = containerRef.current;
    if (!element) {
      return;
    }

    const updateWidth = () => {
      const nextWidth = Math.max(element.clientWidth - 32, 200);
      setPageWidth(nextWidth);
    };

    updateWidth();

    if (typeof ResizeObserver === "undefined") {
      window.addEventListener("resize", updateWidth);
      return () => {
        window.removeEventListener("resize", updateWidth);
      };
    }

    const resizeObserver = new ResizeObserver(() => {
      updateWidth();
    });

    resizeObserver.observe(element);
    return () => {
      resizeObserver.disconnect();
    };
  }, []);

  const fallbackActions = useMemo(() => {
    const actions: React.ReactNode[] = [];

    if (openUrl) {
      actions.push(
        <PlanarianButton
          key="open"
          icon={<EyeOutlined />}
          onClick={() => {
            window.open(openUrl, "_blank", "noopener,noreferrer");
          }}
        >
          Open in browser
        </PlanarianButton>
      );
    }

    if (downloadButton) {
      actions.push(downloadButton);
    }

    return actions;
  }, [downloadButton, openUrl]);

  if (renderError) {
    return (
      <Result
        status="warning"
        title="Unable to render this PDF in the app."
        extra={<div className="pdf-viewer__fallback-actions">{fallbackActions}</div>}
      />
    );
  }

  return (
    <div className="pdf-viewer">
      <div className="pdf-viewer__toolbar">
        <div className="pdf-viewer__toolbar-group">
          <Typography.Text className="pdf-viewer__status">
            {numPages || 0} page{numPages === 1 ? "" : "s"}
          </Typography.Text>
        </div>
        <div className="pdf-viewer__toolbar-group">
          <PlanarianButton
            icon={<MinusOutlined />}
            onClick={() => {
              setFitToWidth(false);
              setScale((current) => Math.max(current - ZOOM_STEP, MIN_ZOOM));
            }}
          >
            Zoom out
          </PlanarianButton>
          <PlanarianButton
            icon={<VerticalAlignMiddleOutlined />}
            onClick={() => setFitToWidth(true)}
          >
            Fit width
          </PlanarianButton>
          <PlanarianButton
            icon={<PlusOutlined />}
            onClick={() => {
              setFitToWidth(false);
              setScale((current) => Math.min(current + ZOOM_STEP, MAX_ZOOM));
            }}
          >
            Zoom in
          </PlanarianButton>
          {downloadButton}
        </div>
      </div>

      <div
        ref={containerRef}
        className="pdf-viewer__canvas-shell pdf-viewer__canvas-shell--centered"
      >
        <Document
          file={file}
          loading={<Spin />}
          onLoadSuccess={({ numPages: nextNumPages }: PdfDocumentLoadSuccess) => {
            setNumPages(nextNumPages);
            setDocumentLoading(false);
            setRenderError(false);
          }}
          onLoadError={() => {
            setDocumentLoading(false);
            setRenderError(true);
          }}
          onSourceError={() => {
            setDocumentLoading(false);
            setRenderError(true);
          }}
        >
          {Array.from(new Array(numPages), (_, index) => (
            <Page
              key={`pdf-page-${index + 1}`}
              className="pdf-viewer__page"
              loading={
                <Space direction="vertical" align="center">
                  <Spin />
                  <Typography.Text>Rendering page...</Typography.Text>
                </Space>
              }
              onRenderError={() => {
                setDocumentLoading(false);
                setRenderError(true);
              }}
              onRenderSuccess={() => {
                setDocumentLoading(false);
              }}
              pageNumber={index + 1}
              renderAnnotationLayer
              renderTextLayer
              width={fitToWidth ? pageWidth : undefined}
              scale={fitToWidth ? undefined : scale}
            />
          ))}
        </Document>
      </div>

      {documentLoading && (
        <Typography.Text className="pdf-viewer__status">
          Loading PDF...
        </Typography.Text>
      )}
    </div>
  );
}
