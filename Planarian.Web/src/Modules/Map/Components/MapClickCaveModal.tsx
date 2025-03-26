import { FC } from "react";
import { Modal, Spin } from "antd";
import { CloseOutlined, EyeOutlined } from "@ant-design/icons";
import { CaveVm } from "../../Caves/Models/CaveVm";
import { CaveComponent } from "../../Caves/Components/CaveComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { NavigationService } from "../../../Shared/Services/NavigationService";
import { useNavigate } from "react-router-dom";

interface MapClickCaveModal {
  isModalVisible: boolean;
  isModalLoading: boolean;
  cave: CaveVm | undefined;
  handleCancel: () => void;
}

const MapClickCaveModal: FC<MapClickCaveModal> = ({
  isModalVisible,
  isModalLoading,
  cave,
  handleCancel,
}) => {
  const navigate = useNavigate();
  return (
    <Modal
      title={cave?.name || "Cave"}
      visible={isModalVisible}
      onCancel={handleCancel}
      width="80vw"
      bodyStyle={{
        height: "65vh",
        overflow: "scroll",
        padding: "0px",
      }}
      footer={[
        ,
        <PlanarianButton
          key="view"
          onClick={() => {
            if (cave) {
              NavigationService.NavigateToCave(cave.id, navigate);
            }
          }}
          icon={<EyeOutlined />}
        >
          View
        </PlanarianButton>,
        <PlanarianButton
          key="close"
          onClick={handleCancel}
          icon={<CloseOutlined />}
        >
          Close
        </PlanarianButton>,
      ]}
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
};

export { MapClickCaveModal };
