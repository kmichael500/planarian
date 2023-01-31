import { Layout, Menu, MenuProps } from "antd";
import {
  DatabaseOutlined,
  SettingOutlined,
  LogoutOutlined,
  LoginOutlined,
  UserAddOutlined,
} from "@ant-design/icons";
import React, { useContext, useState } from "react";
import Favicon from "react-favicon";
import { Helmet } from "react-helmet";
import { Link, BrowserRouter, useNavigate } from "react-router-dom";
import { AppRouting } from "../App.routing";
import { AuthenticationService } from "../Modules/Authentication/Services/AuthenticationService";
import { AppContext, AppProvider } from "./AppContext";
import { MenuItemType } from "antd/lib/menu/hooks/useItems";

const { Content, Footer, Sider } = Layout;

const SideBar: React.FC = () => {
  const [collapsed, setCollapsed] = useState(true);
  const { isAuthenticated, setIsAuthenticated } = useContext(AppContext);
  const navigate = useNavigate();

  console.log("isAuthenticated", isAuthenticated);

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
    <Sider
      collapsible
      collapsed={collapsed}
      onCollapse={(value) => setCollapsed(value)}
    >
      {isAuthenticated && (
        <Menu
          theme="dark"
          defaultSelectedKeys={["1"]}
          mode="inline"
          items={authenticatedMenuItems}
        />
      )}
      {!isAuthenticated && (
        <Menu
          theme="dark"
          defaultSelectedKeys={["1"]}
          mode="inline"
          items={unauthenticatedMenuItems}
        />
      )}
    </Sider>
  );
};

export { SideBar };
