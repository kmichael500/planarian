import React, { useState } from "react";
import { Button, ButtonProps, Grid, Tooltip } from "antd";
import { AppService } from "../../Services/AppService";
import { PermissionKey } from "../../../Modules/Authentication/Models/PermissionKey";

interface PlanarianButtonProps {
  icon: React.ReactNode;
  alwaysShowChildren?: boolean;
  neverShowChildren?: boolean;
  collapseOnScreenSize?: "xl" | "lg" | "md" | "sm" | "xs";
  debounceTime?: number; // Optional debounce time in milliseconds (default: 500ms)
  permissionKey?: PermissionKey;
  tooltip?: React.ReactNode;
}

const { useBreakpoint } = Grid;

export type PlanarianButtonType = ButtonProps & PlanarianButtonProps;
export type PlanarianButtonTypeWithoutIcon = Omit<PlanarianButtonType, "icon">;

const PlanarianButton: React.FC<PlanarianButtonType> = (props) => {
  const {
    debounceTime = 500,
    onClick,
    permissionKey,
    tooltip,
    collapseOnScreenSize,
    alwaysShowChildren = false,
    neverShowChildren = false,
    ...rest
  } = props;

  const screens = useBreakpoint();
  const permissionDisabled =
    permissionKey && !AppService.HasPermission(permissionKey);

  const disabled = rest.disabled;

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

  // Define breakpoints order (from smallest to largest)
  const breakpointsOrder: Array<"xs" | "sm" | "md" | "lg" | "xl"> = [
    "xs",
    "sm",
    "md",
    "lg",
    "xl",
  ];

  const activeBreakpoint =
    breakpointsOrder
      .slice()
      .reverse()
      .find((bp) => screens[bp as keyof typeof screens]) || "xs";

  let showChildren = true;
  if (neverShowChildren) {
    showChildren = false;
  } else if (alwaysShowChildren) {
    showChildren = true;
  } else if (collapseOnScreenSize) {
    const activeIndex = breakpointsOrder.indexOf(activeBreakpoint);
    const collapseIndex = breakpointsOrder.indexOf(collapseOnScreenSize);

    showChildren = activeIndex > collapseIndex;
  } else {
    showChildren = !!screens.xl;
  }

  const getButton = () => (
    <Button
      {...rest}
      icon={rest.icon}
      onClick={handleClick}
      disabled={disabled}
    >
      {showChildren && rest.children}
    </Button>
  );

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
