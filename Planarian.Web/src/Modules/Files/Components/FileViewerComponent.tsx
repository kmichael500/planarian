import { Spin, Result } from "antd";
import { useState, useEffect } from "react";
import {
  CloudDownloadOutlined,
  LeftOutlined,
  RightOutlined,
} from "@ant-design/icons";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { PlanarianTag } from "../../../Shared/Components/Display/PlanarianTag";
import "./FileViewerComponent.scss";
import {
  isImageFileType,
  isPdfFileType,
  isSupportedFileType,
  isTextFileType,
  isCsvFileType,
  isGpxFileType,
  isVectorDatasetFileType,
  isPltFileType,
} from "../Services/FileHelpers";
import { CSVDisplay } from "./CsvDisplayComponent";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { GpxViewer } from "./GpxViewer";
import { VectorDatasetViewer } from "./VectorDatasetViewer";
import { PltViewer } from "./PltViewer";
import { FileAccessAction, FileService } from "../Services/FileService";
import { PdfViewer } from "./PdfViewer";

interface FileViewerProps {
  fileId?: string | null;
  displayName?: string | null;
  fileType?: string | null;
  open: boolean;
  onCancel?: (() => void) | undefined;
  onNext?: () => void;
  onPrevious?: () => void;
  canGoNext?: boolean;
  canGoPrevious?: boolean;
}

const FileViewer: React.FC<FileViewerProps> = ({
  fileId,
  displayName,
  fileType,
  open,
  onCancel: onClose,
  onNext,
  onPrevious,
  canGoNext,
  canGoPrevious,
}) => {
  const isImage = isImageFileType(fileType);
  const isPdf = isPdfFileType(fileType);
  const isGpx = isGpxFileType(fileType);
  const isVectorDataset = isVectorDatasetFileType(fileType);
  const isPlt = isPltFileType(fileType);
  const isSupported = isSupportedFileType(fileType);
  const headerTitle = (
    <>
      {displayName}
      {fileType && <PlanarianTag style={{ marginLeft: "0.5rem" }}>{fileType}</PlanarianTag>}
    </>
  );

  const downloadButton = fileId ? (
    <PlanarianButton
      icon={<CloudDownloadOutlined />}
      onClick={async () => {
        FileService.startFileDownload(fileId);
      }}
    >
      Download
    </PlanarianButton>
  ) : null;

  const [fileContent, setFileContent] = useState<string | null>(null);
  const [pdfFile, setPdfFile] = useState<Blob | null>(null);
  const [fileEmbedUrl, setFileEmbedUrl] = useState<string | undefined>(undefined);
  const [fileAccessError, setFileAccessError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    if (!open) {
      setPdfFile(null);
      setFileContent(null);
      setFileEmbedUrl(undefined);
      setFileAccessError(null);
      setIsLoading(false);
      return;
    }

    if (!fileId) {
      setPdfFile(null);
      setFileEmbedUrl(undefined);
      setFileAccessError(null);
      setFileContent(null);
      setIsLoading(false);
      return;
    }

    setFileContent(null);
    setPdfFile(null);
    setFileEmbedUrl(undefined);
    setFileAccessError(null);
    setIsLoading(true);

    let isCancelled = false;
    const loadFileAccessUrl = async () => {
      try {
        const accessUrl = FileService.getFileAccessUrl(fileId, FileAccessAction.View);
        if (isCancelled) {
          return;
        }

        setFileEmbedUrl(accessUrl);
        if (!isTextFileType(fileType) && !isCsvFileType(fileType) && !isPdf) {
          setIsLoading(false);
        }
      } catch {
        if (isCancelled) {
          return;
        }

        setFileAccessError("Unable to load file.");
        setIsLoading(false);
      }
    };

    loadFileAccessUrl();

    return () => {
      isCancelled = true;
    };
  }, [open, fileId, fileType, isPdf]);

  useEffect(() => {
    if (!open || !fileEmbedUrl) {
      return;
    }

    if (fileAccessError) {
      return;
    }

    if (isTextFileType(fileType) || isCsvFileType(fileType)) {
      let isCancelled = false;
      setIsLoading(true);
      setFileContent(null);

      const loadFileContent = async () => {
        try {
          const response = await fetch(fileEmbedUrl, { credentials: "include" });
          if (!response.ok) {
            throw new Error("Unable to load file.");
          }

          const data = await response.text();
          if (isCancelled) {
            return;
          }

          if (isTextFileType(fileType) || isCsvFileType(fileType)) {
            setFileContent(data);
          }

          setIsLoading(false);
        } catch {
          if (!isCancelled) {
            setFileAccessError("Unable to load file.");
            setIsLoading(false);
          }
        }
      };

      loadFileContent();

      return () => {
        isCancelled = true;
      };
    }
  }, [open, fileEmbedUrl, fileType, fileAccessError]);

  useEffect(() => {
    if (!open || !fileId || !isPdf) {
      return;
    }

    let isCancelled = false;
    setIsLoading(true);
    setPdfFile(null);

    const loadPdf = async () => {
      try {
        const pdfBlob = await FileService.getFileBlob(fileId);
        if (isCancelled) {
          return;
        }

        setPdfFile(pdfBlob);
        setIsLoading(false);
      } catch {
        if (!isCancelled) {
          setFileAccessError("Unable to load file.");
          setIsLoading(false);
        }
      }
    };

    loadPdf();

    return () => {
      isCancelled = true;
    };
  }, [open, fileId, isPdf]);

  const hasPrevious = typeof onPrevious === "function";
  const hasNext = typeof onNext === "function";
  const previousDisabled = hasPrevious && canGoPrevious === false;
  const nextDisabled = hasNext && canGoNext === false;

  useEffect(() => {
    if (!open) {
      return;
    }

    const handleKeyDown = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null;
      if (target) {
        const tagName = target.tagName.toLowerCase();
        if (
          tagName === "input" ||
          tagName === "textarea" ||
          target.isContentEditable
        ) {
          return;
        }
      }

      if (event.key === "ArrowLeft" && hasPrevious && !previousDisabled) {
        event.preventDefault();
        onPrevious?.();
      }

      if (event.key === "ArrowRight" && hasNext && !nextDisabled) {
        event.preventDefault();
        onNext?.();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => {
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [open, hasPrevious, previousDisabled, hasNext, nextDisabled, onPrevious, onNext]);

  const actionButtons = [
    hasPrevious ? (
      <PlanarianButton
        key="previous"
        icon={<LeftOutlined />}
        onClick={onPrevious}
        disabled={previousDisabled}
        aria-label="Previous file"
      />
    ) : null,
    hasNext ? (
      <PlanarianButton
        key="next"
        icon={<RightOutlined />}
        onClick={onNext}
        disabled={nextDisabled}
        aria-label="Next file"
      />
    ) : null,
    downloadButton,
  ].filter(Boolean);

  const headerItems = [headerTitle, ...actionButtons];

  return (
    <>
      <PlanarianModal
        open={open}
        fullScreen
        header={headerItems}
        onClose={() => {
          onClose?.();
        }}
      >
        <Spin spinning={isLoading}>
          {fileAccessError && !isLoading ? (
            <Result status="warning" title={fileAccessError} extra={downloadButton} />
          ) : !fileAccessError && isSupported ? (
            <>
              {isImage && (
                <div
                  style={{
                    display: "flex",
                    justifyContent: "center",
                    alignItems: "center",
                    height: "100%",
                  }}
                >
                  <img
                    src={fileEmbedUrl}
                    alt="file"
                    style={{ maxHeight: "100%" }}
                  />
                </div>
              )}
              {isPdf && pdfFile && (
                <PdfViewer
                  file={pdfFile}
                  openUrl={fileEmbedUrl}
                  downloadButton={downloadButton}
                />
              )}
              {isCsvFileType(fileType) && <CSVDisplay data={fileContent} />}
              {isTextFileType(fileType) && (
                <pre
                  className="file-viewer-text-preview"
                  style={{
                    overflow: "auto",
                    height: "50%",
                    padding: "1rem",
                  }}
                >
                  {isLoading ? <Spin /> : fileContent}
                </pre>
              )}
              {isVectorDataset && fileEmbedUrl && (
                <VectorDatasetViewer
                  embedUrl={fileEmbedUrl}
                  fileType={fileType}
                  downloadButton={downloadButton}
                />
              )}
              {isPlt && fileEmbedUrl && (
                <PltViewer embedUrl={fileEmbedUrl} downloadButton={downloadButton} />
              )}
              {isGpx && fileEmbedUrl && (
                <GpxViewer embedUrl={fileEmbedUrl} downloadButton={downloadButton} />
              )}
            </>
          ) : !fileAccessError ? (
            <Result
              status="warning"
              title={`The filetype '${fileType}' is not currently supported.`}
              extra={downloadButton}
            />
          ) : null}
        </Spin>
      </PlanarianModal>
    </>
  );
};

export { FileViewer };
