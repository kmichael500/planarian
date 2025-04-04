import { Tag, Spin, Result, Drawer } from "antd";
import Papa from "papaparse";
import { useState, useEffect } from "react";
import { CloudDownloadOutlined } from "@ant-design/icons";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import "./FileViewerComponent.scss";
import {
  isImageFileType,
  isPdfFileType,
  isSupportedFileType,
  isTextFileType,
  isCsvFileType,
} from "../Services/FileHelpers";
import { CSVDisplay } from "./CsvDisplayComponent";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";

interface FileViewerProps {
  embedUrl: string | null | undefined;
  downloadUrl?: string | null | undefined;
  displayName?: string | null;
  fileType?: string | null;
  open: boolean;
  onCancel?: (() => void) | undefined;
}

type TableRow = {
  [key: string]: string;
};
const FileViewer: React.FC<FileViewerProps> = ({
  embedUrl,
  downloadUrl,
  displayName,
  fileType,
  open,
  onCancel: onClose,
}) => {
  const isImage = isImageFileType(fileType);
  const isPdf = isPdfFileType(fileType);
  const isSupported = isSupportedFileType(fileType);

  const downloadButton = downloadUrl ? (
    <PlanarianButton href={downloadUrl || ""} icon={<CloudDownloadOutlined />}>
      Download
    </PlanarianButton>
  ) : null;

  const [fileContent, setFileContent] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    console.log(embedUrl);

    if (
      open &&
      (isTextFileType(fileType) || isCsvFileType(fileType)) &&
      embedUrl
    ) {
      setIsLoading(true);
      fetch(embedUrl)
        .then((response) => response.text())
        .then((data) => {
          if (isTextFileType(fileType)) {
            setFileContent(data);
          } else if (isCsvFileType(fileType)) {
            setFileContent(data);
          }
          setIsLoading(false);
        })
        .catch(() => setIsLoading(false));
    }
  }, [open]);

  return (
    <>
      <PlanarianModal
        // fullScreen
        open={open}
        fullScreen
        header={[
          <>
            {displayName} <Tag>{fileType}</Tag>
          </>,
          downloadButton,
        ]}
        onClose={() => {
          {
            if (onClose) {
              onClose();
            }
          }
        }}
      >
        <Spin spinning={isLoading}>
          {isSupported && (
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
                    WebkitOverflowScrolling: "touch", // Enables momentum scrolling on iOS
                  }}
                >
                  <iframe
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
              )}{" "}
            </>
          )}

          {!isSupported && (
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
