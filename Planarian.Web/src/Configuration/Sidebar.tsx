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
import { Link, BrowserRouter } from "react-router-dom";
import { AppRouting } from "../App.routing";
import { AuthenticationService } from "../Modules/Authentication/Services/AuthenticationService";
import { AppContext, AppProvider } from "./AppContext";

const { Content, Footer, Sider } = Layout;

const SideBar: React.FC = () => {
  const [collapsed, setCollapsed] = useState(true);
  const { isAuthenticated, setIsAuthenticated } = useContext(AppContext);

  console.log("isAuthenticated", isAuthenticated);

  const menuItems = isAuthenticated
    ? [
        {
          key: "1",
          icon: (
            <Link to="/projects">
              <DatabaseOutlined />
            </Link>
          ),
          title: "Projects",
        },
        {
          key: "2",
          icon: (
            <Link to="/settings">
              <SettingOutlined />
            </Link>
          ),
          title: "Projects",
        },
        {
          key: "3",
          icon: (
            <LogoutOutlined
              onClick={() => {
                AuthenticationService.Logout();
                window.location.reload();
              }}
            />
          ),
          title: "Logout",
        },
      ]
    : [];

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
          items={menuItems}
        />
      )}
    </Sider>
  );
};

export { SideBar };
