import { List, Tag, Typography } from "antd";
import { FileVm } from "../Models/FileVm";
import { useState } from "react";
import { getFileType } from "../Services/FileHelpers";
import { FileViewer } from "./FileViewerComponent";

const FileListItemComponent = ({
  file,
  isNew,
  isRemoved,
}: {
  file: FileVm;
  isNew?: boolean;
  isRemoved?: boolean;
}) => {
  const [open, setOpen] = useState(false);
  // Allow opening viewer even if removed, as per new requirement
  const onFileClick = () => setOpen(true);
  const fileType = getFileType(file.fileName);

  const textStyle: React.CSSProperties = {};
  if (isRemoved) {
    textStyle.textDecoration = "line-through";
    textStyle.color = "rgba(0, 0, 0, 0.45)";
  }

  return (
    <>
      <List.Item
        actions={[
          <Typography.Link onClick={onFileClick}>View</Typography.Link>,
          <Typography.Link
            href={file.downloadUrl}
            target="_blank"
            rel="noopener noreferrer"
          >
            Download
          </Typography.Link>,
          // If you want a specific text for removed items in actions, you can add it here conditionally
          // For example: isRemoved && <Typography.Text disabled> (Original File)</Typography.Text>
        ]}
      >
        <Typography.Text style={textStyle}>
          <Tag>{fileType}</Tag>
          {file.displayName}
          {isNew && !isRemoved && ( // Show "New" tag only if it's new AND not also marked as removed (edge case)
            <Tag color="success" style={{ marginLeft: 8 }}>
              New
            </Tag>
          )}
          {isRemoved && (
            <Tag color="error" style={{ marginLeft: 8 }}>
              Removed
            </Tag>
          )}
        </Typography.Text>
      </List.Item>
      {/* Allow FileViewer for removed files as well, as they can still be "viewed" */}
      {file.embedUrl && (
        <FileViewer
          open={open}
          onCancel={() => setOpen(false)}
          embedUrl={file.embedUrl}
          downloadUrl={file.downloadUrl}
          displayName={file.displayName}
          fileType={fileType}
        />
      )}
    </>
  );
};

export { FileListItemComponent };
