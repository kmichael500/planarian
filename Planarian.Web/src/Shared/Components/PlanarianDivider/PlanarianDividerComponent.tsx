import React from "react";
import { Row, Col, Typography } from "antd";
interface DividerProps {
  title: string;
  element?: React.ReactElement;
}
const PlanarianDividerComponent = ({ title, element }: DividerProps) => {
  return (
    <>
      <br />
      <Row style={{ borderBottom: "1px solid #f0f0f0", paddingBottom: "5px" }}>
        <Col>
          <Typography.Title level={5}>{title}</Typography.Title>
        </Col>
        <Col flex="auto"></Col>
        <Col>{element}</Col>
      </Row>
      <br />
    </>
  );
};

export { PlanarianDividerComponent };
