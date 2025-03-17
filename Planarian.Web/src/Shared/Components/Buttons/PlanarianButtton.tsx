import React, { useState } from "react";
import { Button, ButtonProps, Grid } from "antd";

interface PlanarianButtonProps {
  icon: React.ReactNode;
  alwaysShowChildren?: boolean;
  neverShowChildren?: boolean;
  // Optional debounce time in milliseconds (default: 500ms)
  debounceTime?: number;
}

const { useBreakpoint } = Grid;

export type PlanarianButtonType = ButtonProps & PlanarianButtonProps;
export type PlanarianButtonTypeWithoutIcon = Omit<PlanarianButtonType, "icon">;

const PlanarianButton: React.FC<PlanarianButtonType> = (props) => {
  const {
    cancelText,
    okText,
    onConfirm,
    debounceTime = 500,
    onClick,
    ...newProps
  } = props as any;
  props = newProps as any;

  const screens = useBreakpoint();
  const { alwaysShowChildren = false, neverShowChildren = false } = props;

  const isLargeScreenSize = Object.entries(screens).some(
    ([key, value]) => value && key === "xl"
  );

  const [debouncing, setDebouncing] = useState(false);

  const handleClick = (e: React.MouseEvent<HTMLButtonElement>) => {
    if (debouncing) return;

    if (onClick) {
      onClick(e);
    }

    setDebouncing(true);
    setTimeout(() => {
      setDebouncing(false);
    }, debounceTime);
  };

  return (
    <Button {...props} icon={props.icon} onClick={handleClick}>
      {!neverShowChildren &&
        (isLargeScreenSize || alwaysShowChildren) &&
        props.children}
    </Button>
  );
};

export { PlanarianButton };
