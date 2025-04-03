import { FC } from "react";
import { Tag, Typography } from "antd";
import styled from "styled-components";
import { PUBLIC_ACCESS_INFO } from "./ProtectedAreaDetails";

const { Text } = Typography;

const LegendContent = styled.div`
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 4px;
`;

export const PublicAccessLegend: FC = () => {
  return (
    <>
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
    </>
  );
};
