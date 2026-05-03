import { MenuOutlined } from "@ant-design/icons";
import { Drawer, Grid, Typography } from "antd";
import { Header } from "antd/lib/layout/layout";
import { CSSProperties, useContext, useEffect, useState } from "react";
import { Helmet } from "react-helmet";
import { AppContext } from "../Context/AppContext";
import { PlanarianMenuComponent } from "../Menu/PlanarianMenuComponent";
import { ProfileMenu } from "../Menu/ProfileMenuComponent";
import { useSideBarMenuItems } from "../Menu/SidebarMenuItems";
import { PlanarianButton } from "../../Shared/Components/Buttons/PlanarianButtton";
import { PlanarianTag } from "../../Shared/Components/Display/PlanarianTag";
import {
  StringHelpers,
  isNullOrWhiteSpace,
} from "../../Shared/Helpers/StringHelpers";
import "./HeaderComponent.scss";

const { useBreakpoint } = Grid;

const HeaderComponent = () => {
  const {
    currentAccountName,
    defaultContentStyle,
    headerTitle,
    headerButtons,
    isAuthenticated,
  } = useContext(AppContext);
  const menuItems = useSideBarMenuItems();
  const hasAccount = !isNullOrWhiteSpace(currentAccountName);

  const screens = useBreakpoint();
  const isLargeScreenSize = Object.entries(screens).some(
    ([key, value]) => value && (key === "lg" || key === "xl")
  );

  const [navigationTitle, setNavigationTitle] = useState("");
  useEffect(() => {
    if (headerTitle[1]) {
      setNavigationTitle(headerTitle[1]);
    } else if (typeof headerTitle[0] === "string") {
      setNavigationTitle(headerTitle[0]);
    } else {
      setNavigationTitle("Planarian");
    }
  }, [headerTitle]);

  const [visible, setVisible] = useState(false);
  const isHeaderTitleLoading =
    headerTitle[0] == null ||
    (typeof headerTitle[0] === "string" && isNullOrWhiteSpace(headerTitle[0]));
  const headerStyle = {
    "--planarian-header-content-spacing": defaultContentStyle.margin,
  } as CSSProperties;

  return (
    <>
      <Helmet>
        <title>{navigationTitle} | Planarian</title>
      </Helmet>

      <Header className="planarian-header" style={headerStyle}>
        <div className="planarian-header__inner">
          {!isLargeScreenSize && (
            <>
              <PlanarianButton
                className="planarian-header__menu-button menu"
                icon={<MenuOutlined />}
                onClick={() => setVisible(true)}
              />
              <Drawer
                bodyStyle={{ padding: 0 }}
                title="Planarian"
                placement="left"
                onClose={() => setVisible(false)}
                open={visible}
              >
                <PlanarianMenuComponent
                  onMenuItemClick={() => {
                    setVisible(false);
                  }}
                  menuItems={menuItems}
                />
              </Drawer>
            </>
          )}

          <div className="planarian-header__title">
            {typeof headerTitle[0] === "string" ? (
              <Typography.Title
                className="planarian-header__title-text"
                level={4}
              >
                {isHeaderTitleLoading ? "" : headerTitle[0]}
              </Typography.Title>
            ) : (
              <div className="planarian-header__title-text">
                {isHeaderTitleLoading ? null : headerTitle[0]}
              </div>
            )}
          </div>

          <div className="planarian-header__actions">
            {hasAccount && <AccountNameTag accountName={currentAccountName} />}
            {headerButtons.map((button, index) => (
              <div className="planarian-header__action" key={index}>
                {button}
              </div>
            ))}
            {isAuthenticated && <ProfileMenu />}
          </div>
        </div>
      </Header>
    </>
  );
};

const AccountNameTag = ({ accountName }: { accountName: string | null }) => {
  const screens = useBreakpoint();
  const isLargeScreenSize = Object.entries(screens).some(
    ([key, value]) => value && key === "xl"
  );

  const isNotXs = Object.entries(screens).some(
    ([key, value]) => value && key !== "xs"
  );

  if (isNullOrWhiteSpace(accountName)) {
    return null;
  }

  const accountNameAbbreviation = StringHelpers.GenerateAbbreviation(accountName);

  return (
    <>
      {isNotXs && (
        <PlanarianTag style={{ whiteSpace: "nowrap" }}>
          {isLargeScreenSize ? accountName : accountNameAbbreviation}
        </PlanarianTag>
      )}
    </>
  );
};

export { HeaderComponent };
