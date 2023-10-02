import React, { useEffect, useState } from "react";
import { Button, Modal, List, message } from "antd";
import { AppOptions } from "../../../Shared/Services/AppService";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import styled from "styled-components";
import { HttpClient } from "../../..";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { SwapOutlined } from "@ant-design/icons";
import { useLocation, useNavigate } from "react-router-dom";
import { AccountService } from "../../Account/Services/AccountService";
import { AuthenticationService } from "../Services/AuthenticationService";
const StyledListItem = styled(List.Item)`
  cursor: pointer;
  transition: background-color 0.3s;

  &:hover {
    background-color: #f0f0f0; /* Adjust color to your preference */
  }
`;

const SwitchAccountComponent: React.FC = () => {
  const [isVisible, setIsVisible] = useState(false);
  const [accountList, setAccountList] = useState<SelectListItem<string>[]>([]);
  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    setAccountList(AppOptions.accountIds);
  }, []);

  const showModal = () => {
    setIsVisible(true);
  };

  const handleCancel = () => {
    setIsVisible(false);
  };

  const handleSwitch = async (accountId: string) => {
    try {
      AuthenticationService.SwitchAccount(accountId);
    } catch (error) {
      message.error("Failed to switch account");
    }
  };

  return (
    <div>
      <PlanarianButton type="link" onClick={showModal} icon={<SwapOutlined />}>
        Switch Account
      </PlanarianButton>
      <Modal
        title="Select an Account"
        visible={isVisible}
        onCancel={handleCancel}
        footer={null}
      >
        <List
          dataSource={accountList}
          renderItem={(item) => (
            <StyledListItem onClick={() => handleSwitch(item.value)}>
              {item.display}
            </StyledListItem>
          )}
        />
      </Modal>
    </div>
  );
};

export default SwitchAccountComponent;
