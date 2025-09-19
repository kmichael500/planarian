import { List, Tag, Typography } from "antd";
import { FileVm } from "../Models/FileVm";
import { getFileType } from "../Services/FileHelpers";

interface FileListItemComponentProps {
  file: FileVm;
  onView: (file: FileVm) => void;
}

const FileListItemComponent = ({
  file,
  onView,
}: FileListItemComponentProps) => {
  const fileType = getFileType(file.fileName);

  return (
    <List.Item
      actions={[
        <Typography.Link onClick={() => onView(file)}>View</Typography.Link>,
        <Typography.Link href={file.downloadUrl}>Download</Typography.Link>,
      ]}
    >
      <Typography.Text>
        <Tag>{fileType}</Tag>
        {file.displayName}
      </Typography.Text>
    </List.Item>
  );
};

export { FileListItemComponent };
