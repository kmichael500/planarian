import { Tag, Spin, Result } from "antd";
import { useState, useEffect } from "react";
import {
  CloudDownloadOutlined,
  LeftOutlined,
  RightOutlined,
} from "@ant-design/icons";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
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

interface FileViewerProps {
  embedUrl: string | null | undefined;
  downloadUrl?: string | null | undefined;
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
  embedUrl,
  downloadUrl,
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
      {fileType && <Tag style={{ marginLeft: "0.5rem" }}>{fileType}</Tag>}
    </>
  );

  const downloadButton = downloadUrl ? (
    <PlanarianButton href={downloadUrl || ""} icon={<CloudDownloadOutlined />}>
      Download
    </PlanarianButton>
  ) : null;

  const [fileContent, setFileContent] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    if (!open) {
      return;
    }

    if (!embedUrl) {
      setFileContent(null);
      setIsLoading(false);
      return;
    }

    if (isTextFileType(fileType) || isCsvFileType(fileType)) {
      let isCancelled = false;
      setIsLoading(true);
      setFileContent(null);

      fetch(embedUrl)
        .then((response) => response.text())
        .then((data) => {
          if (isCancelled) {
            return;
          }

          if (isTextFileType(fileType) || isCsvFileType(fileType)) {
            setFileContent(data);
          }

          setIsLoading(false);
        })
        .catch(() => {
          if (!isCancelled) {
            setIsLoading(false);
          }
        });

      return () => {
        isCancelled = true;
      };
    }

    // For all other file types (including GPX) we do not fetch here.
    setFileContent(null);
    setIsLoading(false);
  }, [open, embedUrl, fileType]);

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
          {isSupported ? (
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
                    src={embedUrl || ""}
                    alt="file"
                    style={{ maxHeight: "100%" }}
                  />
                </div>
              )}
              {isPdf && !isNullOrWhiteSpace(embedUrl) && (
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
                    key={embedUrl || undefined}
                    src={`https://docs.google.com/gview?url=${encodeURIComponent(
                      embedUrl
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
                  style={{
                    overflow: "auto",
                    height: "50%",
                    padding: "1rem",
                    border: "1px solid rgb(240, 240, 240)",
                  }}
                >
                  {isLoading ? <Spin /> : fileContent}
                </pre>
              )}
              {isVectorDataset && embedUrl && (
                <VectorDatasetViewer
                  embedUrl={embedUrl}
                  fileType={fileType}
                  downloadButton={downloadButton}
                />
              )}
              {isPlt && embedUrl && (
                <PltViewer embedUrl={embedUrl} downloadButton={downloadButton} />
              )}
              {isGpx && embedUrl && (
                <GpxViewer embedUrl={embedUrl} downloadButton={downloadButton} />
              )}
            </>
          ) : (
            <Result
              status="warning"
              title={`The filetype '${fileType}' is not currently supported.`}
              extra={downloadButton}
            />
          )}
        </Spin>
      </PlanarianModal>
    </>
  );
};

export { FileViewer };
