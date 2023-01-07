import { Layout, Menu } from "antd";
import React, { useState } from "react";
import "./App.css";

import { SidebarMenuItems } from "./Configuration/SidebarMenuItems";
import { AppRouting } from "./App.routing";
import Favicon from "react-favicon";
import logo from "./logo.svg";
import { Helmet } from "react-helmet";
const { Content, Footer, Sider } = Layout;

const App: React.FC = () => {
  const [collapsed, setCollapsed] = useState(true);

  return (
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
        <Menu
          theme="dark"
          defaultSelectedKeys={["1"]}
          mode="inline"
          items={SidebarMenuItems}
        />
      </Sider>
      <Layout className="site-layout">
        <Content style={{ margin: "16px 16px" }}>
          <AppRouting />
        </Content>
        <Footer style={{ textAlign: "center" }}></Footer>
      </Layout>
    </Layout>
  );
};

export default App;
