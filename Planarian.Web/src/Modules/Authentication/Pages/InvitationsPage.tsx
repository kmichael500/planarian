import { Card, Empty, List, Space, Typography, message } from "antd";
import { CheckCircleOutlined, EnvironmentOutlined } from "@ant-design/icons";
import React from "react";
import { useContext, useEffect, useRef, useState } from "react";
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
  const invitationActionInFlightRef = useRef(false);
  const { setHeaderTitle, setHeaderButtons, refreshPendingInvitations } =
    useContext(AppContext);

  useEffect(() => {
    setHeaderTitle(["Invitations"]);
    setHeaderButtons([]);
  }, [setHeaderButtons, setHeaderTitle]);

  useEffect(() => {
    let isCurrent = true;

    const getInvitations = async () => {
      try {
        const result = await UserService.GetPendingInvitations();
        if (!isCurrent) return;
        setInvitations(result);
      } catch (err) {
        if (!isCurrent) return;
        const error = err as ApiErrorResponse;
        message.error(error.message);
      } finally {
        if (isCurrent) {
          setIsLoading(false);
        }
      }
    };

    void getInvitations();

    return () => {
      isCurrent = false;
    };
  }, []);

  const handleAccept = async (invitation: AcceptInvitationVm) => {
    if (invitationActionInFlightRef.current) return;

    invitationActionInFlightRef.current = true;
    setAcceptingInvitationCode(invitation.invitationCode);
    try {
      await UserService.AcceptInvitation(invitation.invitationCode);
      message.success("You have accepted the invitation.");
      AuthenticationService.SwitchAccount(invitation.accountId, "/caves");
    } catch (err) {
      const error = err as ApiErrorResponse;
      message.error(error.message);
    } finally {
      invitationActionInFlightRef.current = false;
      setAcceptingInvitationCode(null);
    }
  };

  const handleDecline = async (invitation: AcceptInvitationVm) => {
    if (invitationActionInFlightRef.current) return;

    invitationActionInFlightRef.current = true;
    setDecliningInvitationCode(invitation.invitationCode);
    try {
      await UserService.DeclineInvitation(invitation.invitationCode);
      setInvitations((current) =>
        current.filter(
          (item) => item.invitationCode !== invitation.invitationCode
        )
      );
      message.warning("You have declined the invitation.");
      void refreshPendingInvitations().catch(() => {});
    } catch (err) {
      const error = err as ApiErrorResponse;
      message.error(error.message);
    } finally {
      invitationActionInFlightRef.current = false;
      setDecliningInvitationCode(null);
    }
  };

  const isInvitationActionInFlight =
    acceptingInvitationCode != null || decliningInvitationCode != null;

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
                    disabled={isInvitationActionInFlight}
                    alwaysShowChildren
                  >
                    Accept
                  </PlanarianButton>
                  <DeleteButtonComponent
                    loading={
                      decliningInvitationCode === invitation.invitationCode
                    }
                    disabled={isInvitationActionInFlight}
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
