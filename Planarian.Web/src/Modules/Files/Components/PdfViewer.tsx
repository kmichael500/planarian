import { useEffect, useMemo, useRef, useState } from "react";
import { Result, Space, Spin, Typography } from "antd";
import {
  MinusOutlined,
  EyeOutlined,
  PlusOutlined,
  VerticalAlignMiddleOutlined,
} from "@ant-design/icons";
import { Document, Page, pdfjs } from "react-pdf";
import "react-pdf/dist/Page/AnnotationLayer.css";
import "react-pdf/dist/Page/TextLayer.css";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { createPdfDocumentOptions } from "./PdfViewerRequest";
import "./PdfViewer.scss";

interface PdfViewerProps {
  fileUrl: string;
  downloadButton?: React.ReactNode;
}

type PdfDocumentLoadSuccess = {
  numPages: number;
};

const MIN_ZOOM = 0.75;
const MAX_ZOOM = 2.5;
const ZOOM_STEP = 0.25;

pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  "../../../../node_modules/react-pdf/node_modules/pdfjs-dist/build/pdf.worker.min.mjs",
  import.meta.url
).toString();

export function PdfViewer({
  fileUrl,
  downloadButton,
}: PdfViewerProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const documentOptions = useMemo(
    () => createPdfDocumentOptions(crypto.randomUUID()),
    [fileUrl]
  );
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
  }, [fileUrl]);

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

    if (fileUrl) {
      actions.push(
        <PlanarianButton
          key="open"
          icon={<EyeOutlined />}
          onClick={() => {
            window.open(fileUrl, "_blank", "noopener,noreferrer");
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
  }, [downloadButton, fileUrl]);

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
          file={fileUrl}
          options={documentOptions}
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
