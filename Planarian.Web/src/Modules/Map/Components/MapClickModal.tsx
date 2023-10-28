import React, { FC } from "react";
import { Modal, Spin } from "antd";
import { CaveVm } from "../../Caves/Models/CaveVm";
import { CaveComponent } from "../../Caves/Components/CaveComponent";

interface MapClickModalProps {
  isModalVisible: boolean;
  isModalLoading: boolean;
  cave: CaveVm | undefined;
  handleOk: () => void;
  handleCancel: () => void;
}

const MapClickModal: FC<MapClickModalProps> = ({
  isModalVisible,
  isModalLoading,
  cave,
  handleOk,
  handleCancel,
}) => (
  <Modal
    title={cave?.name || "Cave"}
    visible={isModalVisible}
    onOk={handleOk}
    onCancel={handleCancel}
    width="80vw"
    bodyStyle={{
      height: "65vh",
      overflow: "scroll",
      padding: "0px",
    }}
  >
    <Spin spinning={isModalLoading}>
      <CaveComponent
        options={{ showMap: false }}
        cave={cave}
        isLoading={isModalLoading}
      />
    </Spin>
  </Modal>
);

export { MapClickModal };
