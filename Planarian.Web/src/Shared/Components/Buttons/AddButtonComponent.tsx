import { PlusCircleOutlined } from "@ant-design/icons";
import {
  PlanarianButton,
  PlanarianButtonTypeWithoutIcon,
} from "./PlanarianButtton";

const AddButtonComponent: React.FC<PlanarianButtonTypeWithoutIcon> = (
  props
) => {
  return (
    <PlanarianButton type="primary" {...props} icon={<PlusCircleOutlined />}>
      Add
    </PlanarianButton>
  );
};

export { AddButtonComponent };
