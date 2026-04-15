import { useContext, useState } from "react";
import { Dropdown, Avatar, message } from "antd";
import { UserOutlined, SwapOutlined, LogoutOutlined } from "@ant-design/icons";
import { useNavigate } from "react-router-dom";
import {
  PlanarianMenuComponent,
  PlanarianMenuItem,
} from "./PlanarianMenuComponent";
import { AppContext } from "../Context/AppContext";
import { SwitchAccountComponent } from "../../Modules/Authentication/Components/SwitchAccountComponent";
import { StringHelpers } from "../../Shared/Helpers/StringHelpers";
import { ApiErrorResponse } from "../../Shared/Models/ApiErrorResponse";

function ProfileMenu() {
  const navigate = useNavigate();
  const {
    accountIds,
    currentAccountId,
    currentUser,
    isAuthenticated,
    logout,
  } = useContext(AppContext);

  const [isModalOpen, setIsModalOpen] = useState(false);

  const getUserInitials = () =>
    `${StringHelpers.GenerateAbbreviation(currentUser?.fullName ?? "")}`;

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
      isVisible: accountIds.length > 1,
      action: () => {
        setIsModalOpen(true);
      },
    },
    {
      key: "logout",

      icon: <LogoutOutlined />,
      action: async () => {
        try {
          await logout();
          navigate("/login", { replace: true });
        } catch (e) {
          const error = e as ApiErrorResponse;
          message.error(error.message);
        }
      },
      label: "Logout",

      requiresAuthentication: true,
    },
  ] as PlanarianMenuItem[];

  return (
    <>
      {currentAccountId && (
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
