import { Menu } from "antd";
import { useContext, useEffect, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { AppContext } from "../Context/AppContext";
import { MenuInfo, MenuMode } from "rc-menu/lib/interface";
import { PermissionKey } from "../../Modules/Authentication/Models/PermissionKey";
import { AppService } from "../../Shared/Services/AppService";
import { MenuItemType } from "antd/lib/menu/interface";

const { SubMenu } = Menu;

export interface PlanarianMenuItem extends MenuItemType {
  children?: PlanarianMenuItem[];
  requiresAuthentication: boolean;
  action?: () => void; // Action to be executed when item is clicked
  isVisible?: boolean;
  permissionKey?: PermissionKey;
}

interface MenuComponentProps {
  onMenuItemClick?: (key: string) => void;
  menuItems: PlanarianMenuItem[];
  mode?: MenuMode;
}

const PlanarianMenuComponent = (props: MenuComponentProps) => {
  const [selectedKey, setSelectedKey] = useState<string>("");
  const [openedKeys, setOpenedKeys] = useState<string[]>([]);
  const { isAuthenticated } = useContext(AppContext);

  const filteredMenuItems = props.menuItems.filter(
    (item) => item.requiresAuthentication === isAuthenticated
  );

  const location = useLocation();

  const renderMenuItem = (item: PlanarianMenuItem) => {
    if (item.isVisible === false) return null;

    if (item.permissionKey) {
      if (!AppService.HasPermission(item.permissionKey)) {
        return null;
      }
    }

    if (item.children && item.children.length > 0) {
      return (
        <SubMenu key={item.key} title={item.label} icon={item.icon}>
          {item.children.map(renderMenuItem)}
        </SubMenu>
      );
    } else {
      return (
        <>
          {item.key !== undefined && item.key !== null && (
            <>
              <Menu.Item key={item.key} icon={item.icon}>
                {!item.action && (
                  <Link to={String(item.key)}>{item.label}</Link>
                )}
                {item.action && `${item.label}`}
              </Menu.Item>
            </>
          )}
          {(item.key === undefined || item.key === null) && <>{item.icon}</>}
        </>
      );
    }
  };

  const handleMenuClick = (e: MenuInfo) => {
    const key = e.key;
    const clickedItem = props.menuItems.find((item) => item.key === key);
    clickedItem?.action?.();

    props.onMenuItemClick?.(e.key);
  };

  useEffect(() => {
    const findSelectedItem = (
      items: PlanarianMenuItem[]
    ): PlanarianMenuItem | undefined => {
      const currentPathname = new URL(location.pathname, window.location.origin)
        .pathname;

      // First pass: Look for an exact match
      for (const item of items) {
        if (item.key && currentPathname === String(item.key)) return item; // Exact match

        if (item.children) {
          const foundChild = findSelectedItem(item.children);
          if (foundChild) return foundChild;
        }
      }

      // Second pass: If no exact match is found, look for a general match
      for (const item of items) {
        if (item.key && currentPathname.startsWith(String(item.key)))
          return item; // General match

        if (item.children) {
          const foundChild = findSelectedItem(item.children);
          if (foundChild) return foundChild;
        }
      }
    };

    const items = props.menuItems;
    const selectedItem = findSelectedItem(items);

    if (selectedItem?.key && typeof selectedItem.key === "string") {
      setSelectedKey(selectedItem.key);

      // Not sure if this is needed. It's purpose was to open the children of the selected item when refreshing the page
      // However, it causes a bug that always opens the children of the selected item before clicking the menu open/closed

      // const openKeys: string[] = [];
      // let parent = (items as PlanarianMenuItem[]).find((item) =>
      //   item.children?.includes(selectedItem)
      // );
      // while (parent && typeof parent.key === "string") {
      //   openKeys.push(parent.key);
      //   parent = (items as PlanarianMenuItem[]).find((item) =>
      //     item.children?.includes(parent!)
      //   );
      // }
      // setOpenedKeys(openKeys);
    }
  }, [location, props.menuItems]);

  return (
    <>
      <Menu
        theme="light"
        selectedKeys={[selectedKey]}
        openKeys={openedKeys}
        onOpenChange={setOpenedKeys}
        onSelect={(value) => setSelectedKey(value.key)}
        mode={props.mode ?? "inline"}
        onClick={(value: MenuInfo) => {
          handleMenuClick(value);
        }}
      >
        {filteredMenuItems.map(renderMenuItem)}
      </Menu>
    </>
  );
};

export { PlanarianMenuComponent };
