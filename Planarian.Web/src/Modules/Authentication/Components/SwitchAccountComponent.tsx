import { Modal, Button, Form, Select, message } from "antd";
import { AppOptions } from "../../../Shared/Services/AppService";
import { AuthenticationService } from "../Services/AuthenticationService";
import React from "react";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { CancelButtonComponent } from "../../../Shared/Components/Buttons/CancelButtonComponent";
import { SwapOutlined } from "@ant-design/icons";

type SwitchAccountComponentProps = {
  isVisible: boolean;
  handleCancel: () => void;
};

const SwitchAccountComponent = ({
  isVisible: isOpen,
  handleCancel: onCancel,
}: SwitchAccountComponentProps) => {
  const currentAccountId = AuthenticationService.GetAccountId();
  const accountList = AppOptions.accountIds.filter(
    (item) => item.value !== currentAccountId
  );

  const handleSwitch = async (accountId: string) => {
    try {
      AuthenticationService.SwitchAccount(accountId);
      const accountName = accountList.find(
        (item) => item.value === accountId
      )?.display;
      message.success(`Switched to account ${accountName}`);
      window.location.reload();
    } catch (error) {
      message.error("Failed to switch account");
    }
  };

  return (
    <Modal
      title="Select an Account"
      open={isOpen}
      onCancel={onCancel}
      footer={[
        <CancelButtonComponent key="cancel" onClick={onCancel} />,

        <PlanarianButton
          key="submit"
          type="primary"
          form="switchAccountForm"
          htmlType="submit"
          icon={<SwapOutlined />}
        >
          Switch
        </PlanarianButton>,
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
  );
};

export { SwitchAccountComponent };