import { List, Tag, Typography } from "antd";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { FileVm } from "../Models/FileVm";
import { getFileType } from "../Services/FileHelpers";
import { FileService } from "../Services/FileService";
import { PlanarianTag } from "../../../Shared/Components/Display/PlanarianTag";
import "./FileListItemComponent.scss";

type FileListItemFile = Pick<FileVm, "id" | "name" | "extension"> &
  Partial<Pick<FileVm, "uuid" | "fileTypeTagId" | "fileTypeKey">>;

interface FileListItemComponentProps {
  file: FileListItemFile;
  onView?: (file: FileListItemFile) => void;
  onDownload?: (file: FileListItemFile) => void;
  status?: { label: string; color: string };
  unavailableText?: string;
  ariaLabel?: string;
  allowDefaultDownload?: boolean;
  showFileType?: boolean;
}

const FileListItemComponent = ({
  file,
  onView,
  onDownload,
  status,
  unavailableText,
  ariaLabel,
  allowDefaultDownload = true,
  showFileType = true,
}: FileListItemComponentProps) => {
  const fileType = getFileType(file.extension);

  return (
    <List.Item
      className="planarian-file-list-item"
      role="group" aria-label={ariaLabel ?? `${file.name} file`}
      actions={[
        ...(onView ? [<PlanarianButton key="view" type="link" size="small" alwaysShowChildren className="planarian-file-action"
          aria-label={`View ${file.name}`} onClick={() => onView(file)}>View</PlanarianButton>] : []),
        ...((onDownload || (allowDefaultDownload && !unavailableText)) ? [<PlanarianButton key="download" type="link"
          size="small" alwaysShowChildren className="planarian-file-action" aria-label={`Download ${file.name}`} onClick={() => {
            if (onDownload) onDownload(file);
            else FileService.startFileDownload(file.id);
          }}>Download</PlanarianButton>] : []),
      ]}
    >
      <Typography.Text>
        {showFileType && <PlanarianTag>{fileType}</PlanarianTag>}
        {file.name}
        {status && <Tag color={status.color} style={{ marginInlineStart: 8 }}>{status.label}</Tag>}
        {unavailableText && <Typography.Text type="secondary" style={{ marginInlineStart: 8 }}>
          {unavailableText}
        </Typography.Text>}
      </Typography.Text>
    </List.Item>
  );
};

export { FileListItemComponent };
