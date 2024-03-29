import { Button, ButtonProps, Grid } from "antd";

interface PlanarianButtonProps {
  icon: React.ReactNode;
  alwaysShowChildren?: boolean;
  neverShowChildren?: boolean;
}

const { useBreakpoint } = Grid;

export type PlanarianButtonType = ButtonProps & PlanarianButtonProps;
export type PlanarianButtonTypeWithoutIcon = Omit<PlanarianButtonType, "icon">;

const PlanarianButton: React.FC<PlanarianButtonType> = (props) => {
  const { cancelText, okText, onConfirm, ...newProps } = props as any;
  props = newProps as any;

  const screens = useBreakpoint();
  const { alwaysShowChildren = false, neverShowChildren = false } = props;

  const isLargeScreenSize = Object.entries(screens).some(
    ([key, value]) => value && key === "xl"
  );

  return (
    <Button {...props} icon={props.icon}>
      {!neverShowChildren &&
        (isLargeScreenSize || alwaysShowChildren) &&
        props.children}
    </Button>
  );
};

export { PlanarianButton };
