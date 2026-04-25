import React, { useEffect, useState } from "react";
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
  EditOutlined,
  RedoOutlined,
  UserAddOutlined,
} from "@ant-design/icons";
import { useNavigate } from "react-router-dom";

import { AccountUserManagerService } from "../Services/UserManagerService";
import { InviteUserRequest } from "../Models/InviteUserRequest";
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
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { SplitSortControl } from "../../Search/Components/SplitSortControl";
import { ScrollCollapseSection } from "../../../Shared/Components/ScrollCollapseSection/ScrollCollapseSection";
import { useScrollRevealVisibility } from "../../../Shared/Hooks/useScrollRevealVisibility";
import "./UserManagerComponent.scss";

const { Text } = Typography;

type UserStatusFilter = "all" | "accepted" | "pending";
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
  const [form] = Form.useForm();
  const navigate = useNavigate();
  const toolbarVisibility = useScrollRevealVisibility({
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
      const userId = await AccountUserManagerService.InviteUser(values);
      message.success("Invitation sent successfully.");
      setInviteModalVisible(false);
      form.resetFields();
      // Navigate to userId/permissions/View relative to this page
      navigate(`${userId}/permissions/${PermissionKey.View}`);
      fetchUsers();
    } catch (err) {
      const error = err as ApiErrorResponse;
      message.error(error.message);
    }
    setLoading(false);
  };

  const [isRevoking, setIsRevoking] = useState<boolean>(false);
  const handleRevokeAccess = async (userId: string) => {
    try {
      setIsRevoking(true);
      await AccountUserManagerService.RevokeAccess(userId);
      message.success("Access revoked.");
      fetchUsers();
    } catch (err) {
      const error = err as PlanarianError;
      message.error(error.message);
    }
    setIsRevoking(false);
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

  const getUserStatus = (user: UserManagerGridVm): UserStatusFilter =>
    user.invitationAcceptedOn ? "accepted" : "pending";

  const sortOptions: SelectListItem<string>[] = [
    { display: "Invitation sent", value: "invitationSentOn" },
    { display: "Name", value: "fullName" },
    { display: "Email", value: "emailAddress" },
    { display: "Invitation accepted", value: "invitationAcceptedOn" },
    { display: "Last active", value: "lastActiveOn" },
  ];

  const compareNullableDates = (
    a?: string | null,
    b?: string | null
  ): number => new Date(a ?? 0).getTime() - new Date(b ?? 0).getTime();

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
    const isPending = user.invitationSentOn && !user.invitationAcceptedOn;
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

    actions.push({
      key: "remove",
      label: "Remove",
      render: (
        <DeleteButtonComponent
          alwaysShowChildren
          permissionKey={PermissionKey.Admin}
          loading={isRevoking}
          type="default"
          title={`Are you sure you want to revoke access for ${user.fullName}? You will need to re-invite them to grant access in the future.`}
          onConfirm={() => handleRevokeAccess(user.userId)}
          okText="Yes"
          cancelText="No"
        >
          Remove
        </DeleteButtonComponent>
      ),
    });

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
          isPending ? (
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
          <ScrollCollapseSection visible={toolbarVisibility.isVisible}>
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
