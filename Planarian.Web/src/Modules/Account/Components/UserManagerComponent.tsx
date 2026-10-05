import React, { useContext, useEffect, useState } from "react";
import {
  Modal,
  Form,
  Input,
  message,
  Row,
  Col,
  Select,
  Typography,
} from "antd";
import {
  DeleteOutlined,
  EditOutlined,
  MailOutlined,
  RedoOutlined,
  UserAddOutlined,
} from "@ant-design/icons";
import { useNavigate } from "react-router-dom";

import { AccountUserManagerService } from "../Services/UserManagerService";
import { InviteUserRequest } from "../Models/InviteUserRequest";
import { InvitationEmailAttemptVm } from "../Models/InvitationEmailHistoryVm";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { PlanarianError } from "../../../Shared/Exceptions/PlanarianErrors";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { formatDateTime, nameof } from "../../../Shared/Helpers/StringHelpers";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import {
  GridCard,
  GridCardAction,
} from "../../../Shared/Components/CardGrid/GridCard";
import { SpinnerCardComponent } from "../../../Shared/Components/SpinnerCard/SpinnerCard";
import { EmailHistoryModal } from "../../../Shared/Components/EmailHistoryModal/EmailHistoryModal";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";
import { SplitSortControl } from "../../Search/Components/SplitSortControl";
import { ScrollCollapseSection } from "../../../Shared/Components/ScrollCollapseSection/ScrollCollapseSection";
import { useScrollRevealVisibility } from "../../../Shared/Scroll/useScrollRevealVisibility";
import { AppContext } from "../../../Configuration/Context/AppContext";
import "./UserManagerComponent.scss";

const { Text } = Typography;
const USER_MANAGER_COLLAPSE_BREAKPOINT_PX = 760;

type UserStatusFilter = "all" | "accepted" | "pending" | "revoked";
type UserSortBy =
  | "fullName"
  | "emailAddress"
  | "invitationSentOn"
  | "invitationAcceptedOn"
  | "lastActiveOn";

const UserManagerComponent: React.FC = () => {
  const [users, setUsers] = useState<UserManagerGridVm[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [inviteModalVisible, setInviteModalVisible] = useState<boolean>(false);
  const [searchText, setSearchText] = useState<string>("");
  const [statusFilter, setStatusFilter] = useState<UserStatusFilter>("all");
  const [sortBy, setSortBy] = useState<UserSortBy>("invitationSentOn");
  const [sortDescending, setSortDescending] = useState(true);
  const [invitationHistoryVisible, setInvitationHistoryVisible] =
    useState(false);
  const [invitationHistoryLoading, setInvitationHistoryLoading] =
    useState(false);
  const [invitationHistoryUser, setInvitationHistoryUser] =
    useState<UserManagerGridVm | null>(null);
  const [invitationEmailHistory, setInvitationEmailHistory] = useState<
    InvitationEmailAttemptVm[]
  >([]);
  const [form] = Form.useForm();
  const [revokeForm] = Form.useForm<{ reason: string }>();
  const [revokeModalUser, setRevokeModalUser] =
    useState<UserManagerGridVm | null>(null);
  const navigate = useNavigate();
  const { currentUser } = useContext(AppContext);
  const toolbarVisibility = useScrollRevealVisibility({
    breakpointPx: USER_MANAGER_COLLAPSE_BREAKPOINT_PX,
    mode: "direct",
  });

  const fetchUsers = async () => {
    setLoading(true);
    try {
      const response = await AccountUserManagerService.GetAccountUsers();
      setUsers(response);
    } catch (error) {
      message.error("Failed to load users.");
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchUsers();
  }, []);

  const handleInviteUser = async (values: InviteUserRequest) => {
    try {
      setLoading(true);
      const result = await AccountUserManagerService.InviteUser(values);
      if (
        result.invitationEmailDeliveryStatus ===
        MessageDeliveryStatus.SendFailed
      ) {
        message.warning(
          "Invitation created, but the email could not be sent. You can resend it from the user list."
        );
      } else {
        message.success("Invitation sent successfully.");
      }
      setInviteModalVisible(false);
      form.resetFields();
      // Navigate to userId/permissions/View relative to this page
      navigate(`${result.userId}/permissions/${PermissionKey.View}`);
      fetchUsers();
    } catch (err) {
      const error = err as ApiErrorResponse;
      message.error(error.message);
    }
    setLoading(false);
  };

  const handleShowInvitationHistory = async (user: UserManagerGridVm) => {
    setInvitationHistoryUser(user);
    setInvitationHistoryVisible(true);
    setInvitationHistoryLoading(true);
    try {
      setInvitationEmailHistory(
        await AccountUserManagerService.GetInvitationEmailHistory(user.userId)
      );
    } catch (err) {
      const error = err as PlanarianError;
      message.error(error.message);
      setInvitationEmailHistory([]);
    } finally {
      setInvitationHistoryLoading(false);
    }
  };

  const [isRevoking, setIsRevoking] = useState<boolean>(false);

  const handleRemoveInvitation = async (userId: string) => {
    try {
      setIsRevoking(true);
      await AccountUserManagerService.RevokeAccess(userId);
      message.success("Invitation removed.");
      fetchUsers();
    } catch (err) {
      const error = err as PlanarianError;
      message.error(error.message);
    } finally {
      setIsRevoking(false);
    }
  };

  const handleRevokeAccess = async (user: UserManagerGridVm, reason: string) => {
    try {
      setIsRevoking(true);
      const result = await AccountUserManagerService.RevokeAccess(user.userId, {
        reason: reason.trim(),
      });
      if (
        result?.notificationEmailDeliveryStatus ===
        MessageDeliveryStatus.SendFailed
      ) {
        message.warning(
          "Access was revoked, but the notification email could not be sent."
        );
      } else if (result?.notificationEmailDeliveryStatus != null) {
        message.success("Access revoked and the user was notified.");
      } else {
        message.success("Access revoked.");
      }
      setRevokeModalUser(null);
      revokeForm.resetFields();
      fetchUsers();
    } catch (err) {
      const error = err as PlanarianError;
      message.error(error.message);
    } finally {
      setIsRevoking(false);
    }
  };

  const [isRestoring, setIsRestoring] = useState<boolean>(false);
  const handleRestoreAccess = async (userId: string) => {
    try {
      setIsRestoring(true);
      const result = await AccountUserManagerService.RestoreAccess(userId);
      if (
        result?.notificationEmailDeliveryStatus ===
        MessageDeliveryStatus.SendFailed
      ) {
        message.warning(
          "Access was restored, but the notification email could not be sent."
        );
      } else if (result?.notificationEmailDeliveryStatus != null) {
        message.success("Access restored and the user was notified.");
      } else {
        message.success("Access restored.");
      }
      fetchUsers();
    } catch (err) {
      const error = err as PlanarianError;
      message.error(error.message);
    } finally {
      setIsRestoring(false);
    }
  };

  const [isResending, setIsResending] = useState<boolean>(false);
  const handleResendInvitation = async (userId: string) => {
    try {
      setIsResending(true);
      await AccountUserManagerService.ResendInvitation(userId);
      message.success("Invitation resent.");
      fetchUsers();
    } catch (err) {
      const error = err as PlanarianError;
      message.error(error.message);
    }
    setIsResending(false);
  };

  const getUserStatus = (user: UserManagerGridVm): UserStatusFilter => {
    if (user.accessRevokedOn) return "revoked";
    return user.invitationAcceptedOn ? "accepted" : "pending";
  };

  const sortOptions: SelectListItem<string>[] = [
    { display: "Invitation sent", value: "invitationSentOn" },
    { display: "Name", value: "fullName" },
    { display: "Email", value: "emailAddress" },
    { display: "Invitation accepted", value: "invitationAcceptedOn" },
    { display: "Last active", value: "lastActiveOn" },
  ];

  const compareNullableDates = (a?: string | null, b?: string | null): number =>
    new Date(a ?? 0).getTime() - new Date(b ?? 0).getTime();

  const filteredUsers = users
    .filter(
      (user) =>
        user.fullName.toLowerCase().includes(searchText.toLowerCase()) ||
        user.emailAddress.toLowerCase().includes(searchText.toLowerCase())
    )
    .filter(
      (user) => statusFilter === "all" || getUserStatus(user) === statusFilter
    )
    .sort((a, b) => {
      let comparison = 0;

      switch (sortBy) {
        case "fullName":
          comparison = a.fullName.localeCompare(b.fullName);
          break;
        case "emailAddress":
          comparison = a.emailAddress.localeCompare(b.emailAddress);
          break;
        case "invitationAcceptedOn":
          comparison = compareNullableDates(
            a.invitationAcceptedOn,
            b.invitationAcceptedOn
          );
          break;
        case "lastActiveOn":
          comparison = compareNullableDates(a.lastActiveOn, b.lastActiveOn);
          break;
        case "invitationSentOn":
        default:
          comparison = compareNullableDates(
            a.invitationSentOn,
            b.invitationSentOn
          );
          break;
      }

      return sortDescending ? -comparison : comparison;
    });

  const renderDate = (value?: string | null) =>
    value ? formatDateTime(value) : "Not recorded";

  const renderUserCard = (user: UserManagerGridVm) => {
    const isPending = user.hasActiveInvitation;
    const isRevoked = !!user.accessRevokedOn;
    const actions: GridCardAction[] = [
      {
        key: "edit",
        label: "Edit",
        icon: <EditOutlined />,
        to: user.userId,
        type: "primary",
      },
    ];

    if (isPending) {
      actions.push({
        key: "resend",
        label: "Resend",
        icon: <RedoOutlined />,
        loading: isResending,
        onClick: () => handleResendInvitation(user.userId),
      });
    }

    if (isRevoked) {
      actions.push({
        key: "restore",
        label: "Restore access",
        render: (
          <PlanarianButton
            alwaysShowChildren
            icon={<RedoOutlined />}
            permissionKey={PermissionKey.Admin}
            loading={isRestoring}
            type="default"
            onClick={() => handleRestoreAccess(user.userId)}
          >
            Restore access
          </PlanarianButton>
        ),
      });
    } else if (isPending && user.userId !== currentUser?.id) {
      actions.push({
        key: "remove",
        label: "Remove invitation",
        render: (
          <DeleteButtonComponent
            alwaysShowChildren
            permissionKey={PermissionKey.Admin}
            loading={isRevoking}
            type="default"
            title={`Are you sure you want to remove the invitation for ${user.fullName}?`}
            onConfirm={() => handleRemoveInvitation(user.userId)}
            okText="Yes"
            cancelText="No"
          >
            Remove invitation
          </DeleteButtonComponent>
        ),
      });
    } else if (user.userId !== currentUser?.id) {
      actions.push({
        key: "revoke",
        label: "Revoke access",
        render: (
          <PlanarianButton
            alwaysShowChildren
            danger
            icon={<DeleteOutlined />}
            permissionKey={PermissionKey.Admin}
            loading={isRevoking && revokeModalUser?.userId === user.userId}
            type="default"
            onClick={() => setRevokeModalUser(user)}
          >
            Revoke access
          </PlanarianButton>
        ),
      });
    }

    return (
      <GridCard
        actions={actions}
        className="user-manager-grid-card"
        stickyFooter
        stickyHeader
        header={
          <span>
            <span className="user-manager-grid-card__name">
              {user.fullName}
            </span>
            <span className="user-manager-grid-card__email">
              {user.emailAddress}
            </span>
          </span>
        }
        headerExtra={
          isRevoked ? (
            <span className="user-manager-grid-card__status">Access revoked</span>
          ) : isPending ? (
            <span className="user-manager-grid-card__status">Pending</span>
          ) : null
        }
      >
        <div className="user-manager-grid-card__details">
          <div className="user-manager-grid-card__detail">
            <Text type="secondary">Invitation Sent</Text>
            <span>{renderDate(user.invitationSentOn)}</span>
          </div>
          <div className="user-manager-grid-card__detail">
            <Text type="secondary">Invitation Accepted</Text>
            <span>{renderDate(user.invitationAcceptedOn)}</span>
          </div>
          <div className="user-manager-grid-card__detail">
            <Text type="secondary">Last Active</Text>
            <span>{renderDate(user.lastActiveOn)}</span>
          </div>
          {user.accessRevokedOn ? (
            <div className="user-manager-grid-card__detail">
              <Text type="secondary">Access Revoked</Text>
              <span>{renderDate(user.accessRevokedOn)}</span>
            </div>
          ) : null}
          {user.invitationEmailAttemptCount > 0 ? (
            <div className="user-manager-grid-card__history">
              <PlanarianButton
                alwaysShowChildren
                icon={<MailOutlined />}
                size="small"
                type="link"
                onClick={() => handleShowInvitationHistory(user)}
              >
                Email History
              </PlanarianButton>
            </div>
          ) : null}
        </div>
      </GridCard>
    );
  };

  return (
    <>
      <div className="user-manager-container">
        <div className="user-manager-toolbar">
          <div className="user-manager-toolbar__search-row">
            <Input.Search
              className="user-manager-search"
              placeholder="Search users"
              onSearch={(value) => setSearchText(value)}
              onChange={(e) => setSearchText(e.target.value)}
              allowClear
            />
          </div>
          <ScrollCollapseSection
            className="user-manager-toolbar__collapsible"
            visible={toolbarVisibility.isVisible}
          >
            <div className="user-manager-toolbar__secondary-rows">
              <div className="user-manager-toolbar__secondary-controls">
                <Select<UserStatusFilter>
                  className="user-manager-filter"
                  value={statusFilter}
                  onChange={setStatusFilter}
                  options={[
                    { label: "All statuses", value: "all" },
                    { label: "Accepted", value: "accepted" },
                    { label: "Pending", value: "pending" },
                    { label: "Access revoked", value: "revoked" },
                  ]}
                />
                <SplitSortControl
                  isDescending={sortDescending}
                  onSelect={(value) => setSortBy(value as UserSortBy)}
                  onToggleDirection={() =>
                    setSortDescending((previous) => !previous)
                  }
                  selectedValue={sortBy}
                  sortOptions={sortOptions}
                />
              </div>
              <div className="user-manager-invite-column">
                <PlanarianButton
                  className="user-manager-invite-button"
                  permissionKey={PermissionKey.Admin}
                  icon={<UserAddOutlined />}
                  type="primary"
                  alwaysShowChildren
                  onClick={() => setInviteModalVisible(true)}
                >
                  Invite User
                </PlanarianButton>
              </div>
            </div>
          </ScrollCollapseSection>
        </div>
        <div className="user-manager-grid">
          <SpinnerCardComponent spinning={loading}>
            <CardGridComponent
              fillHeight
              items={filteredUsers}
              itemKey={(user) => user.userId}
              noDataDescription="No users found"
              onScrollStateChange={toolbarVisibility.handleScrollStateChange}
              renderItem={renderUserCard}
            />
          </SpinnerCardComponent>
        </div>
      </div>

      <EmailHistoryModal
        open={invitationHistoryVisible}
        loading={invitationHistoryLoading}
        title={
          invitationHistoryUser
            ? `Invitation email history — ${invitationHistoryUser.fullName}`
            : "Invitation email history"
        }
        attempts={invitationEmailHistory}
        emptyText="No tracked invitation emails."
        onClose={() => setInvitationHistoryVisible(false)}
      />

      <Modal
        title={
          revokeModalUser
            ? `Revoke access for ${revokeModalUser.fullName}`
            : "Revoke access"
        }
        open={revokeModalUser != null}
        confirmLoading={isRevoking}
        okText="Revoke access"
        okButtonProps={{ danger: true }}
        onCancel={() => {
          setRevokeModalUser(null);
          revokeForm.resetFields();
        }}
        onOk={() => {
          if (!revokeModalUser) return;
          revokeForm
            .validateFields()
            .then(({ reason }) => handleRevokeAccess(revokeModalUser, reason))
            .catch(() => {});
        }}
      >
        <Text>
          Their existing permissions will be preserved so access can be restored
          later. The reason below will be shown to the user in Planarian and
          included in the notification email.
        </Text>
        <Form form={revokeForm} layout="vertical" style={{ marginTop: 16 }}>
          <Form.Item
            label="Reason for revocation"
            name="reason"
            rules={[
              {
                required: true,
                whitespace: true,
                message: "Please provide a reason for revoking access.",
              },
              {
                max: 255,
                message: "The reason cannot exceed 255 characters.",
              },
            ]}
          >
            <Input.TextArea
              rows={4}
              maxLength={255}
              showCount
              placeholder="Explain why this account access is being revoked"
            />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title="Invite User"
        open={inviteModalVisible}
        confirmLoading={loading}
        onCancel={() => setInviteModalVisible(false)}
        onOk={() => {
          form
            .validateFields()
            .then((values) => {
              handleInviteUser(values as InviteUserRequest);
            })
            .catch((info) => {
              console.error("Validation Failed:", info);
            });
        }}
      >
        <Form layout="vertical" form={form}>
          <Row gutter={16}>
            <Col xs={24} sm={12}>
              <Form.Item
                label="First Name"
                name={nameof<InviteUserRequest>("firstName")}
              >
                <Input />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item
                label="Last Name"
                name={nameof<InviteUserRequest>("lastName")}
              >
                <Input />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item
            label="Email Address"
            name={nameof<InviteUserRequest>("emailAddress")}
            rules={[
              { required: true, type: "email", message: "Invalid email" },
            ]}
          >
            <Input type="email" />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
};

export { UserManagerComponent };
