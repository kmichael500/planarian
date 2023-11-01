import styled from "styled-components";
import { List, Tag, Typography } from "antd";
import { FileVm } from "../Models/FileVm";
import { ReactNode, useState } from "react";
import { getFileType } from "../Services/FileHelpers";
import { FileViewer } from "./FileViewerComponent";
import { createGlobalStyle } from "styled-components";
import React from "react";

const GlobalStyles = createGlobalStyle`
  .ant-list-item {
    padding: 10px;
  }

  .ant-modal,
  .ant-modal-content {
    height: 100vh;
    width: 100vw;
    margin: 0;
    top: 0;
  }

  .ant-modal-body {
    height: calc(100vh - 110px);
  }
`;

const FullScreenModal = ({ children }: { children: ReactNode }) => {
  return (
    <div>
      <React.Fragment>
        <GlobalStyles />
        {children}
      </React.Fragment>
    </div>
  );
};

const FileListItemComponent = ({ file }: { file: FileVm }) => {
  const [open, setOpen] = useState(false);
  const onFileClick = () => setOpen(true);
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
        onCancel={() => setOpen(false)}
        embedUrl={file.embedUrl}
        downloadUrl={file.downloadUrl}
        displayName={file.displayName}
        fileType={fileType}
      />
    </>
  );
};

export { FileListItemComponent, FullScreenModal };
