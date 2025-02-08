import React, { useState } from "react";
import { Card, Typography, Button, Space, Tag, message } from "antd";
import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  EnvironmentOutlined,
} from "@ant-design/icons";

const { Title, Text } = Typography;

const mockInvitation = {
  surveyName: "Southeastern Cave Survey",
  states: ["Tennessee"],
};

const InvitationComponent: React.FC = () => {
  const [status, setStatus] = useState<"pending" | "accepted" | "declined">(
    "pending"
  );

  const handleAccept = () => {
    setStatus("accepted");
    message.success("You have accepted the invitation.");
  };

  const handleDecline = () => {
    setStatus("declined");
    message.warning("You have declined the invitation.");
  };

  return (
    <div style={styles.container}>
      {status === "pending" ? (
        <Card style={styles.card} bordered={false}>
          <Title level={3} style={{ marginBottom: 10 }}>
            {mockInvitation.surveyName}
          </Title>
          <Text>
            You’ve been invited to access {mockInvitation.surveyName} data on
            Planarian. Accept the invitation to continue!
          </Text>

          <div style={styles.section}>
            <Text strong>Regions:</Text>
            <div style={styles.tags}>
              {mockInvitation.states.map((state) => (
                <Tag
                  key={state}
                  color="blue"
                  icon={<EnvironmentOutlined />}
                  style={styles.tag}
                >
                  {state}
                </Tag>
              ))}
            </div>
          </div>

          <Space style={{ marginTop: 20 }}>
            <Button
              type="primary"
              icon={<CheckCircleOutlined />}
              onClick={handleAccept}
              style={styles.acceptButton}
            >
              Accept
            </Button>
            <Button
              type="default"
              icon={<CloseCircleOutlined />}
              danger
              onClick={handleDecline}
            >
              Decline
            </Button>
          </Space>
        </Card>
      ) : (
        <Card style={styles.card} bordered={false}>
          <Title level={3} style={{ marginBottom: 10 }}>
            {status === "accepted" ? "Access Confirmed" : "Invitation Declined"}
          </Title>
          <Text>
            {status === "accepted"
              ? "You now have access to the cave location data."
              : "If you change your mind, contact the survey organization."}
          </Text>
        </Card>
      )}
    </div>
  );
};

const styles: { [key: string]: React.CSSProperties } = {
  container: {
    display: "flex",
    justifyContent: "center",
    alignItems: "center",
    height: "100vh",
    background: "linear-gradient(135deg, #f0f2f5, #e6f7ff)",
    padding: 20,
  },
  card: {
    width: 420,
    textAlign: "center",
    padding: 24,
    boxShadow: "0px 6px 12px rgba(0,0,0,0.1)",
    borderRadius: 10,
    background: "white",
  },
  section: {
    marginTop: 16,
    textAlign: "left",
  },
  tags: {
    marginTop: 8,
    display: "flex",
    flexWrap: "wrap",
    gap: "8px",
  },
  tag: {
    padding: "4px 10px",
    fontSize: "14px",
  },
  acceptButton: {
    backgroundColor: "#1890ff",
    borderColor: "#1890ff",
  },
};

export { InvitationComponent };
