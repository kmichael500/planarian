import React from "react";
import { Row, Col, Typography } from "antd";

interface DividerProps {
  title: string | React.ReactElement;
  secondaryTitle?: string;
  element?: React.ReactElement;
}

const PlanarianDividerComponent = ({
  title,
  secondaryTitle,
  element,
}: DividerProps) => {
  return (
    <>
      <br />
      <Row
        style={{ borderBottom: "1px solid #f0f0f0", paddingBottom: "5px" }}
        align="middle"
      >
        <Col>
          <Typography.Title level={5} style={{ marginBottom: 0 }}>
            {title}
          </Typography.Title>
        </Col>
        {secondaryTitle && (
          <Col>
            <Typography.Text
              type="secondary"
              style={{ fontSize: "12px", marginLeft: "10px" }}
            >
              {secondaryTitle}
            </Typography.Text>
          </Col>
        )}
        <Col flex="auto"></Col>
        <Col>{element}</Col>
      </Row>
      <br />
    </>
  );
};

export { PlanarianDividerComponent };
