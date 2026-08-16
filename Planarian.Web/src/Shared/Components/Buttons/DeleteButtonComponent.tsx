import { DeleteOutlined } from "@ant-design/icons";
import { Popconfirm, PopconfirmProps } from "antd";
import { ReactNode } from "react";
import {
  PlanarianButton,
  PlanarianButtonTypeWithoutIcon,
} from "./PlanarianButtton";

type DeleteButtonConfirmationProps = Pick<
  PopconfirmProps,
  "title" | "description" | "onConfirm" | "onCancel" | "okText" | "cancelText"
>;

type DeleteButtonComponentType = Omit<
  PlanarianButtonTypeWithoutIcon,
  keyof DeleteButtonConfirmationProps | "title"
> &
  DeleteButtonConfirmationProps & {
    children?: ReactNode;
    icon?: ReactNode;
  };

const DeleteButtonComponent: React.FC<DeleteButtonComponentType> = ({
  title,
  description,
  onConfirm,
  onCancel,
  okText,
  cancelText,
  children,
  icon = <DeleteOutlined />,
  ...buttonProps
}) => {
  return (
    <Popconfirm
      title={title}
      description={description}
      onConfirm={onConfirm}
      onCancel={onCancel}
      okText={okText}
      cancelText={cancelText}
    >
      <PlanarianButton
        {...buttonProps}
        danger={buttonProps.danger ?? true}
        type={buttonProps.type ?? "primary"}
        icon={icon}
      >
        {children || "Delete"}
      </PlanarianButton>
    </Popconfirm>
  );
};

export { DeleteButtonComponent };
