import { UndoOutlined } from "@ant-design/icons";
import {
  PlanarianButton,
  PlanarianButtonTypeWithoutIcon,
} from "./PlanarianButtton";

const CancelButtonComponent: React.FC<PlanarianButtonTypeWithoutIcon> = (
  props
) => {
  return (
    <PlanarianButton danger type="primary" {...props} icon={<UndoOutlined />}>
      Cancel
    </PlanarianButton>
  );
};

export { CancelButtonComponent };
