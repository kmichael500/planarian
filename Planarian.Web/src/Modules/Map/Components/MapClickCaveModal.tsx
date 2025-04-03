import { FC } from "react";
import { Spin } from "antd";
import { CloseOutlined, EyeOutlined } from "@ant-design/icons";
import { CaveVm } from "../../Caves/Models/CaveVm";
import { CaveComponent } from "../../Caves/Components/CaveComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { NavigationService } from "../../../Shared/Services/NavigationService";
import { useNavigate } from "react-router-dom";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";

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
  handleCancel: handleClose,
}) => {
  const navigate = useNavigate();
  return (
    <PlanarianModal
      header={cave?.name || "Cave"}
      open={isModalVisible}
      onClose={handleClose}
      footer={[
        <PlanarianButton
          key="view"
          alwaysShowChildren
          onClick={() => {
            if (cave) {
              NavigationService.NavigateToCave(cave.id, navigate);
            }
          }}
          icon={<EyeOutlined />}
        >
          View
        </PlanarianButton>,
      ]}
    >
      <Spin spinning={isModalLoading}>
        <CaveComponent
          options={{ showMap: false, inCardContainer: false }}
          cave={cave}
          isLoading={isModalLoading}
        />
      </Spin>
    </PlanarianModal>
  );
};

export { MapClickCaveModal };
