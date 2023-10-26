import { Modal, Button, Form, List, message, Divider } from "antd";
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
  const currentAccountName = AuthenticationService.GetAccountName();
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
      title="Switch Accounts"
      open={isOpen}
      onCancel={onCancel}
      footer={[<CancelButtonComponent key="cancel" onClick={onCancel} />]}
    >
      <Form
        id="switchAccountForm"
        onFinish={(values) => handleSwitch(values.account)}
      >
        {/* Display the current account */}
        <div
          style={{
            // fontWeight: "bold",
            backgroundColor: "#f0f2f5",
            padding: "10px",
          }}
        >
          Your Current Account: {currentAccountName}
        </div>
        <div
          style={{
            paddingTop: "10px",
            paddingBottom: "10px",
            fontWeight: "bold",
          }}
        >
          Please select one of the accounts below to switch to:
        </div>
        <Form.Item
          name="account"
          rules={[{ required: true, message: "Please select an account!" }]}
        >
          <List
            dataSource={accountList}
            renderItem={(item) => (
              <List.Item
                key={item.value}
                onClick={() => handleSwitch(item.value)}
                style={{
                  cursor: "pointer",
                  transition: "background-color 0.3s, border-color 0.3s",
                  padding: "10px",
                  border: "1px solid #d9d9d9", // Adding a border
                  borderRadius: "4px", // Optional: to give rounded corners
                  marginBottom: "10px", // Optional: to space out items
                }}
                onMouseEnter={(e) =>
                  (e.currentTarget.style.backgroundColor = "#e6f7ff")
                }
                onMouseLeave={(e) =>
                  (e.currentTarget.style.backgroundColor = "")
                }
              >
                {item.display}
              </List.Item>
            )}
          />
        </Form.Item>
      </Form>
    </Modal>
  );
};

export { SwitchAccountComponent };
