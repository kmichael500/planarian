import { useState } from "react";
import {
  Menu,
  Dropdown,
  Avatar,
  message,
  Modal,
  List,
  Button,
  Form,
  Select,
} from "antd";
import { UserOutlined, SwapOutlined, LogoutOutlined } from "@ant-design/icons";
import { AppOptions } from "../../Shared/Services/AppService";
import styled from "styled-components";
import { useNavigate, useLocation } from "react-router-dom";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";
const StyledListItem = styled(List.Item)`
  cursor: pointer;
  transition: background-color 0.3s;

  &:hover {
    background-color: #f0f0f0; /* Adjust color to your preference */
  }
`;
type ProfileMenuProps = {
  user: {
    firstName: string;
    lastName: string;
  };
};

function ProfileMenu({ user }: ProfileMenuProps) {
  const [selectedKey, setSelectedKey] = useState<string>("");

  const getUserInitials = () => {
    return `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`;
  };

  const [isVisible, setIsVisible] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const [uniqueKey, setUniqueKey] = useState(0);

  const accountList = AppOptions.accountIds;
  const showModal = () => {
    console.log("show modal");
    setIsVisible(true);
  };

  const handleCancel = () => {
    setIsVisible(false);
  };

  const handleSwitch = async (accountId: string) => {
    try {
      AuthenticationService.SwitchAccount(accountId);
      message.success("Account switched successfully");
      window.location.reload();
    } catch (error) {
      message.error("Failed to switch account");
    }
  };

  const menuItems = [
    {
      key: "profile",
      icon: <UserOutlined />,
      label: "Profile",
    },
    {
      key: "switch-account",
      icon: <SwapOutlined />,
      label: "Switch Account",
    },
    {
      key: "logout",
      icon: <LogoutOutlined />,
      label: "Logout",
    },
  ];

  const renderMenuItem = (item: any) => (
    <Menu.Item
      key={item.key}
      icon={item.icon}
      onClick={() => {
        setSelectedKey(item.key);
        if (item.key === "switch-account") {
          showModal();
        }
      }}
    >
      {item.label}
    </Menu.Item>
  );

  const menu = (
    <Menu
      selectedKeys={[selectedKey]}
      onClick={(value) => setSelectedKey(value.key)}
    >
      {menuItems.map(renderMenuItem)}
    </Menu>
  );

  return (
    <>
      <Modal
        title="Select an Account"
        visible={isVisible}
        onCancel={handleCancel}
        footer={[
          <Button key="cancel" onClick={handleCancel}>
            Cancel
          </Button>,
          <Button
            key="submit"
            type="primary"
            form="switchAccountForm"
            htmlType="submit"
          >
            Switch
          </Button>,
        ]}
      >
        <Form
          id="switchAccountForm"
          onFinish={(values) => handleSwitch(values.account)}
        >
          <Form.Item
            name="account"
            rules={[{ required: true, message: "Please select an account!" }]}
          >
            <Select placeholder="Select an account" style={{ width: "100%" }}>
              {accountList.map((item) => (
                <Select.Option key={item.value} value={item.value}>
                  {item.display}
                </Select.Option>
              ))}
            </Select>
          </Form.Item>
        </Form>
      </Modal>
      <Dropdown overlay={menu} trigger={["click"]}>
        <Avatar
          size={35}
          style={{ backgroundColor: "#87d068", cursor: "pointer" }}
          icon={<UserOutlined />}
        >
          {getUserInitials()}
        </Avatar>
      </Dropdown>
    </>
  );
}

export default ProfileMenu;
