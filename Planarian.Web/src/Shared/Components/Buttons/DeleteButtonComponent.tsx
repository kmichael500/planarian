import { DeleteOutlined } from "@ant-design/icons";
import {
  PlanarianButton,
  PlanarianButtonTypeWithoutIcon,
} from "./PlanarianButtton";
import { Popconfirm, PopconfirmProps } from "antd";
import { ReactNode } from "react";

type DeleteButtonComponentType = PopconfirmProps &
  PlanarianButtonTypeWithoutIcon & {
    children?: ReactNode;
  };

const DeleteButtonComponent: React.FC<DeleteButtonComponentType> = (props) => {
  return (
    <Popconfirm {...props}>
      <PlanarianButton
        {...props}
        danger={props.danger ?? true}
        type={props.type ?? "primary"}
        icon={<DeleteOutlined />}
      >
        {props.children || "Delete"}
      </PlanarianButton>
    </Popconfirm>
  );
};

export { DeleteButtonComponent };
