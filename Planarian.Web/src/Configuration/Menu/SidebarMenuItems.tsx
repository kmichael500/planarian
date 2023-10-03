import { Divider } from "antd";
import { Link } from "react-router-dom";
import { PlanarianMenuItem } from "./PlanarianMenuComponent";
import {
  LoginOutlined,
  UserAddOutlined,
  DatabaseOutlined,
  SettingOutlined,
  ImportOutlined,
} from "@ant-design/icons";
import { AccountService } from "../../Modules/Account/Services/AccountService";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";
import { isNullOrWhiteSpace } from "../../Shared/Helpers/StringHelpers";

const SideBarMenuItems = () => {
  var hasAccount = !isNullOrWhiteSpace(AuthenticationService.GetAccountId());
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
      key: "/account",
      icon: <SettingOutlined />,
      label: "Account",
      isVisible: hasAccount,
      children: [
        {
          key: "/account/import",
          icon: (
            <Link to="/caves/import">
              <ImportOutlined />
            </Link>
          ),
          label: "Import",
        },
        {
          key: "/account/settings",
          icon: <SettingOutlined />,
          label: "Settings",
        },
      ],
    },
    {
      icon: <Divider />,
    },
    {
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
  });

  return [...authenticatedMenuItems, ...unauthenticatedMenuItems];
};

export { SideBarMenuItems };
