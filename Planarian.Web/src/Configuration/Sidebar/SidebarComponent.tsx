import { Col, Divider, Layout, Menu, Row, Space, Typography } from "antd";
import {
  DatabaseOutlined,
  SettingOutlined,
  LogoutOutlined,
  LoginOutlined,
  UserAddOutlined,
} from "@ant-design/icons";
import React, { useContext, useEffect, useState } from "react";
import { Link, useNavigate, useLocation } from "react-router-dom";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";
import { AppContext } from "../Context/AppContext";
import { MenuItemType } from "antd/lib/menu/hooks/useItems";
import { LogoIcon } from "./AppIcon";

const { Sider } = Layout;

const SideBarComponent: React.FC = () => {
  const [collapsed, setCollapsed] = useState(true);
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
    // {
    //   key: "logo",

    //   icon: (
    //     <Link to="/">
    //       <LogoIcon />
    //     </Link>
    //   ),
    // },
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
    <Sider
      collapsible
      theme="light"
      collapsed={collapsed}
      onCollapse={(value) => setCollapsed(value)}
      style={{
        position: "sticky",
        top: 0,
        zIndex: 1,
        width: "100%",
        height: "100vh",
      }}
    >
      <Space
        direction="horizontal"
        style={{ width: "100%", justifyContent: "center" }}
      >
        <Link to="/">
          <LogoIcon style={{ padding: "10px", fontSize: "50px" }} />
        </Link>
      </Space>
      <Space
        direction="horizontal"
        style={{
          width: "100%",
          fontWeight: "lighter",
          justifyContent: "center",
        }}
      >
        <Typography.Text>Planarian</Typography.Text>
      </Space>
      {isAuthenticated && (
        <Menu
          theme="light"
          selectedKeys={[selectedKey]}
          onSelect={(value) => setSelectedKey(value.key)}
          mode="inline"
          items={authenticatedMenuItems}
        />
      )}
      {!isAuthenticated && (
        <Menu
          theme="light"
          selectedKeys={[selectedKey]}
          onSelect={(value) => setSelectedKey(value.key)}
          mode="inline"
          items={unauthenticatedMenuItems}
        />
      )}
    </Sider>
  );
};

export { SideBarComponent };
