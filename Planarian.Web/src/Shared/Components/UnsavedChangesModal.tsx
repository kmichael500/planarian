import { Typography } from "antd";
import { PlanarianButton } from "./Buttons/PlanarianButtton";
import { PlanarianModal } from "./Buttons/PlanarianModal";

interface UnsavedChangesModalProps {
  open: boolean;
  onKeepEditing: () => void;
  onDiscardChanges: () => void;
}

export const UnsavedChangesModal = ({
  open,
  onKeepEditing,
  onDiscardChanges,
}: UnsavedChangesModalProps) => (
  <PlanarianModal
    open={open}
    onClose={onKeepEditing}
    header="Discard unsaved changes?"
    width={480}
    height={260}
    footer={[
      <PlanarianButton key="keep" icon={undefined} onClick={onKeepEditing}>
        Keep editing
      </PlanarianButton>,
      <PlanarianButton
        key="discard"
        icon={undefined}
        danger
        onClick={onDiscardChanges}
      >
        Discard changes
      </PlanarianButton>,
    ]}
  >
    <Typography.Paragraph>
      Your changes have not been saved. If you leave this page, they will be
      lost.
    </Typography.Paragraph>
  </PlanarianModal>
);
