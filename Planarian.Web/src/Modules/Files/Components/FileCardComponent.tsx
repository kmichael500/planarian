import { List, Typography } from "antd";
import { FileVm } from "../Models/FileVm";
import "./FileListComponent.scss";

const FileListComponent = ({ file }: { file: FileVm }) => {
  const listItemStyle = {
    transition: "background-color 0.3s",
    "&:hover": {
      backgroundColor: "#f0f0f0", // Change the background color when hovered
      cursor: "pointer", // Change the cursor to a pointer on hover
    },
  };

  return (
    <List.Item
      style={listItemStyle}
      actions={[<Typography.Link href={file.url}>Download</Typography.Link>]}
    >
      <Typography.Text>{file.displayName}</Typography.Text>
    </List.Item>
  );
};

export { FileListComponent };
