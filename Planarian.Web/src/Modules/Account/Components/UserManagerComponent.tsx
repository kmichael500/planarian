import React, { useEffect, useState } from "react";
import {
  Table,
  Button,
  Modal,
  Form,
  Input,
  message,
  Card,
  Row,
  Col,
  Space,
} from "antd";
import { RedoOutlined } from "@ant-design/icons";

import { AccountUserManagerService } from "../Services/UserManagerService";
import { InviteUserRequest } from "../Models/InviteUserRequest";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { PlanarianError } from "../../../Shared/Exceptions/PlanarianErrors";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { formatDateTime, nameof } from "../../../Shared/Helpers/StringHelpers";
import { ColumnsType } from "antd/lib/table";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";
import { Link, useNavigate } from "react-router-dom";
import { EditButtonComponentt } from "../../../Shared/Components/Buttons/EditButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { UserAddOutlined } from "@ant-design/icons";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";

const UserManagerComponent: React.FC = () => {
  const [users, setUsers] = useState<UserManagerGridVm[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [inviteModalVisible, setInviteModalVisible] = useState<boolean>(false);
  const [searchText, setSearchText] = useState<string>("");

  const navigate = useNavigate();

  const [form] = Form.useForm();

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

      // naviagte to userId/permissions/View relative to this page
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

  const columns: ColumnsType<UserManagerGridVm> = [
    {
      title: "Name",
      dataIndex: nameof<UserManagerGridVm>("fullName"),
      sorter: (a, b) => a.fullName.localeCompare(b.fullName),
    },
    {
      title: "Email",
      dataIndex: nameof<UserManagerGridVm>("emailAddress"),
      sorter: (a, b) => a.emailAddress.localeCompare(b.emailAddress),
    },
    {
      title: "Invitation Sent On",
      dataIndex: nameof<UserManagerGridVm>("invitationSentOn"),
      sorter: (a, b) =>
        new Date(a.invitationSentOn ?? 0).getTime() -
        new Date(b.invitationSentOn ?? 0).getTime(),
      defaultSortOrder: "descend",
      render: (text: string) => (text ? formatDateTime(text) : ""),
    },
    {
      title: "Invitation Accepted On",
      dataIndex: nameof<UserManagerGridVm>("invitationAcceptedOn"),
      sorter: (a, b) =>
        new Date(a.invitationAcceptedOn ?? 0).getTime() -
        new Date(b.invitationAcceptedOn ?? 0).getTime(),
      render: (text: string) => (text ? formatDateTime(text) : ""),
    },
    {
      title: "Action",
      render: (_: any, record: UserManagerGridVm) => (
        <Space>
          {" "}
          <DeleteButtonComponent
            permissionKey={PermissionKey.Admin}
            loading={isRevoking}
            title={`Are you sure you want to revoke access for ${record.fullName}? You will need to re-invite them to grant access in the future.`}
            onConfirm={() => {
              handleRevokeAccess(record.userId);
            }}
            okText="Yes"
            cancelText="No"
          >
            Revoke
          </DeleteButtonComponent>
          {record.invitationSentOn && !record.invitationAcceptedOn && (
            <PlanarianButton
              loading={isResending}
              permissionKey={PermissionKey.Admin}
              type="primary"
              onClick={() => handleResendInvitation(record.userId)}
              icon={<RedoOutlined />}
            >
              Resend Invitation
            </PlanarianButton>
          )}
          <Link to={record.userId}>
            <EditButtonComponentt />
          </Link>
        </Space>
      ),
    },
  ];

  // Filter users based on search text (matching full name or email)
  const filteredUsers = users.filter(
    (user) =>
      user.fullName.toLowerCase().includes(searchText.toLowerCase()) ||
      user.emailAddress.toLowerCase().includes(searchText.toLowerCase())
  );

  return (
    <>
      <Card>
        <div
          style={{
            marginBottom: 16,
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
          }}
        >
          <PlanarianButton
            permissionKey={PermissionKey.Admin}
            icon={<UserAddOutlined />}
            type="primary"
            onClick={() => setInviteModalVisible(true)}
          >
            Invite User
          </PlanarianButton>
          <Input.Search
            placeholder="Search users"
            onSearch={(value) => setSearchText(value)}
            onChange={(e) => setSearchText(e.target.value)}
            style={{ width: 200 }}
            allowClear
          />
        </div>
        <Table
          columns={columns}
          dataSource={filteredUsers}
          rowKey="userId"
          loading={loading}
        />
      </Card>

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
            <Col span={12}>
              <Form.Item
                label="First Name"
                name={nameof<InviteUserRequest>("firstName")}
              >
                <Input />
              </Form.Item>
            </Col>
            <Col span={12}>
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
