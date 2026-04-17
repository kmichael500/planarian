import { useContext, useRef, useState } from "react";
import { Dropdown, Avatar } from "antd";
import {
  UserOutlined,
  SwapOutlined,
  LogoutOutlined,
  BgColorsOutlined,
} from "@ant-design/icons";
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
import { useTheme } from "../../ThemeProvider";

function ProfileMenu() {
  const { modeLabel, cycleMode } = useTheme();
  const getUserInitials = () => {
    const name = AuthenticationService.GetName();
    return `${StringHelpers.GenerateAbbreviation(name ?? "")}`;
  };

  const hasAccount = !isNullOrWhiteSpace(AuthenticationService.GetAccountId());

  const navigate = useNavigate();
  const { isAuthenticated, setIsAuthenticated } = useContext(AppContext);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const keepThemePreviewOpenRef = useRef(false);

  const menuItems = [
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
          open={isDropdownOpen}
          onOpenChange={(open) => {
            // Keep the menu open while cycling themes so the user can preview
            // each mode in place without reopening the dropdown.
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
