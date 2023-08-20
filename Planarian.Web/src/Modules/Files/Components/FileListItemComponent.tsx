import { List, Typography } from "antd";
import { FileVm } from "../Models/FileVm";
import "./FileListItemComponent.scss";

const FileListItemComponent = ({ file }: { file: FileVm }) => {
  return (
    <List.Item
      actions={[<Typography.Link href={file.url}>Download</Typography.Link>]}
    >
      <Typography.Text>{file.displayName}</Typography.Text>
    </List.Item>
  );
};

export { FileListItemComponent };
