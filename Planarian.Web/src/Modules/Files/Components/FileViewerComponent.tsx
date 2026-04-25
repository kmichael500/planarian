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
  const [fileEmbedUrl, setFileEmbedUrl] = useState<string | undefined>(undefined);
  const [fileAccessError, setFileAccessError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    if (!open) {
      setFileEmbedUrl(undefined);
      setFileAccessError(null);
      return;
    }

    if (!fileId) {
      setFileEmbedUrl(undefined);
      setFileAccessError(null);
      setFileContent(null);
      setIsLoading(false);
      return;
    }

    setFileContent(null);
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
        if (!isTextFileType(fileType) && !isCsvFileType(fileType)) {
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
  }, [open, fileId, fileType]);

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
              {isPdf && !isNullOrWhiteSpace(fileEmbedUrl) && (
                <div
                  style={{
                    width: "100%",
                    height: "100%",
                    overflow: "hidden",
                    position: "relative",
                    WebkitOverflowScrolling: "touch",
                  }}
                >
                  <iframe
                    key={fileEmbedUrl || undefined}
                    src={`https://docs.google.com/gview?url=${encodeURIComponent(
                      fileEmbedUrl
                    )}&embedded=true`}
                    style={{
                      width: "100%",
                      height: "100%",
                      border: "none",
                      position: "absolute",
                      top: 0,
                      left: 0,
                      overflow: "auto",
                    }}
                    title="PDF Viewer"
                  />
                </div>
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
