import { List, Modal, Typography } from "antd";
import React, { useContext, useMemo } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { CancelButtonComponent } from "../../../Shared/Components/Buttons/CancelButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import "./SwitchAccountComponent.scss";

type SwitchAccountComponentProps = {
  isVisible: boolean;
  handleCancel: () => void;
};

const SwitchAccountComponent = ({
  isVisible: isOpen,
  handleCancel: onCancel,
}: SwitchAccountComponentProps) => {
  const {
    accountIds,
    revokedAccountIds,
    currentAccountId,
    currentAccountName,
    switchAccount,
  } = useContext(AppContext);

  const accountList = useMemo(
    () => [
      ...accountIds
        .filter((item) => item.value !== currentAccountId)
        .map((item) => ({ ...item, isRevoked: false })),
      ...revokedAccountIds.map((item) => ({ ...item, isRevoked: true })),
    ],
    [accountIds, revokedAccountIds, currentAccountId]
  );

  const handleSwitch = (accountId: string) => {
    onCancel();
    switchAccount(accountId);
  };

  return (
    <Modal
      title={currentAccountId ? "Switch Accounts" : "Accounts"}
      open={isOpen}
      onCancel={onCancel}
      footer={[<CancelButtonComponent key="cancel" onClick={onCancel} />]}
    >
      <div>
        {currentAccountId && (
          <div className="planarian-account-banner">
            Your Current Account: {currentAccountName}
          </div>
        )}
        <div
          style={{
            paddingTop: "10px",
            paddingBottom: "10px",
            fontWeight: "bold",
          }}
        >
          {currentAccountId
            ? "Please select one of the accounts below to switch to:"
            : "Your accounts:"}
        </div>
        <List
            dataSource={accountList}
            renderItem={(item) => {
              const isRevoked = item.isRevoked;
              return (
                <List.Item
                  className="planarian-account-option"
                  key={item.value}
                  aria-disabled={isRevoked || undefined}
                  style={{
                    opacity: isRevoked ? 0.65 : 1,
                    padding: isRevoked ? "10px" : 0,
                    border: "1px solid #d9d9d9",
                    borderRadius: "4px",
                    marginBottom: "10px",
                  }}
                >
                  {isRevoked ? (
                    <>
                      <span>{item.display}</span>
                      <Typography.Text type="danger">Access revoked</Typography.Text>
                    </>
                  ) : (
                    <PlanarianButton
                      icon={null}
                      alwaysShowChildren
                      block
                      type="text"
                      className="planarian-account-choice"
                      onClick={() => handleSwitch(item.value)}
                    >
                      {item.display}
                    </PlanarianButton>
                  )}
                </List.Item>
              );
            }}
          />
      </div>
    </Modal>
  );
};

export { SwitchAccountComponent };
