import { Card, Space, Typography } from "antd";
import { ReactNode } from "react";

export const CaveAuthoringAssetCard = ({
  title,
  ariaLabel,
  metadata,
  actions,
  status,
  children,
}: {
  title: string;
  ariaLabel?: string;
  metadata?: ReactNode;
  actions: ReactNode;
  status?: ReactNode;
  children?: ReactNode;
}) => (
  <div role="group" aria-label={ariaLabel ?? title}>
  <Card size="small">
    <Space direction="vertical" size="small" style={{ width: "100%" }}>
      <div style={{ display: "flex", gap: 8, justifyContent: "space-between", alignItems: "center", flexWrap: "wrap" }}>
        <Space size="small" wrap>
          <Typography.Text strong>{title}</Typography.Text>
          {metadata && <Typography.Text type="secondary">{metadata}</Typography.Text>}
        </Space>
        <Space size="small" wrap>{actions}</Space>
      </div>
      {status}
      {children}
    </Space>
  </Card>
  </div>
);
