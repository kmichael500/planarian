import { useContext, useState } from "react";
import {
  Menu,
  Dropdown,
  Avatar,
  message,
  Modal,
  List,
  Button,
  Form,
  Select,
} from "antd";
import { UserOutlined, SwapOutlined, LogoutOutlined } from "@ant-design/icons";
import { AppOptions } from "../../Shared/Services/AppService";
import styled from "styled-components";
import { useNavigate, useLocation } from "react-router-dom";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";
import {
  PlanarianMenuComponent,
  PlanarianMenuItem,
} from "./PlanarianMenuComponent";
import { AppContext } from "../Context/AppContext";
import { SwitchAccountComponent } from "../../Modules/Authentication/Components/SwitchAccountComponent";
type ProfileMenuProps = {
  user: {
    firstName: string;
    lastName: string;
  };
};

function ProfileMenu({ user }: ProfileMenuProps) {
  const getUserInitials = () => {
    return `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`;
  };

  const navigate = useNavigate();
  const { isAuthenticated, setIsAuthenticated } = useContext(AppContext);

  const [isModalOpen, setIsModalOpen] = useState(false);

  const menuItems = [
    {
      key: "/profile",
      icon: <UserOutlined />,
      label: "Profile",
      requiresAuthentication: true,
    },
    {
      key: "switch-account",
      icon: <SwapOutlined />,
      label: "Switch Account",
      requiresAuthentication: true,
      action: () => {
        setIsModalOpen(true);
      },
    },
    {
      key: "logout",

      icon: <LogoutOutlined />,
      action: () => {
        AuthenticationService.Logout();
        setIsAuthenticated(false);
        navigate("/login");
      },
      label: "Logout",
      requiresAuthentication: true,
    },
  ] as PlanarianMenuItem[];

  return (
    <>
      <SwitchAccountComponent
        isVisible={isModalOpen}
        handleCancel={() => setIsModalOpen(false)}
      />
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

export default ProfileMenu;
