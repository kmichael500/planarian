import { useState } from "react";
import { CaveVm } from "../../Caves/Models/CaveVm";
import { CaveService } from "../../Caves/Service/CaveService";
import { MapBaseComponent } from "./MapBaseComponent";
import { MapClickModal } from "./MapClickModal";
import { ViewStateChangeEvent } from "react-map-gl/maplibre";
interface MapComponentProps {
  initialCenter?: [number, number];
  initialZoom?: number;
  onMoveEnd?: ((e: ViewStateChangeEvent) => void) | undefined;
  showFullScreenControl?: boolean;
}

const MapComponent = ({
  initialCenter,
  initialZoom,
  onMoveEnd,
  showFullScreenControl = false,
}: MapComponentProps) => {
  const [isModalVisible, setIsModalVisible] = useState(false);
  const [isModalLoading, setIsModalLoading] = useState(false);
  const [cave, setCave] = useState<CaveVm | undefined>(undefined);

  const handleCaveClick = async (caveId: string) => {
    setIsModalLoading(true);
    setIsModalVisible(true);

    const result = await CaveService.GetCave(caveId);
    setCave(result);
    setIsModalLoading(false);
  };

  const handleOk = () => {
    setIsModalVisible(false);
  };

  const handleCancel = () => {
    setIsModalVisible(false);
  };

  return (
    <>
      <MapBaseComponent
        initialCenter={initialCenter}
        initialZoom={initialZoom}
        onCaveClicked={handleCaveClick}
        onMoveEnd={onMoveEnd}
        showFullScreenControl={showFullScreenControl}
      />
      <MapClickModal
        isModalVisible={isModalVisible}
        isModalLoading={isModalLoading}
        cave={cave}
        handleOk={handleOk}
        handleCancel={handleCancel}
      />
    </>
  );
};

export { MapComponent };
