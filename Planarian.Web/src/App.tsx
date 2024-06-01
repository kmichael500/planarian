import { Col, Layout, Row, Spin } from "antd";
import React from "react";
import "./App.css";
import { AppRouting } from "./Configuration/Routing/App.routing";
import Favicon from "react-favicon";
import logo from "./logo.svg";
import { Helmet } from "react-helmet";
import { BrowserRouter } from "react-router-dom";
import { AppContext, AppProvider } from "./Configuration/Context/AppContext";
import { SideBarComponent } from "./Configuration/Sidebar/SidebarComponent";
import { HeaderComponent } from "./Configuration/Header/HeaderComponent";
import { LogoIcon } from "./Configuration/Sidebar/AppIcon";

const { Content } = Layout;

const App: React.FC = () => {
  return (
    <>
      <Helmet>
        <title>Planarian</title>
        <meta name="description" content="Cave project management" />
      </Helmet>
      <Favicon url={logo} />
      <BrowserRouter>
        <AppProvider>
          <AppContext.Consumer>
            {({ isInitialized, isLoading, initializedError }) =>
              isInitialized ? (
                <Layout style={{ minHeight: "100vh" }}>
                  <SideBarComponent />
                  <Layout className="site-layout">
                    <HeaderComponent />
                    <Content style={{ margin: "16px" }}>
                      <AppRouting />
                    </Content>
                  </Layout>
                </Layout>
              ) : (
                <div
                  style={{
                    display: "flex",
                    justifyContent: "center",
                    alignItems: "center",
                    height: "100vh",
                  }}
                >
                  <LogoIcon
                    style={{
                      padding: "10px",
                      fontSize: "150px",
                      opacity: "0.5",
                    }}
                  />
                  <Row>
                    <Col span={24}>
                      <Spin spinning={isLoading} size="large" tip="Planarian" />
                    </Col>
                    <Col span={24}>{initializedError}</Col>
                  </Row>
                </div>
              )
            }
          </AppContext.Consumer>
        </AppProvider>
      </BrowserRouter>
    </>
  );
};

export default App;
