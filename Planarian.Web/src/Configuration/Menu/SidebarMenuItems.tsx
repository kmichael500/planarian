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
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";
import { isNullOrWhiteSpace } from "../../Shared/Helpers/StringHelpers";
import { useState, useEffect } from "react";
import { PermissionKey } from "../../Modules/Authentication/Models/PermissionKey";

const SideBarMenuItems = () => {
  const [hasAccount, setHasAccount] = useState(
    !isNullOrWhiteSpace(AuthenticationService.GetAccountId())
  );

  useEffect(() => {
    // subscribe to authentication changes
    const unsubscribe = AuthenticationService.onAuthChange(() => {
      setHasAccount(!isNullOrWhiteSpace(AuthenticationService.GetAccountId()));
    });

    // clean up the subscription on component unmount
    return () => {
      unsubscribe();
    };
  }, []); // empty dependency array ensures this runs once on mount and once on unmount
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

    // do permissions
  });

  return [...authenticatedMenuItems, ...unauthenticatedMenuItems];
};

export { SideBarMenuItems };
