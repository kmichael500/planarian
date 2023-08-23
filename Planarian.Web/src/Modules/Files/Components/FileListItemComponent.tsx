import { List, Modal, Result, Spin, Tag, Typography } from "antd";
import { FileVm } from "../Models/FileVm";
import "./FileListItemComponent.scss";
import { useEffect, useState } from "react";
import {
  getFileType,
  isImageFileType,
  isPdfFileType,
  isSupportedFileType,
  isTextFileType,
} from "../Services/FileHelpers";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";

import { CloudDownloadOutlined } from "@ant-design/icons";

const FileListItemComponent = ({ file }: { file: FileVm }) => {
  const [open, setOpen] = useState(false);
  const onFileClick = () => {
    setOpen(true);
  };

  const fileType = getFileType(file.fileName);

  return (
    <>
      <List.Item
        actions={[
          <Typography.Link onClick={onFileClick}>View</Typography.Link>,
          <Typography.Link href={file.downloadUrl}>Download</Typography.Link>,
        ]}
      >
        <Typography.Text>
          <Tag>{fileType}</Tag>
          {file.displayName}
        </Typography.Text>
      </List.Item>
      <FileViewer
        open={open}
        onCancel={() => {
          setOpen(false);
        }}
        embedUrl={file.embedUrl}
        downloadUrl={file.downloadUrl}
        displayName={file.displayName}
        fileType={fileType}
      />
    </>
  );
};
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

export const FileViewer: React.FC<FileViewerProps> = ({
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
  useEffect(() => {
    if (isTextFileType(fileType) && embedUrl) {
      setIsLoading(true);
      fetch(embedUrl)
        .then((response) => response.text())
        .then((data) => {
          setFileContent(data);
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

export { FileListItemComponent };
