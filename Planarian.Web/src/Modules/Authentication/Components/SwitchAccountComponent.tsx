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
    disabledAccountIds,
    currentAccountId,
    currentAccountName,
    switchAccount,
  } = useContext(AppContext);

  const accountList = useMemo(
    () => [
      ...accountIds
        .filter((item) => item.value !== currentAccountId)
        .map((item) => ({ ...item, isDisabled: false, disableMessage: null as string | null })),
      ...disabledAccountIds.map((item) => ({ ...item, isDisabled: true })),
    ],
    [accountIds, disabledAccountIds, currentAccountId]
  );

  const handleSwitch = (accountId: string) => {
    onCancel();
    switchAccount(accountId, currentAccountId ? undefined : "/caves");
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
              const isDisabled = item.isDisabled;
              const disableMessage =
                item.disableMessage?.trim() ||
                `Your access to ${item.display} has been disabled. If you have any questions or believe this was a mistake, please reach out to an administrator.`;
              return (
                <List.Item
                  className="planarian-account-option"
                  key={item.value}
                  aria-disabled={isDisabled || undefined}
                  style={{
                    padding: isDisabled ? undefined : 0,
                    border: "1px solid #d9d9d9",
                    borderRadius: "4px",
                    marginBottom: "10px",
                  }}
                >
                  {isDisabled ? (
                    <div className="planarian-account-disabled-content">
                      <div className="planarian-account-disabled-header">
                        <Typography.Text strong>{item.display}</Typography.Text>
                        <Typography.Text
                          className="planarian-account-disabled-status"
                          type="danger"
                        >
                          Access disabled
                        </Typography.Text>
                      </div>
                      <Typography.Paragraph
                        className="planarian-account-disabled-message"
                        type="secondary"
                      >
                        {disableMessage}
                      </Typography.Paragraph>
                    </div>
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
