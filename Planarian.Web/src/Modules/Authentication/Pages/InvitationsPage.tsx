import { Card, Empty, List, Space, Typography, message } from "antd";
import {
  CheckCircleOutlined,
  EnvironmentOutlined,
} from "@ant-design/icons";
import React from "react";
import { useContext, useEffect, useState } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { PlanarianTag } from "../../../Shared/Components/Display/PlanarianTag";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { AcceptInvitationVm } from "../../User/Models/AcceptInvitationVm";
import { UserService } from "../../User/UserService";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";
import { AuthenticationService } from "../Services/AuthenticationService";

const { Text, Title } = Typography;

const InvitationsPage = () => {
  const [invitations, setInvitations] = useState<AcceptInvitationVm[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [acceptingInvitationCode, setAcceptingInvitationCode] = useState<
    string | null
  >(null);
  const [decliningInvitationCode, setDecliningInvitationCode] = useState<
    string | null
  >(null);
  const { setHeaderTitle, setHeaderButtons, refreshPendingInvitations } =
    useContext(AppContext);

  useEffect(() => {
    setHeaderTitle(["Invitations"]);
    setHeaderButtons([]);
  }, [setHeaderButtons, setHeaderTitle]);

  const getInvitations = async () => {
    try {
      const result = await UserService.GetPendingInvitations();
      setInvitations(result);
    } catch (err) {
      const error = err as ApiErrorResponse;
      message.error(error.message);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    getInvitations();
  }, []);

  const handleAccept = async (invitation: AcceptInvitationVm) => {
    try {
      setAcceptingInvitationCode(invitation.invitationCode);
      await UserService.AcceptInvitation(invitation.invitationCode);
      message.success("You have accepted the invitation.");
      await refreshPendingInvitations();
      AuthenticationService.SwitchAccount(invitation.accountId, "/caves");
    } catch (err) {
      const error = err as ApiErrorResponse;
      message.error(error.message);
    } finally {
      setAcceptingInvitationCode(null);
    }
  };

  const handleDecline = async (invitation: AcceptInvitationVm) => {
    try {
      setDecliningInvitationCode(invitation.invitationCode);
      await UserService.DeclineInvitation(invitation.invitationCode);
      message.warning("You have declined the invitation.");
      await getInvitations();
      await refreshPendingInvitations();
    } catch (err) {
      const error = err as ApiErrorResponse;
      message.error(error.message);
    } finally {
      setDecliningInvitationCode(null);
    }
  };

  return (
    <div style={styles.container}>
      <Title level={3}>Pending Invitations</Title>
      <List
        loading={isLoading}
        dataSource={invitations}
        locale={{
          emptyText: (
            <Empty
              description="No pending invitations were found for your email."
              image={Empty.PRESENTED_IMAGE_SIMPLE}
            />
          ),
        }}
        renderItem={(invitation) => (
          <List.Item>
            <Card style={styles.card} styles={{ body: styles.cardBody }}>
              <Space
                direction="vertical"
                size="middle"
                style={{ width: "100%" }}
              >
                <div>
                  <Title level={4} style={{ marginBottom: 0 }}>
                    {invitation.accountName}
                  </Title>
                  <Text type="secondary">
                    Invited as {invitation.firstName} {invitation.lastName}
                  </Text>
                </div>

                <Space size={[8, 8]} wrap>
                  {invitation.regions.map((region) => (
                    <PlanarianTag
                      key={region}
                      color="blue"
                      icon={<EnvironmentOutlined />}
                    >
                      {region}
                    </PlanarianTag>
                  ))}
                </Space>

                <Space size={[8, 8]} wrap>
                  <PlanarianButton
                    type="primary"
                    icon={<CheckCircleOutlined />}
                    onClick={() => handleAccept(invitation)}
                    loading={
                      acceptingInvitationCode === invitation.invitationCode
                    }
                    alwaysShowChildren
                  >
                    Accept
                  </PlanarianButton>
                  <DeleteButtonComponent
                    loading={
                      decliningInvitationCode === invitation.invitationCode
                    }
                    title="Are you sure you want to decline the invitation?"
                    onConfirm={() => handleDecline(invitation)}
                    okText="Yes"
                    cancelText="No"
                    alwaysShowChildren
                  >
                    Decline
                  </DeleteButtonComponent>
                </Space>
              </Space>
            </Card>
          </List.Item>
        )}
      />
    </div>
  );
};

const styles: { [key: string]: React.CSSProperties } = {
  container: {
    maxWidth: 720,
    margin: "0 auto",
  },
  card: {
    width: "100%",
    borderRadius: 8,
  },
  cardBody: {
    padding: 16,
  },
};

export { InvitationsPage };
