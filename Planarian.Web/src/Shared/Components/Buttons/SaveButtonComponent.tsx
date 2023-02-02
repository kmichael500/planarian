import { SaveOutlined } from "@ant-design/icons";
import {
  PlanarianButton,
  PlanarianButtonTypeWithoutIcon,
} from "./PlanarianButtton";

const SaveButtonComponent: React.FC<PlanarianButtonTypeWithoutIcon> = (
  props
) => {
  return (
    <PlanarianButton type="primary" {...props} icon={<SaveOutlined />}>
      Save
    </PlanarianButton>
  );
};

export { SaveButtonComponent };
