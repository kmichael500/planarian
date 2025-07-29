// src/modules/Files/Components/FileListItemComponent.tsx
import React, { useState } from "react";
import { List, Tag, Typography, Tooltip } from "antd";
import { FileVm } from "../Models/FileVm";
import { getFileType } from "../Services/FileHelpers";
import { FileViewer } from "./FileViewerComponent";

export const FileListItemComponent = ({
  file,
  isNew,
  isRemoved,
  isRenamed,
  isTagChanged,
  originalDisplayName,
  originalTagValue,
}: {
  file: FileVm;
  isNew?: boolean;
  isRemoved?: boolean;
  isRenamed?: boolean;
  isTagChanged?: boolean;
  originalDisplayName?: string | null;
  originalTagValue?: string | null;
}) => {
  const [open, setOpen] = useState(false);
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
          <Typography.Link key="view" onClick={onFileClick}>
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
          {/* File type tag */}
          <Tag>{fileType}</Tag>

          {/* File name */}
          {file.displayName}

          {/* Status tags container */}
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
