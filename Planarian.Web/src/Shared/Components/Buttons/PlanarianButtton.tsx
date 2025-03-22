import React, { useState } from "react";
import { Button, ButtonProps, Grid, Tooltip } from "antd";
import { AppService } from "../../Services/AppService";
import { PermissionKey } from "../../../Modules/Authentication/Models/PermissionKey";

interface PlanarianButtonProps {
  icon: React.ReactNode;
  alwaysShowChildren?: boolean;
  neverShowChildren?: boolean;
  // Optional debounce time in milliseconds (default: 500ms)
  debounceTime?: number;
  permissionKey?: PermissionKey;
  tooltip?: React.ReactNode;
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
    permissionKey,
    tooltip,
    ...newProps
  } = props as any;
  props = newProps as any;

  const screens = useBreakpoint();
  const { alwaysShowChildren = false, neverShowChildren = false } = props;

  const isLargeScreenSize = Object.entries(screens).some(
    ([key, value]) => value && key === "xl"
  );

  const permissionDisabled =
    permissionKey && !AppService.HasPermission(permissionKey);

  // Merge with any existing disabled prop
  const disabled = props.disabled;

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

  const getButton = () => {
    return (
      <Button
        {...props}
        icon={props.icon}
        onClick={handleClick}
        disabled={disabled}
      >
        {!neverShowChildren &&
          (isLargeScreenSize || alwaysShowChildren) &&
          props.children}
      </Button>
    );
  };

  return (
    <>
      {!permissionDisabled &&
        (tooltip ? (
          <Tooltip title={tooltip}>
            <>{getButton()}</>
          </Tooltip>
        ) : (
          getButton()
        ))}
    </>
  );
};

export { PlanarianButton };
