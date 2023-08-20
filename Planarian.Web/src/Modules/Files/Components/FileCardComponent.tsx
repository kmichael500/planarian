import { Card } from "antd";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { FileVm } from "../Models/FileVm";
import { CloudDownloadOutlined } from "@ant-design/icons";

const FileCardComponent = ({ file }: { file: FileVm }) => {
  const handleDownload = (file: FileVm): void => {
    var fileUrl = file.url;

    var a = document.createElement("a");

    // Set the download attribute to specify the file name
    a.download = file.fileName;

    // Set the URL of the file
    a.href = fileUrl;

    // click link to trigger the download
    a.click();
  };

  return (
    <Card
      style={{ height: "100%" }}
      title={file.displayName}
      actions={[
        <PlanarianButton
          type="primary"
          onClick={() => handleDownload(file)}
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
