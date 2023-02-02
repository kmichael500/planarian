import { DeleteOutlined } from "@ant-design/icons";
import {
  PlanarianButton,
  PlanarianButtonTypeWithoutIcon,
} from "./PlanarianButtton";
import { Popconfirm, PopconfirmProps } from "antd";

type DeleteButtonomponentType = PopconfirmProps &
  PlanarianButtonTypeWithoutIcon;

const DeleteButtonComponent: React.FC<DeleteButtonomponentType> = (props) => {
  return (
    <Popconfirm {...props}>
      <PlanarianButton
        {...props}
        danger
        type="primary"
        icon={<DeleteOutlined />}
      >
        Delete
      </PlanarianButton>
    </Popconfirm>
  );
};

export { DeleteButtonComponent };
