import { LogoutOutlined, SwapOutlined, UserOutlined } from "@ant-design/icons";
import { PlanarianMenuItem } from "./PlanarianMenuComponent";

const ProfileMenuItems = () => {
  const menuItems = [
    {
      key: "profile",
      icon: <UserOutlined />,
      label: "Profile",
    },
    {
      key: "switch-account",
      icon: <SwapOutlined />,
      label: "Switch Account",
    },
    {
      key: "logout",
      icon: <LogoutOutlined />,
      label: "Logout",
    },
  ] as PlanarianMenuItem[];

  return menuItems;
};
