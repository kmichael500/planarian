import { Col, Drawer, Grid, Row, Spin, Typography } from "antd";
import { Header } from "antd/lib/layout/layout";
import { useContext, useEffect, useState } from "react";
import { MenuOutlined } from "@ant-design/icons";
import { Helmet } from "react-helmet";
import { isNullOrWhiteSpace } from "../../Shared/Helpers/StringHelpers";
import { AppContext } from "../Context/AppContext";
import { MenuComponent } from "../Menu/MenuComponent";
import { PlanarianButton } from "../../Shared/Components/Buttons/PlanarianButtton";

const { useBreakpoint } = Grid;

const HeaderComponent: React.FC = () => {
  const { headerTitle, headerButtons } = useContext(AppContext);
  const [hasHeaderButons, setHasHeaderButons] = useState<boolean>(false);

  const screens = useBreakpoint();
  const isLargeScreenSize = Object.entries(screens).some(
    ([key, value]) => value && (key === "lg" || key === "xl")
  );
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
              : "16px",
          paddingBottom: "4px",
          paddingRight: "16px",
          paddingLeft: "16px",
          height: "70px",
          background: "white",
          position: "sticky",
          top: 0,
          zIndex: 1,
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
                title="Planrian"
                placement="left"
                onClose={() => setVisible(false)}
                open={visible}
              >
                <MenuComponent
                  onMenuItemClick={(key) => {
                    setVisible(false);
                  }}
                />
              </Drawer>
            </Col>
            <Col>
              <>
                {typeof headerTitle[0] == "string" && (
                  <Typography.Title level={4}>{headerTitle}</Typography.Title>
                )}
                {typeof headerTitle[0] != "string" && headerTitle[0]}
              </>
            </Col>

            {/* take up rest of space to push others to right and left side */}
            <Col flex="auto"></Col>
            {headerButtons.map((button, index) => (
              <Col key={index}>{button}</Col>
            ))}
          </Row>
        </Spin>
      </Header>
    </>
  );
};

export { HeaderComponent };
