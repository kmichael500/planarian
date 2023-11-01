import { Modal, Tag, Table, Spin, Result, Drawer, Button } from "antd";
import Papa from "papaparse";
import { useState, useEffect, useRef } from "react";
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

interface FileViewerProps {
  embedUrl: string | null | undefined;
  downloadUrl?: string | null | undefined;
  displayName?: string | null;
  fileType?: string | null;
  open: boolean;
  onCancel?:
    | ((e: React.MouseEvent<HTMLElement, MouseEvent>) => void)
    | undefined;
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
  onCancel,
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

  const [tableData, setTableData] = useState<TableRow[]>([]);

  useEffect(() => {
    if ((isTextFileType(fileType) || isCsvFileType(fileType)) && embedUrl) {
      setIsLoading(true);
      fetch(embedUrl)
        .then((response) => response.text())
        .then((data) => {
          if (isTextFileType(fileType)) {
            setFileContent(data);
          } else if (isCsvFileType(fileType)) {
            const parsedData = Papa.parse<TableRow>(data, {
              header: true,
            }).data;
            setTableData(parsedData);
          }
          setIsLoading(false);
        })
        .catch(() => setIsLoading(false));
    }
  }, [embedUrl, fileType]);

  return (
    <>
      <Drawer
        width="100VW"
        height="100VH"
        open={open}
        autoFocus={true} // add autoFocus prop
        extra={[<>{downloadButton}</>]}
        title={
          <>
            {displayName} <Tag>{fileType}</Tag>
          </>
        }
        onClose={(e) => {
          {
            if (onCancel) {
              onCancel(e as any);
            }
          }
        }}
        footer={null}
      >
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
            {isPdf && (
              <embed
                src={`${embedUrl ?? undefined}#toolbar=0`}
                type="application/pdf"
                width="100%"
                height="100%"
              />
            )}
            {isCsvFileType(fileType) && <CSVDisplay data={fileContent || ""} />}
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
      </Drawer>
    </>
  );
};

export { FileViewer };
