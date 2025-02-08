import React, { useState } from "react";
import { Card, Typography, Button, Space, Tag, message } from "antd";
import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  UserAddOutlined,
  LoginOutlined,
  EnvironmentOutlined,
} from "@ant-design/icons";
import { useNavigate } from "react-router-dom"; // Add this import for navigation
import { AuthenticationService } from "../Services/AuthenticationService";
import { AcceptInvitationVm } from "../../User/Models/AcceptInvitationVm";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { UserService } from "../../User/UserService";
import { SwitchAccountComponent } from "./SwitchAccountComponent";

const { Title, Text } = Typography;

const isLoggedIn = AuthenticationService.IsAuthenticated();
interface InvitationComponentProps {
  invitation?: AcceptInvitationVm;
  invitationCode: string;
  isLoading: boolean;
  updateInvitation: () => Promise<void>;
}

const InvitationComponent = ({
  invitation,
  isLoading,
  updateInvitation,
  invitationCode,
}: InvitationComponentProps) => {
  const [status, setStatus] = useState<"pending" | "accepted" | "declined">(
    "pending"
  );

  const navigate = useNavigate();

  const [isDeclining, setIsDeclining] = useState<boolean>(false);
  const [isAccepting, setIsAccepting] = useState<boolean>(false);

  const [showSwitchAccountModal, setShowSwitchAccountModal] = useState(false);

  const handleAccept = async () => {
    try {
      setIsAccepting(true);
      await UserService.AcceptInvitation(invitationCode);
      setStatus("accepted");
      message.success("You have accepted the invitation.");
    } catch (error) {
      const err = error as ApiErrorResponse;
      message.error(err.message);
    }
    setIsAccepting(false);
  };

  const handleDecline = async () => {
    try {
      setIsDeclining(true);
      await UserService.DeclineInvitation(invitationCode);
      setStatus("declined");
      message.warning("You have declined the invitation.");
    } catch (error) {
      const err = error as ApiErrorResponse;
      message.error(err.message);
    }
    setIsDeclining(false);
  };
  const handleCreateAccount = () => {
    navigate(`/register?invitationCode=${invitationCode}`);
  };

  const handleExistingAccount = () => {
    navigate(`/login?invitationCode=${invitationCode}`);
  };

  let invitationMessage = `You’ve been invited to access ${invitation?.accountName} data on
  Planarian.`;

  if (isLoggedIn) {
    invitationMessage += " Accept the invitation to continue!";
  } else {
    invitationMessage +=
      " Create an account or login to accept the invitation.";
  }

  if (!isLoading && !invitation) {
    return (
      <div style={styles.container}>
        <Card style={styles.card} bordered={false}>
          <Title level={3} style={{ marginBottom: 10 }}>
            Invitation Not Found
          </Title>
          <Text>
            We could not find an invitation with the provided code. Please check
            the code and try again.
          </Text>
        </Card>
      </div>
    );
  }

  return (
    <div style={styles.container}>
      {status === "pending" ? (
        <Card loading={isLoading} style={styles.card} bordered={false}>
          <Title level={3} style={{ marginBottom: 10 }}>
            {invitation?.accountName}
          </Title>
          <Text>{invitationMessage}</Text>

          <div style={styles.section}>
            <Text strong>Regions:</Text>
            <div style={styles.tags}>
              {invitation?.regions.map((region) => (
                <Tag
                  key={region}
                  color="blue"
                  icon={<EnvironmentOutlined />}
                  style={styles.tag}
                >
                  {region}
                </Tag>
              ))}
            </div>
          </div>

          {isLoggedIn ? (
            <Space style={{ marginTop: 20 }}>
              <Button
                type="primary"
                icon={<CheckCircleOutlined />}
                onClick={handleAccept}
                loading={isAccepting}
              >
                Accept
              </Button>
              <Button
                type="default"
                icon={<CloseCircleOutlined />}
                danger
                onClick={handleDecline}
                loading={isDeclining}
              >
                Decline
              </Button>
            </Space>
          ) : (
            <div style={styles.buttonStack}>
              <Button
                type="primary"
                icon={<UserAddOutlined />}
                block
                onClick={handleCreateAccount}
              >
                Create a New Account
              </Button>
              <Button
                type="default"
                icon={<LoginOutlined />}
                block
                onClick={handleExistingAccount}
              >
                I Have an Account
              </Button>
              <Button
                type="default"
                icon={<CloseCircleOutlined />}
                danger
                block
                onClick={handleDecline}
              >
                Decline
              </Button>
            </div>
          )}
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
          {status === "accepted" && (
            <Button
              type="primary"
              onClick={() => setShowSwitchAccountModal(true)}
              style={{ marginTop: 20 }}
            >
              Switch Account
            </Button>
          )}
          <SwitchAccountComponent
            isVisible={showSwitchAccountModal}
            handleCancel={function (): void {
              setShowSwitchAccountModal(false);
            }}
          />
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
  buttonStack: {
    marginTop: 20,
    display: "flex",
    flexDirection: "column",
    gap: "10px",
  },
};

export { InvitationComponent };
