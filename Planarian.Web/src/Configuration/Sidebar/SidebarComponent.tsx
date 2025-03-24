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
  const [userCollapsed, setUserCollapsed] = useState(false);
  const screens = useBreakpoint();
  const isLargeScreen = !!screens.lg;
  const collapsed = isLargeScreen ? userCollapsed : true;

  return (
    <Sider
      className="sidebar"
      breakpoint="lg"
      collapsedWidth={isLargeScreen ? 100 : 0}
      collapsible
      theme="light"
      collapsed={collapsed}
      onCollapse={(value, type) => {
        // Only update the user's setting if the collapse was triggered by a click.
        if (type === "clickTrigger") {
          setUserCollapsed(value);
        }
      }}
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
          <LogoIcon style={{ width: "90px", paddingTop: "20px" }} />
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
