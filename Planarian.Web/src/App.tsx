import { Col, Layout, Row, Spin } from "antd";
import React from "react";
import "./App.css";
import { AppRouting } from "./Configuration/Routing/App.routing";
import { Helmet } from "react-helmet";
import { BrowserRouter, useNavigate } from "react-router-dom";
import { AppContext, AppProvider } from "./Configuration/Context/AppContext";
import { SideBarComponent } from "./Configuration/Sidebar/SidebarComponent";
import { HeaderComponent } from "./Configuration/Header/HeaderComponent";
import { LogoIcon } from "./Configuration/Sidebar/AppIcon";
import { ApiExceptionType } from "./Shared/Models/ApiErrorResponse";
import { PlanarianButton } from "./Shared/Components/Buttons/PlanarianButtton";
import { RedoOutlined } from "@ant-design/icons";
import { AuthenticationService } from "./Modules/Authentication/Services/AuthenticationService";

const { Content } = Layout;

const App: React.FC = () => {
  return (
    <>
      <Helmet>
        <title>Planarian</title>
        <meta name="description" content="Cave project management" />
      </Helmet>
      <BrowserRouter>
        <AppProvider>
          <AppContext.Consumer>
            {({ isInitialized, isLoading, initializedError, contentStyle }) =>
              isInitialized ? (
                <Layout style={{ minHeight: "100vh" }}>
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

export default App;
