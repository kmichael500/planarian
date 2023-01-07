import { MenuProps } from "antd";
import { DatabaseOutlined, SettingOutlined } from "@ant-design/icons";

type MenuItem = Required<MenuProps>["items"][number];

function getItem(
  label: React.ReactNode,
  key: React.Key,
  icon?: React.ReactNode,
  children?: MenuItem[],
  style?: React.CSSProperties
): MenuItem {
  return {
    key,
    icon,
    children,
    label,
    style,
  } as MenuItem;
}

export const SidebarMenuItems: MenuItem[] = [
  getItem("Projects", "2", <DatabaseOutlined />),
  getItem("Settings", "3", <SettingOutlined />),
];
