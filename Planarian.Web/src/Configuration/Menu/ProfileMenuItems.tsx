import { LogoutOutlined, SwapOutlined, UserOutlined } from "@ant-design/icons";
import { PlanarianMenuItem } from "./PlanarianMenuComponent";
import { AppOptions } from "../../Shared/Services/AppService";

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
      isVisible: AppOptions.accountIds.length > 1,
    },
    {
      key: "logout",
      icon: <LogoutOutlined />,
      label: "Logout",
    },
  ] as PlanarianMenuItem[];

  return menuItems;
};
