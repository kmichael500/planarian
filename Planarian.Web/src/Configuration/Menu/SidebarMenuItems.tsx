import { Divider } from "antd";
import { Link } from "react-router-dom";
import { PlanarianMenuItem } from "./PlanarianMenuComponent";
import {
  LoginOutlined,
  UserAddOutlined,
  DatabaseOutlined,
  SettingOutlined,
  ImportOutlined,
  CompassOutlined,
  UserOutlined,
} from "@ant-design/icons";
import { PermissionKey } from "../../Modules/Authentication/Models/PermissionKey";
import { useContext, useMemo } from "react";
import { AppContext } from "../Context/AppContext";

const useSideBarMenuItems = () => {
  const { currentAccountId } = useContext(AppContext);
  const hasAccount = currentAccountId != null;

  return useMemo(() => {
    const authenticatedMenuItems = [
      {
        key: "/caves",
        icon: (
          <Link to="/caves">
            <DatabaseOutlined />
          </Link>
        ),
        label: "Caves",
        isVisible: hasAccount,
      },
      {
        key: "/map",
        icon: (
          <Link to="/map">
            <CompassOutlined />
          </Link>
        ),
        label: "Map",
        isVisible: hasAccount,
      },
      {
        key: "/account",
        icon: <SettingOutlined />,
        label: "Account",
        isVisible: hasAccount,
        permissionKey: PermissionKey.Manager,
        children: [
          {
            key: "/account/import",
            icon: (
              <Link to="/caves/import">
                <ImportOutlined />
              </Link>
            ),
            label: "Import",
            permissionKey: PermissionKey.Admin,
          },
          {
            key: "/account/users",
            icon: (
              <Link to="/account/users">
                <UserOutlined />
              </Link>
            ),
            label: "Users",
            permissionKey: PermissionKey.Manager,
          },
          {
            key: "/account/settings",
            icon: <SettingOutlined />,
            label: "Settings",
            permissionKey: PermissionKey.Admin,
          },
        ],
      },
      {
        icon: <Divider />,
        isVisible: false,
      },
      {
        isVisible: false,
        key: "/projects",
        icon: (
          <Link to="/projects">
            <DatabaseOutlined />
          </Link>
        ),
        label: "Projects",
      },
    ] as PlanarianMenuItem[];

    const unauthenticatedMenuItems = [
      {
        key: "/login",
        icon: (
          <Link to="/login">
            <LoginOutlined />
          </Link>
        ),
        label: "Login",
      },
      {
        key: "/register",
        icon: (
          <Link to="/register">
            <UserAddOutlined />
          </Link>
        ),
        label: "Register",
      },
    ] as PlanarianMenuItem[];

    unauthenticatedMenuItems.forEach((element) => {
      element.requiresAuthentication = false;
    });

    authenticatedMenuItems.forEach((element) => {
      element.requiresAuthentication = true;

      if (element.children) {
        element.children.forEach((child) => {
          child.requiresAuthentication = true;
        });
      }
    });

    return [...authenticatedMenuItems, ...unauthenticatedMenuItems];
  }, [hasAccount]);
};

export { useSideBarMenuItems };
