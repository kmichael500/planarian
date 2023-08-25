import { Modal, Tag, Table, Spin, Result } from "antd";
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
      <Modal open={open} onCancel={onCancel} footer={null}>
        <h2>
          {displayName} <Tag>{fileType}</Tag>
        </h2>
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
            {isCsvFileType(fileType) && (
              <Table
                dataSource={tableData}
                columns={
                  tableData.length > 0
                    ? Object.keys(tableData[0]).map((key) => ({
                        title: key,
                        dataIndex: key,
                      }))
                    : []
                }
              />
            )}
            {isTextFileType(fileType) && (
              <pre
                style={{
                  overflow: "auto",
                  height: "100%",
                  padding: "1rem",
                  border: "1px solid rgb(240, 240, 240)",
                }}
              >
                {isLoading ? <Spin /> : fileContent}
              </pre>
            )}{" "}
            {downloadButton}
          </>
        )}

        {!isSupported && (
          <Result
            status="warning"
            title={`The filetype '${fileType}' is not currently supported.`}
            extra={downloadButton}
          />
        )}
      </Modal>
    </>
  );
};

export { FileViewer };
