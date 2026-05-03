import { List, Typography } from "antd";
import { FileVm } from "../Models/FileVm";
import { getFileType } from "../Services/FileHelpers";
import { FileService } from "../Services/FileService";
import { PlanarianTag } from "../../../Shared/Components/Display/PlanarianTag";
import "./FileListItemComponent.scss";

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
      className="planarian-file-list-item"
      actions={[
        <Typography.Link onClick={() => onView(file)}>View</Typography.Link>,
        <Typography.Link
          onClick={async () => {
            FileService.startFileDownload(file.id);
          }}
        >
          Download
        </Typography.Link>,
      ]}
    >
      <Typography.Text>
        <PlanarianTag>{fileType}</PlanarianTag>
        {file.displayName}
      </Typography.Text>
    </List.Item>
  );
};

export { FileListItemComponent };
