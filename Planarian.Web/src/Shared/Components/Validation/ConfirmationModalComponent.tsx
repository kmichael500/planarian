import React, { ReactNode, useState } from "react";
import { Modal, Input, Typography } from "antd";
import {
  DeleteOutlined,
  CheckCircleOutlined,
  UndoOutlined,
} from "@ant-design/icons";
import { PlanarianButton } from "../Buttons/PlanarianButtton";

const { Paragraph } = Typography;

const normalizeConfirmationPhrase = (value: string) =>
  value.trim().toLowerCase();

interface ConfirmationModalComponentProps {
  onConfirm?: () => Promise<void> | void;
  onCancel?: () => void;
  confirmationWord: string;
  title?: string;
  okText?: string;
  cancelText?: string;
  modalMessage?: ReactNode;
  children?: ReactNode;
  onOkClickRender?: ReactNode;
  autoClose?: boolean;
}

const ConfirmationModalComponent = ({
  onConfirm,
  onCancel,
  confirmationWord,
  title = "Are you sure?",
  modalMessage,
  okText = "Ok",
  cancelText = "Cancel",
  children,
  onOkClickRender,
  autoClose = true,
  ...props
}: ConfirmationModalComponentProps) => {
  const [inputValue, setInputValue] = useState("");
  const [open, setVisible] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const isConfirmed =
    normalizeConfirmationPhrase(inputValue) ===
    normalizeConfirmationPhrase(confirmationWord);

  const handleOk = async () => {
    if (isConfirmed) {
      setIsLoading(true);

      if (onConfirm) {
        await onConfirm();
      }

      if (autoClose) {
        setInputValue("");
        setVisible(false);
        setIsLoading(false);
      }
    }
  };

  const handleCancel = () => {
    onCancel && onCancel();
    setInputValue("");
    setVisible(false);
  };

  const renderConfirmationWord = () => {
    const chars = confirmationWord.split("");
    const inputChars = inputValue.trim().split("");
    return chars.map((char, index) => {
      let color = "inherit";
      if (index < inputChars.length) {
        color =
          char.toLowerCase() === inputChars[index].toLowerCase()
            ? "green"
            : "red";
      }
      if (isConfirmed) {
        color = "green";
      }
      return (
        <span key={index} style={{ color }}>
          {char}
        </span>
      );
    });
  };

  return (<>
    <PlanarianButton
      danger
      onClick={() => setVisible(true)}
      icon={<DeleteOutlined />}
      type="primary"
    >
      {children}
    </PlanarianButton>
    <Modal
      width={700}
      open={open}
      title={title}
      onOk={handleOk}
      onCancel={handleCancel}
      {...props}
      footer={[
        <PlanarianButton
          key="cancel"
          onClick={handleCancel}
          icon={<UndoOutlined />}
        >
          {cancelText}
        </PlanarianButton>,
        <PlanarianButton
          key="confirm"
          danger
          onClick={handleOk}
          disabled={!isConfirmed}
          loading={isLoading}
          icon={<CheckCircleOutlined />}
        >
          {okText}
        </PlanarianButton>,
      ]}
    >
      {isLoading && onOkClickRender}
      {(!isLoading || (isLoading && !onOkClickRender)) && (
        <>
          {modalMessage !== undefined && modalMessage !== null && (
            <Paragraph>{modalMessage}</Paragraph>
          )}
          <Paragraph>Type the full phrase shown below to confirm.</Paragraph>
          <div style={{ textAlign: "center" }}>
            <Paragraph
              strong
              style={{ fontSize: "20px", textAlign: "center" }}
            >
              {renderConfirmationWord()}
            </Paragraph>
          </div>
          <Input
            style={{ fontSize: "20px" }}
            value={inputValue}
            onChange={(e) => setInputValue(e.target.value)}
            onPressEnter={(e) => {
              if (isConfirmed) {
                handleOk();
              }
            }}
          />
        </>
      )}
    </Modal>
  </>);
};

export { ConfirmationModalComponent };
