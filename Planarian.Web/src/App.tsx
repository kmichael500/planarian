import { theme as antdTheme } from "antd";
import { Col, ConfigProvider, Layout, Row, Spin } from "antd";
import { RedoOutlined } from "@ant-design/icons";
import React from "react";
import { Helmet } from "react-helmet";
import { BrowserRouter } from "react-router-dom";
import "./App.css";
import { AppContext, AppProvider } from "./Configuration/Context/AppContext";
import { HeaderComponent } from "./Configuration/Header/HeaderComponent";
import { LogoIcon } from "./Configuration/Sidebar/AppIcon";
import { SideBarComponent } from "./Configuration/Sidebar/SidebarComponent";
import { AppRouting } from "./Configuration/Routing/App.routing";
import { AuthenticationService } from "./Modules/Authentication/Services/AuthenticationService";
import { SyncfusionThemeManager } from "./SyncfusionThemeManager";
import { PlanarianButton } from "./Shared/Components/Buttons/PlanarianButtton";
import { ApiExceptionType } from "./Shared/Models/ApiErrorResponse";
import { ThemeProvider, useTheme } from "./ThemeProvider";

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
            {({ isInitialized, isLoading, initializedError, contentStyle }) =>
              isInitialized ? (
                <Layout style={{ minHeight: "calc(var(--vh, 1vh) * 100)" }}>
                  <SideBarComponent />
                  <Layout className="site-layout">
                    <HeaderComponent />
                    <Content style={contentStyle ?? {}}>
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
                            onClick={() => {
                              AuthenticationService.ResetAccountId();
                              AuthenticationService.Logout();
                              window.location.reload();
                            }}
                          >
                            Login
                          </PlanarianButton>{" "}
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

const App: React.FC = () => {
  return (
    <ThemeProvider>
      <ThemeConsumerWrapper />
    </ThemeProvider>
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

export default App;
