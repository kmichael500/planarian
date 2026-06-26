import {
  BgColorsOutlined,
  LogoutOutlined,
  MailOutlined,
  SwapOutlined,
  UserOutlined,
} from "@ant-design/icons";
import { Avatar, Badge, Dropdown, message } from "antd";
import { useContext, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { AppContext } from "../Context/AppContext";
import { SwitchAccountComponent } from "../../Modules/Authentication/Components/SwitchAccountComponent";
import { ApiErrorResponse } from "../../Shared/Models/ApiErrorResponse";
import {
  PlanarianMenuComponent,
  PlanarianMenuItem,
} from "./PlanarianMenuComponent";
import { StringHelpers } from "../../Shared/Helpers/StringHelpers";
import { useTheme } from "../../ThemeProvider";

function ProfileMenu() {
  const navigate = useNavigate();
  const { modeLabel, cycleMode } = useTheme();
  const {
    accountIds,
    currentAccountId,
    currentUser,
    isAuthenticated,
    logout,
    pendingInvitationCount,
  } = useContext(AppContext);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const keepThemePreviewOpenRef = useRef(false);

  const getUserInitials = () =>
    `${StringHelpers.GenerateAbbreviation(currentUser?.fullName ?? "")}`;

  const menuItems = [
    {
      key: "/user/invitations",
      icon: <MailOutlined />,
      label: `Invitations (${pendingInvitationCount})`,
      requiresAuthentication: true,
      isVisible: pendingInvitationCount > 0,
    },
    {
      key: "/user/profile",
      icon: <UserOutlined />,
      label: "Profile",
      requiresAuthentication: true,
    },
    {
      key: "theme",
      icon: <BgColorsOutlined />,
      label: `Theme: ${modeLabel}`,
      requiresAuthentication: true,
      action: () => {
        cycleMode();
      },
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
          message.error(error.message ?? "Failed to log out.");
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
          open={isDropdownOpen}
          onOpenChange={(open) => {
            if (!open && keepThemePreviewOpenRef.current) {
              keepThemePreviewOpenRef.current = false;
              return;
            }

            setIsDropdownOpen(open);
          }}
          overlay={
            <PlanarianMenuComponent
              mode="vertical"
              menuItems={menuItems}
              onMenuItemClick={(key) => {
                if (key === "theme") {
                  keepThemePreviewOpenRef.current = true;
                  setIsDropdownOpen(true);
                  return;
                }

                setIsDropdownOpen(false);
              }}
            />
          }
          trigger={["click"]}
        >
          <Badge
            count={pendingInvitationCount}
            style={{
              minWidth: 20,
              height: 20,
              lineHeight: "20px",
              fontSize: 12,
            }}
          >
            <Avatar
              size={40}
              style={{
                backgroundColor: "rgb(253, 244, 217)",
                color: "#3b3320",
                cursor: "pointer",
              }}
            >
              {getUserInitials()}
            </Avatar>
          </Badge>
        </Dropdown>
      )}
    </>
  );
}

export { ProfileMenu };
