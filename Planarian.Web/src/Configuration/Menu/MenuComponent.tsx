import { Divider, Menu } from "antd";
import { MenuItemType } from "antd/lib/menu/hooks/useItems";
import { useContext, useEffect, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";
import { AppContext } from "../Context/AppContext";
import {
  DatabaseOutlined,
  LoginOutlined,
  LogoutOutlined,
  SettingOutlined,
  UserAddOutlined,
  ImportOutlined,
} from "@ant-design/icons";
import SubMenu from "antd/lib/menu/SubMenu";

interface PlanarianMenuItem extends MenuItemType {
  children?: PlanarianMenuItem[];
}

interface MenuComponentProps {
  onMenuItemClick?: (key: string) => void;
}

const MenuComponent: React.FC<MenuComponentProps> = (props) => {
  const [selectedKey, setSelectedKey] = useState<string>("");
  const [openedKeys, setOpenedKeys] = useState<string[]>([]);

  const { isAuthenticated, setIsAuthenticated } = useContext(AppContext);
  const navigate = useNavigate();
  const location = useLocation();

  const renderMenuItem = (item: PlanarianMenuItem) => {
    if (item.children && item.children.length > 0) {
      return (
        <SubMenu key={item.key} title={item.label} icon={item.icon}>
          {item.children.map(renderMenuItem)}
        </SubMenu>
      );
    } else {
      return (
        <Menu.Item key={item.key} icon={item.icon} onClick={item.onClick}>
          <Link to={String(item.key)}>{item.label}</Link>
        </Menu.Item>
      );
    }
  };

  const authenticatedMenuItems = [
    {
      key: "/caves",
      icon: (
        <Link to="/caves">
          <DatabaseOutlined />
        </Link>
      ),
      label: "Caves",
    },

    {
      key: "/account",
      icon: <SettingOutlined />,
      label: "Account",
      children: [
        {
          key: "/account/import",
          icon: (
            <Link to="/caves/import">
              <ImportOutlined />
            </Link>
          ),
          label: "Import",
        },
        {
          key: "/account/settings",
          icon: <SettingOutlined />,
          label: "Settings",
        },
      ],
    },
    {
      icon: <Divider />,
    },
    {
      key: "/projects",
      icon: (
        <Link to="/projects">
          <DatabaseOutlined />
        </Link>
      ),
      label: "Projects",
    },
    {
      key: "/settings",
      icon: (
        <Link to="/settings">
          <SettingOutlined />
        </Link>
      ),
      label: "Settings",
    },
    {
      key: "logout",

      icon: <LogoutOutlined />,
      onClick: () => {
        AuthenticationService.Logout();
        setIsAuthenticated(false);
        navigate("/login");
      },
      label: "Logout",
    },
  ] as PlanarianMenuItem[];

  const unauthenticatedMenuItems = [
    {
      key: "/login",
      icon: (
        <Link to="/login">
          <LoginOutlined />
        </Link>
      ),
      label: "Login",
    },
    {
      key: "/register",
      icon: (
        <Link to="/register">
          <UserAddOutlined />
        </Link>
      ),
      label: "Register",
    },
  ] as PlanarianMenuItem[];

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

    const items = isAuthenticated
      ? authenticatedMenuItems
      : unauthenticatedMenuItems;
    const selectedItem = findSelectedItem(items);

    if (selectedItem?.key && typeof selectedItem.key === "string") {
      setSelectedKey(selectedItem.key);

      const openKeys: string[] = [];
      let parent = (items as PlanarianMenuItem[]).find((item) =>
        item.children?.includes(selectedItem)
      );
      while (parent && typeof parent.key === "string") {
        openKeys.push(parent.key);
        parent = (items as PlanarianMenuItem[]).find((item) =>
          item.children?.includes(parent!)
        );
      }
      setOpenedKeys(openKeys);
    }
  }, [location, isAuthenticated]);

  return (
    <>
      {isAuthenticated && (
        <Menu
          theme="light"
          selectedKeys={[selectedKey]}
          openKeys={openedKeys}
          onOpenChange={setOpenedKeys}
          onSelect={(value) => setSelectedKey(value.key)}
          mode="inline"
          onClick={(value) => {
            props.onMenuItemClick?.(value.key);
          }}
        >
          {authenticatedMenuItems.map(renderMenuItem)}
        </Menu>
      )}
      {!isAuthenticated && (
        <Menu
          theme="light"
          selectedKeys={[selectedKey]}
          openKeys={openedKeys}
          onOpenChange={setOpenedKeys}
          onSelect={(value) => setSelectedKey(value.key)}
          mode="inline"
          onClick={(value) => {
            props.onMenuItemClick?.(value.key);
          }}
        >
          {unauthenticatedMenuItems.map(renderMenuItem)}
        </Menu>
      )}
    </>
  );
};

export { MenuComponent };
