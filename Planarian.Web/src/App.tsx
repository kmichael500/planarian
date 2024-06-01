import { Col, Layout, Row, Spin } from "antd";
import React, { useState, useEffect } from "react";
import "./App.css";
import { AppRouting } from "./Configuration/Routing/App.routing";
import Favicon from "react-favicon";
import logo from "./logo.svg";
import { Helmet } from "react-helmet";
import { BrowserRouter } from "react-router-dom";
import { AppContext, AppProvider } from "./Configuration/Context/AppContext";
import { SideBarComponent } from "./Configuration/Sidebar/SidebarComponent";
import { HeaderComponent } from "./Configuration/Header/HeaderComponent";
import { AppService } from "./Shared/Services/AppService";
import { LogoIcon } from "./Configuration/Sidebar/AppIcon";
import { ApiErrorResponse } from "./Shared/Models/ApiErrorResponse";

const { Content } = Layout;

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
    <>
      <Helmet>
        <title>Planarian</title>
        <meta name="description" content="Cave project managment" />
      </Helmet>
      <Favicon url={logo} />
      <BrowserRouter>
        <AppProvider>
          {isInitialized ? (
            <Layout style={{ minHeight: "100vh" }}>
              <SideBarComponent />
              <Layout className="site-layout">
                <HeaderComponent />
                <AppContext.Consumer>
                  {({ hideBodyPadding }) => (
                    <Content
                      style={{ margin: hideBodyPadding ? "0px" : "16px" }}
                    >
                      <AppRouting />
                    </Content>
                  )}
                </AppContext.Consumer>
                {/* <Footer style={{ textAlign: "center" }}></Footer> */}
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
                style={{ padding: "10px", fontSize: "150px", opacity: "0.5" }}
              />
              <Row>
                <Col span={24}>
                  <Spin spinning={isLoading} size="large" tip="Planarian" />
                </Col>
                <Col span={24}>{initializedError}</Col>
              </Row>
            </div>
          )}
        </AppProvider>
      </BrowserRouter>
    </>
  );
};

export default App;
