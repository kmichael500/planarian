import { Col, Drawer, Grid, Row, Spin, Tag, Typography } from "antd";
import { Header } from "antd/lib/layout/layout";
import { useContext, useEffect, useState } from "react";
import { MenuOutlined } from "@ant-design/icons";
import { Helmet } from "react-helmet";
import {
  StringHelpers,
  isNullOrWhiteSpace,
} from "../../Shared/Helpers/StringHelpers";
import { AppContext } from "../Context/AppContext";
import { PlanarianButton } from "../../Shared/Components/Buttons/PlanarianButtton";
import { ProfileMenu } from "../Menu/ProfileMenuComponent";
import { PlanarianMenuComponent } from "../Menu/PlanarianMenuComponent";
import { SideBarMenuItems } from "../Menu/SidebarMenuItems";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";

const { useBreakpoint } = Grid;

const HeaderComponent = () => {
  const { headerTitle, headerButtons } = useContext(AppContext);
  const [hasHeaderButons, setHasHeaderButons] = useState<boolean>(false);

  const isAuthenticated = AuthenticationService.IsAuthenticated();
  const hasAccount = !isNullOrWhiteSpace(AuthenticationService.GetAccountId());

  const screens = useBreakpoint();
  const isLargeScreenSize = Object.entries(screens).some(
    ([key, value]) => value && (key === "lg" || key === "xl")
  );

  const isXsScreen = screens.xs === true && !screens.sm;

  useEffect(() => {
    if (headerButtons.length === 0) {
      setHasHeaderButons(false);
    } else {
      setHasHeaderButons(true);
    }
  }, [headerButtons]);

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

  return (
    <>
      <Helmet>
        <title>{navigationTitle} | Planarian</title>
      </Helmet>

      <Header
        style={{
          paddingTop:
            hasHeaderButons || (!hasHeaderButons && !isLargeScreenSize)
              ? "4px"
              : "4px", // this used to be 16px but caused a bug in the header with the position of the icons
          paddingBottom: "4px",
          paddingRight: "16px",
          paddingLeft: "16px",
          height: "70px",
          background: "white",
          position: "sticky",
          top: 0,
          zIndex: 1000,
          width: "100%",
          border: "1px solid #f0f0f0",
        }}
      >
        <Spin
          spinning={
            headerTitle[0] == null ||
            (typeof headerTitle[0] === "string" &&
              isNullOrWhiteSpace(headerTitle[0]))
          }
        >
          <Row align="middle" gutter={10}>
            <Col>
              {!isLargeScreenSize && (
                <PlanarianButton
                  className="menu"
                  type="primary"
                  icon={<MenuOutlined />}
                  onClick={() => setVisible(true)}
                />
              )}

              <Drawer
                bodyStyle={{ padding: 0 }}
                title="Planarian"
                placement="left"
                onClose={() => setVisible(false)}
                open={visible}
              >
                <PlanarianMenuComponent
                  onMenuItemClick={(key) => {
                    setVisible(false);
                  }}
                  menuItems={[...SideBarMenuItems()]}
                />
              </Drawer>
            </Col>
            <Col flex="1 1 0" style={{ minWidth: 0 }}>
              <div
                style={{
                  overflow: "hidden",
                  whiteSpace: "nowrap",
                  textOverflow: "ellipsis",
                }}
              >
                {typeof headerTitle[0] === "string" ? (
                  <Typography.Title level={4} style={{ margin: 0 }}>
                    {headerTitle}
                  </Typography.Title>
                ) : (
                  headerTitle[0]
                )}
              </div>
            </Col>

            {hasAccount && (
              <Col style={{ flexShrink: 0 }}>
                <AccountNameTag />
              </Col>
            )}
            {headerButtons.map((button, index) => (
              <Col key={index}>{button}</Col>
            ))}
            {isAuthenticated && <ProfileMenu />}
          </Row>
        </Spin>
      </Header>
    </>
  );
};

const AccountNameTag = () => {
  let accountName = "";
  let accountNameAbbreviation = "";

  const screens = useBreakpoint();
  const isLargeScreenSize = Object.entries(screens).some(
    ([key, value]) => value && key === "xl"
  );

  const isNotXs = Object.entries(screens).some(
    ([key, value]) => value && key !== "xs"
  );

  accountName = AuthenticationService.GetAccountName();
  accountNameAbbreviation = StringHelpers.GenerateAbbreviation(accountName);

  return (
    <>
      {isNotXs && (
        <Tag style={{ whiteSpace: "nowrap" }}>
          {isLargeScreenSize ? accountName : accountNameAbbreviation}
        </Tag>
      )}
    </>
  );
};

export { HeaderComponent };
