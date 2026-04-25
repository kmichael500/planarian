import { RedoOutlined } from "@ant-design/icons";
import { theme as antdTheme } from "antd";
import { Col, ConfigProvider, Layout, Row, Spin, message } from "antd";
import React from "react";
import { Helmet } from "react-helmet";
import { BrowserRouter } from "react-router-dom";
import "./App.css";
import { AppContext, AppProvider } from "./Configuration/Context/AppContext";
import { HeaderComponent } from "./Configuration/Header/HeaderComponent";
import { AppRouting } from "./Configuration/Routing/App.routing";
import { SideBarComponent } from "./Configuration/Sidebar/SidebarComponent";
import { LogoIcon } from "./Configuration/Sidebar/AppIcon";
import { SyncfusionThemeManager } from "./SyncfusionThemeManager";
import { ThemeProvider, useTheme } from "./ThemeProvider";
import { PlanarianButton } from "./Shared/Components/Buttons/PlanarianButtton";
import {
  ApiErrorResponse,
  ApiExceptionType,
} from "./Shared/Models/ApiErrorResponse";

const { Content } = Layout;

const AppContent: React.FC = () => {
  return (
    <>
      <Helmet>
        <title>Planarian</title>
        <meta name="description" content="Cave project management" />
      </Helmet>
      <SyncfusionThemeManager />
      <BrowserRouter>
        <AppProvider>
          <AppContext.Consumer>
            {({
              isInitialized,
              isLoading,
              initializedError,
              contentStyle,
              logout,
            }) =>
              isInitialized ? (
                <Layout
                  style={{
                    height: "calc(var(--vh, 1vh) * 100)",
                    minHeight: 0,
                    overflow: "hidden",
                  }}
                >
                  <SideBarComponent />
                  <Layout className="site-layout">
                    <HeaderComponent />
                    <Content
                      className="site-layout-content"
                      style={contentStyle ?? {}}
                    >
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
                    height: "calc(var(--vh, 1vh) * 100)",
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
                    <Col span={24}>{initializedError?.message}</Col>
                    {initializedError?.errorCode ===
                      ApiExceptionType.Unauthorized && (
                        <Col span={24}>
                          <PlanarianButton
                            icon={<RedoOutlined />}
                            onClick={async () => {
                              try {
                                await logout();
                                window.location.assign("/login");
                              } catch (e) {
                                const error = e as ApiErrorResponse;
                                message.error(
                                  error.message ?? "Failed to log out."
                                );
                              }
                            }}
                          >
                            Login
                          </PlanarianButton>
                        </Col>
                      )}
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

const ThemeConsumerWrapper: React.FC = () => {
  const { effectiveMode } = useTheme();

  return (
    <ConfigProvider
      theme={{
        algorithm:
          effectiveMode === "dark"
            ? antdTheme.darkAlgorithm
            : antdTheme.defaultAlgorithm,
      }}
    >
      <AppContent />
    </ConfigProvider>
  );
};

const App: React.FC = () => {
  return (
    <ThemeProvider>
      <ThemeConsumerWrapper />
    </ThemeProvider>
  );
};

export default App;
