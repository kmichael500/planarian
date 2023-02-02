import { Layout } from "antd";
import React from "react";
import "./App.css";

import { AppRouting } from "./App.routing";
import Favicon from "react-favicon";
import logo from "./logo.svg";
import { Helmet } from "react-helmet";
import { BrowserRouter } from "react-router-dom";
import { AppProvider } from "./Configuration/Context/AppContext";
import { SideBarComponent } from "./Configuration/Sidebar/SidebarComponent";
import { HeaderComponent } from "./Configuration/Header/HeaderComponent";

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
