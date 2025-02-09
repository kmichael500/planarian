import { useContext, useState } from "react";
import { Dropdown, Avatar } from "antd";
import { UserOutlined, SwapOutlined, LogoutOutlined } from "@ant-design/icons";
import { useNavigate } from "react-router-dom";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";
import {
  PlanarianMenuComponent,
  PlanarianMenuItem,
} from "./PlanarianMenuComponent";
import { AppContext } from "../Context/AppContext";
import { SwitchAccountComponent } from "../../Modules/Authentication/Components/SwitchAccountComponent";
import { AppOptions } from "../../Shared/Services/AppService";
import {
  StringHelpers,
  isNullOrWhiteSpace,
} from "../../Shared/Helpers/StringHelpers";

function ProfileMenu() {
  const getUserInitials = () => {
    const name = AuthenticationService.GetName();
    return `${StringHelpers.GenerateAbbreviation(name ?? "")}`;
  };

  const hasAccount = !isNullOrWhiteSpace(AuthenticationService.GetAccountId());

  const navigate = useNavigate();
  const { isAuthenticated, setIsAuthenticated } = useContext(AppContext);

  const [isModalOpen, setIsModalOpen] = useState(false);

  const menuItems = [
    {
      key: "/user/profile",
      icon: <UserOutlined />,
      label: "Profile",
      requiresAuthentication: true,
    },
    {
      key: "switch-account",
      icon: <SwapOutlined />,
      label: "Switch Account",
      requiresAuthentication: true,
      isVisible: AppOptions.accountIds.length > 1,
      action: () => {
        setIsModalOpen(true);
      },
    },
    {
      key: "logout",

      icon: <LogoutOutlined />,
      action: async () => {
        await AuthenticationService.Logout();
        setIsAuthenticated(false);
        navigate("/login");
      },
      label: "Logout",

      requiresAuthentication: true,
    },
  ] as PlanarianMenuItem[];

  return (
    <>
      {hasAccount && (
        <SwitchAccountComponent
          isVisible={isModalOpen}
          handleCancel={() => setIsModalOpen(false)}
        />
      )}
      {isAuthenticated && (
        <Dropdown
          overlay={
            <PlanarianMenuComponent mode="vertical" menuItems={menuItems} />
          }
          trigger={["click"]}
        >
          <Avatar
            size={40}
            style={{ backgroundColor: "#87d068", cursor: "pointer" }}
          >
            {getUserInitials()}
          </Avatar>
        </Dropdown>
      )}
    </>
  );
}

export { ProfileMenu };
