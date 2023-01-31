import { Layout, Menu, MenuProps } from "antd";
import {
  DatabaseOutlined,
  SettingOutlined,
  LogoutOutlined,
  LoginOutlined,
  UserAddOutlined,
} from "@ant-design/icons";
import React, { useContext, useState } from "react";
import "./App.css";

import { AppRouting } from "./App.routing";
import Favicon from "react-favicon";
import logo from "./logo.svg";
import { Helmet } from "react-helmet";
import { BrowserRouter, Link } from "react-router-dom";
import { AuthenticationService } from "./Modules/Authentication/Services/AuthenticationService";
import { AppContext, AppProvider } from "./Configuration/AppContext";
type MenuItem = Required<MenuProps>["items"][number];

const { Content, Footer, Sider } = Layout;

const App: React.FC = () => {
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
    <BrowserRouter>
      <AppProvider>
        <Layout style={{ minHeight: "100vh" }}>
          <Helmet>
            <title>Planarian</title>
            <meta name="description" content="Cave project managment" />
          </Helmet>

          <Favicon url={logo} />
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
          <Layout className="site-layout">
            <Content style={{ margin: "16px 16px" }}>
              <AppRouting />
            </Content>
            <Footer style={{ textAlign: "center" }}></Footer>
          </Layout>
        </Layout>
      </AppProvider>
    </BrowserRouter>
  );
};

export default App;
