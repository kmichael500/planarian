import { Grid, Layout, Space, Typography } from "antd";
import React, { useState } from "react";
import { Link } from "react-router-dom";
import { LogoIcon } from "./AppIcon";
import "./SidebarComponent.scss";
import { PlanarianMenuComponent } from "../Menu/PlanarianMenuComponent";
import { SideBarMenuItems } from "../Menu/SidebarMenuItems";

const { Sider } = Layout;
const { useBreakpoint } = Grid;

const SideBarComponent: React.FC = () => {
  const [collapsed, setCollapsed] = useState(true);

  const screens = useBreakpoint();
  const isLargeScreenSize = Object.entries(screens).some(
    ([key, value]) => value && (key === "lg" || key === "xl")
  );

  return (
    <Sider
      className="sidebar"
      breakpoint={"lg"}
      collapsedWidth={isLargeScreenSize ? 100 : 0}
      collapsible
      theme="light"
      collapsed={collapsed}
      onCollapse={(value) => setCollapsed(value)}
      style={{
        position: "sticky",
        top: 0,
        zIndex: 1,
        width: "100%",
        height: "100vh",
      }}
    >
      <Space
        direction="horizontal"
        style={{ width: "100%", justifyContent: "center" }}
      >
        <Link to="/">
          <LogoIcon style={{ padding: "10px", fontSize: "50px" }} />
        </Link>
      </Space>
      <Space
        direction="horizontal"
        style={{
          width: "100%",
          fontWeight: "lighter",
          justifyContent: "center",
        }}
      >
        <Typography.Text>Planarian</Typography.Text>
      </Space>
      <PlanarianMenuComponent menuItems={[...SideBarMenuItems()]} />
    </Sider>
  );
};

export { SideBarComponent };
