import { List, Tag, Typography } from "antd";
import { FileVm } from "../Models/FileVm";
import { useState } from "react";
import { getFileType } from "../Services/FileHelpers";
import { FileViewer } from "./FileViewerComponent";

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

export { FileListItemComponent };
