import { Col, Layout, Row, Space, Spin } from "antd";
import React, { useState, useEffect } from "react";
import "./App.css";
import { AppRouting } from "./App.routing";
import Favicon from "react-favicon";
import logo from "./logo.svg";
import { Helmet } from "react-helmet";
import { BrowserRouter } from "react-router-dom";
import { AppProvider } from "./Configuration/Context/AppContext";
import { SideBarComponent } from "./Configuration/Sidebar/SidebarComponent";
import { HeaderComponent } from "./Configuration/Header/HeaderComponent";
import { AppService } from "./Shared/Services/AppService";
import { AuthenticationService } from "./Modules/Authentication/Services/AuthenticationService";
import { LogoIcon } from "./Configuration/Sidebar/AppIcon";
import { ApiErrorResponse } from "./Shared/Models/ApiErrorResponse";

const { Content, Footer, Sider } = Layout;

const App: React.FC = () => {
  const [isInitialized, setIsInitialized] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [initializedError, setInitializedError] = useState<string | null>(null);
  useEffect(() => {
    const initializeApp = async () => {
      try {
        await AppService.InitializeApp();
        setIsInitialized(true);
        setIsLoading(false);
      } catch (e) {
        const error = e as ApiErrorResponse;
        setInitializedError(error.message);
        setIsLoading(false);
      }
    };
    initializeApp();
  }, []);

  return (
    <BrowserRouter>
      {isInitialized && (
        <>
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
        </>
      )}
      {!isInitialized && (
        <>
          <div
            style={{
              display: "flex",
              justifyContent: "center",
              alignItems: "center",
              height: "100vh",
            }}
          >
            <LogoIcon
              style={{ padding: "10px", fontSize: "150px", opacity: "0.5" }}
            />

            <Row>
              <Col span={24}>
                <Spin spinning={isLoading} size="large" tip="Planarian"></Spin>
              </Col>
              <Col span={24}>{initializedError}</Col>
            </Row>
          </div>
        </>
      )}
    </BrowserRouter>
  );
};

export default App;
