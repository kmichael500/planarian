import { Card } from "antd";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { FileVm } from "../Models/FileVm";
import { CloudDownloadOutlined } from "@ant-design/icons";

const FileCardComponent = ({ file }: { file: FileVm }) => {
  return (
    <Card
      title={file.displayName}
      actions={[
        <PlanarianButton
          type="primary"
          // onClick={() => handleDownload(file.id)}
          icon={<CloudDownloadOutlined />}
        >
          Download
        </PlanarianButton>,
      ]}
    >
      <p>Name: {file.fileName}</p>
      <TagComponent tagId={file.fileTypeTagId} />
    </Card>
  );
};

export { FileCardComponent };
