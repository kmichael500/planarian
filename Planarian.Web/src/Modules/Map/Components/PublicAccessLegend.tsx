import { FC } from "react";
import { Tag, Typography } from "antd";
import styled from "styled-components";
import { PUBLIC_ACCESS_INFO } from "./ProtectedAreaDetails";

const { Text } = Typography;

const LegendContainer = styled.div`
  background: white;
  border: 1px solid #ddd;
  padding: 8px;
  font-size: 12px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.15);
  border-radius: 6px;
  position: absolute;
  bottom: 10px;
  left: 10px;
  z-index: 10;
  max-width: 220px;
`;

const LegendContent = styled.div`
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 4px;
`;

export const PublicAccessLegend: FC = () => {
  return (
    <LegendContainer>
      <Text strong style={{ fontSize: "12px" }}>
        Access
      </Text>
      <LegendContent>
        {Object.entries(PUBLIC_ACCESS_INFO).map(([code, info]) => (
          <Tag
            key={code}
            color={info.color}
            style={{ margin: 0, padding: "0 4px" }}
          >
            {info.label}
          </Tag>
        ))}
      </LegendContent>
      <Text
        type="secondary"
        style={{ fontSize: "10px", display: "block", marginTop: 4 }}
      >
        *Not indicative of caving permissions
      </Text>
    </LegendContainer>
  );
};
