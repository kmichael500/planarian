import { Form, message, Button, List, Popconfirm, Modal, Select } from "antd";
import { useState, useEffect } from "react";
import { useParams } from "react-router-dom";
import { NotFoundError } from "../../../Shared/Exceptions/PlanarianErrors";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { SelectListItemDescriptionData } from "../../../Shared/Models/SelectListItem";
import { PermissionSelectListData } from "../Models/PermissionSelectListData";
import { UserPermissionVm } from "../Models/UserAccessPermissionVm";
import { AccountUserManagerService } from "../Services/UserManagerService";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { PermissionType } from "../../Authentication/Models/PermissionType";
import { Card, Spin } from "antd";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";
import { AddButtonComponent } from "../../../Shared/Components/Buttons/AddButtonComponent";

interface UserPermissionListProps {
  userId: string;
}

export const UserPermissionManagementList: React.FC<
  UserPermissionListProps
> = ({ userId }) => {
  if (isNullOrWhiteSpace(userId)) {
    throw new NotFoundError("Invalid user ID.");
  }

  const [permissions, setPermissions] = useState<UserPermissionVm[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [allPossiblePermissions, setAllPossiblePermissions] = useState<
    SelectListItemDescriptionData<string, PermissionSelectListData>[]
  >([]);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [form] = Form.useForm();

  const loadPermissions = async () => {
    try {
      const data = await AccountUserManagerService.GetUserPermissions(userId!);
      setPermissions(data);
    } catch (err: any) {
      message.error(err.message || "Failed to load user permissions");
      console.error(err);
    }
  };

  const loadPermissionSelectList = async () => {
    try {
      const list = await AccountUserManagerService.GetPermissionSelectList(
        PermissionType.User
      );
      setAllPossiblePermissions(list);
    } catch (err: any) {
      message.error(err.message || "Failed to load permission list");
      console.error(err);
    }
  };

  useEffect(() => {
    loadPermissions();
    loadPermissionSelectList();
  }, [userId]);

  const handleOpenModal = () => {
    form.resetFields();
    setIsModalOpen(true);
  };

  const handleCloseModal = () => {
    setIsModalOpen(false);
  };

  const handleAddPermission = async () => {
    try {
      const values = await form.validateFields();

      await AccountUserManagerService.AddUserPermission(
        userId,
        values.permissionKey
      );
      message.success("Permission added!");
      setIsModalOpen(false);
      loadPermissions();
    } catch (err: any) {
      const error = err as ApiErrorResponse;
      if (err.errorFields) {
      } else {
        message.error(error.message || "Failed to add permission");
      }
    }
  };

  const handleRemove = async (permissionKey: string) => {
    try {
      await AccountUserManagerService.RemoveUserPermission(
        userId,
        permissionKey
      );
      message.success("Permission removed.");
      loadPermissions();
    } catch (err: any) {
      message.error(err.message || "Failed to remove permission");
      console.error(err);
    }
  };

  useEffect(() => {
    const fetchData = async () => {
      setIsLoading(true);
      await loadPermissions();
      await loadPermissionSelectList();
      setIsLoading(false);
    };
    fetchData();
  }, [userId]);

  return (
    <Card
      loading={isLoading}
      title="User Permissions"
      extra={<AddButtonComponent onClick={handleOpenModal} />}
    >
      <List
        dataSource={permissions}
        renderItem={(item) => (
          <List.Item
            actions={[
              <DeleteButtonComponent
                title="Are you sure you want to delete this permission?"
                onConfirm={() => handleRemove(item.permissionKey)}
                okText="Yes"
                cancelText="No"
              ></DeleteButtonComponent>,
            ]}
          >
            <List.Item.Meta
              title={item.display}
              description={
                <div style={{ display: "flex", alignItems: "center" }}>
                  {item.description}
                </div>
              }
            />
          </List.Item>
        )}
      />

      <Modal
        title="Add User Permission"
        open={isModalOpen}
        onOk={handleAddPermission}
        onCancel={handleCloseModal}
        okText="Add"
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="permissionKey"
            label="Permission"
            rules={[{ required: true, message: "Please select a permission" }]}
          >
            <Select placeholder="Select permission type">
              {allPossiblePermissions.map((perm) => (
                <Select.Option key={perm.value} value={perm.data.key}>
                  {perm.display}
                </Select.Option>
              ))}
            </Select>
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  );
};
