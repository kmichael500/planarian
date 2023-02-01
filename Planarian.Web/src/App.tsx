import {
  Button,
  Col,
  Layout,
  Menu,
  MenuProps,
  Row,
  Space,
  Typography,
} from "antd";
import React from "react";
import "./App.css";

import { AppRouting } from "./App.routing";
import Favicon from "react-favicon";
import logo from "./logo.svg";
import { Helmet } from "react-helmet";
import { BrowserRouter, Link } from "react-router-dom";
import { AppProvider } from "./Configuration/Context/AppContext";
import { SideBarComponent } from "./Configuration/Sidebar/SidebarComponent";
import { Header } from "antd/lib/layout/layout";
import { LogoIcon } from "./Configuration/Sidebar/AppIcon";
import { HeaderComponent } from "./Configuration/NavBar/HeaderComponent";

const { Content, Footer, Sider } = Layout;

const App: React.FC = () => {
  return (
    <BrowserRouter>
      <AppProvider>
        <Layout style={{ minHeight: "100vh" }}>
          <Helmet>
            <title>Planarian</title>
            <meta name="description" content="Cave project managment" />
          </Helmet>
          <Favicon url={logo} />
          <SideBarComponent />
          <Layout className="site-layout">
            <HeaderComponent />
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
