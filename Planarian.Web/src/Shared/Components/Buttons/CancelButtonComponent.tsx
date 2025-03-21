import { UndoOutlined } from "@ant-design/icons";
import {
  PlanarianButton,
  PlanarianButtonTypeWithoutIcon,
} from "./PlanarianButtton";

const CancelButtonComponent: React.FC<PlanarianButtonTypeWithoutIcon> = (
  props
) => {
  return (
    <PlanarianButton
      alwaysShowChildren
      danger
      type="primary"
      {...props}
      icon={<UndoOutlined />}
    >
      Cancel
    </PlanarianButton>
  );
};

export { CancelButtonComponent };
