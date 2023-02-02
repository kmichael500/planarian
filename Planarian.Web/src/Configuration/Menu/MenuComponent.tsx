import { Menu } from "antd";
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
} from "@ant-design/icons";

interface MenuComponentProps {
  onMenuItemClick?: (key: string) => void;
}

const MenuComponent: React.FC<MenuComponentProps> = (props) => {
  const [selectedKey, setSelectedKey] = useState<string>("");
  const { isAuthenticated, setIsAuthenticated } = useContext(AppContext);
  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    if (isAuthenticated) {
      const selectedItem = authenticatedMenuItems.find(
        (item) => item.key && location.pathname.startsWith(`/${item.key}`)
      );
      if (selectedItem?.key) {
        setSelectedKey(selectedItem.key as string);
      }
    } else {
      const selectedItem = unauthenticatedMenuItems.find(
        (item) => item.key && location.pathname.startsWith(`/${item.key}`)
      );
      if (selectedItem?.key) {
        setSelectedKey(selectedItem.key as string);
      }
    }
  }, [location]);

  const authenticatedMenuItems = [
    {
      key: "projects",
      icon: (
        <Link to="/projects">
          <DatabaseOutlined />
        </Link>
      ),
      label: "Projects",
    },
    {
      key: "settings",
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
  ] as MenuItemType[];

  const unauthenticatedMenuItems = [
    {
      key: "login",
      icon: (
        <Link to="/login">
          <LoginOutlined />
        </Link>
      ),
      label: "Login",
    },
    {
      key: "register",
      icon: (
        <Link to="/register">
          <UserAddOutlined />
        </Link>
      ),
      label: "Register",
    },
  ] as MenuItemType[];

  return (
    <>
      {isAuthenticated && (
        <Menu
          theme="light"
          selectedKeys={[selectedKey]}
          onSelect={(value) => setSelectedKey(value.key)}
          mode="inline"
          onClick={(value) => {
            props.onMenuItemClick?.(value.key);
          }}
          items={authenticatedMenuItems}
        />
      )}{" "}
      {!isAuthenticated && (
        <Menu
          theme="light"
          selectedKeys={[selectedKey]}
          onSelect={(value) => setSelectedKey(value.key)}
          mode="inline"
          onClick={(value) => {
            props.onMenuItemClick?.(value.key);
          }}
          items={unauthenticatedMenuItems}
        />
      )}
    </>
  );
};

export { MenuComponent };
