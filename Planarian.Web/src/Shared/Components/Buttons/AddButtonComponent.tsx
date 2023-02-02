import { PlusCircleOutlined } from "@ant-design/icons";
import { ButtonProps } from "antd";
import {
  PlanarianButton,
  PlanarianButtonType,
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
