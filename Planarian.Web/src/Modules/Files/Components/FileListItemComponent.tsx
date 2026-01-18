// src/modules/Files/Components/FileListItemComponent.tsx
import React from "react";
import { List, Tag, Typography, Tooltip } from "antd";
import { FileVm } from "../Models/FileVm";
import { getFileType } from "../Services/FileHelpers";

interface FileListItemComponentProps {
  file: FileVm;
  onView: (file: FileVm) => void;
  isNew?: boolean;
  isRemoved?: boolean;
  isRenamed?: boolean;
  isTagChanged?: boolean;
  originalDisplayName?: string | null;
  originalTagValue?: string | null;
}

const FileListItemComponent = ({
  file,
  onView,
  isNew,
  isRemoved,
  isRenamed,
  isTagChanged,
  originalDisplayName,
  originalTagValue,
}: FileListItemComponentProps) => {
  const fileType = getFileType(file.fileName);

  const textStyle: React.CSSProperties = {};
  if (isRemoved) {
    textStyle.textDecoration = "line-through";
    textStyle.color = "rgba(0, 0, 0, 0.45)";
  }

  return (
    <List.Item
      actions={[
        <Typography.Link key="view" onClick={() => onView(file)}>
          View
        </Typography.Link>,
        <Typography.Link
          key="download"
          href={file.downloadUrl}
          target="_blank"
          rel="noopener noreferrer"
        >
          Download
        </Typography.Link>,
      ]}
    >
      <Typography.Text style={textStyle}>
        <Tag>{fileType}</Tag>
        {file.displayName}
        <span
          style={{
            marginLeft: 8,
            display: "inline-flex",
            alignItems: "center",
            gap: 8,
          }}
        >
          {isNew && <Tag color="success">New</Tag>}
          {isRemoved && <Tag color="error">Removed</Tag>}
          {isRenamed && originalDisplayName && (
            <Tooltip title={`Original Value: ${originalDisplayName}`}>
              <Tag color="warning">Renamed</Tag>
            </Tooltip>
          )}
          {isTagChanged && originalTagValue && (
            <Tooltip title={`Original Value: ${originalTagValue}`}>
              <Tag color="processing">Tag Changed</Tag>
            </Tooltip>
          )}
        </span>
      </Typography.Text>
    </List.Item>
  );
};

export { FileListItemComponent };
