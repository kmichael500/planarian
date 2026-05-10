import { Form, List, Modal } from "antd";
import React, { useContext, useMemo } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { CancelButtonComponent } from "../../../Shared/Components/Buttons/CancelButtonComponent";
import "./SwitchAccountComponent.scss";

type SwitchAccountComponentProps = {
  isVisible: boolean;
  handleCancel: () => void;
};

const SwitchAccountComponent = ({
  isVisible: isOpen,
  handleCancel: onCancel,
}: SwitchAccountComponentProps) => {
  const { accountIds, currentAccountId, currentAccountName, switchAccount } =
    useContext(AppContext);

  const accountList = useMemo(
    () => accountIds.filter((item) => item.value !== currentAccountId),
    [accountIds, currentAccountId]
  );

  const handleSwitch = (accountId: string) => {
    onCancel();
    switchAccount(accountId);
  };

  return (
    <Modal
      title="Switch Accounts"
      open={isOpen}
      onCancel={onCancel}
      footer={[<CancelButtonComponent key="cancel" onClick={onCancel} />]}
    >
      <Form id="switchAccountForm">
        <div className="planarian-account-banner">
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
        <Form.Item name="account">
          <List
            dataSource={accountList}
            renderItem={(item) => (
              <List.Item
                className="planarian-account-option"
                key={item.value}
                onClick={() => handleSwitch(item.value)}
                style={{
                  cursor: "pointer",
                  padding: "10px",
                  border: "1px solid #d9d9d9",
                  borderRadius: "4px",
                  marginBottom: "10px",
                }}
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
