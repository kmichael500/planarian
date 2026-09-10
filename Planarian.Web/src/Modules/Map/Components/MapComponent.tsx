import { useState } from "react";
import { CaveVm } from "../../Caves/Models/CaveVm";
import { CaveService } from "../../Caves/Service/CaveService";
import { MapBaseComponent } from "./MapBaseComponent";
import { MapClickCaveModal } from "./MapClickCaveModal";
import { ViewStateChangeEvent } from "react-map-gl/maplibre";
import { MapClickPointModal } from "./MapClickPointModal";
import { FeatureCollection } from "geojson";

interface MapComponentProps {
  initialCenter?: [number, number];
  initialZoom?: number;
  onMoveEnd?: ((e: ViewStateChangeEvent) => void) | undefined;
  showFullScreenControl?: boolean;
  showGeolocateControl?: boolean;
  showSearchBar?: boolean;
  onShapefileUploaded?: (data: FeatureCollection[]) => void;
}

const MapComponent = ({
  initialCenter,
  initialZoom,
  onMoveEnd,
  showFullScreenControl = false,
  showGeolocateControl = true,
  showSearchBar = true,
  onShapefileUploaded,
}: MapComponentProps) => {
  const [isModalVisible, setIsModalVisible] = useState(false);
  const [isModalLoading, setIsModalLoading] = useState(false);
  const [cave, setCave] = useState<CaveVm | undefined>(undefined);

  // New state for geologic info modal
  const [isGeoModalVisible, setIsGeoModalVisible] = useState(false);
  const [geoCoordinates, setGeoCoordinates] = useState<[number, number] | null>(
    null
  );

  const handleCaveClick = async (caveId: string) => {
    setIsModalLoading(true);
    setIsModalVisible(true);
    const result = await CaveService.GetCave(caveId);
    setCave(result);
    setIsModalLoading(false);
  };

  // This callback is called when a non-cave area is clicked.
  const handleNonCaveClick = (lat: number, lng: number) => {
    setGeoCoordinates([lat, lng]);
    setIsGeoModalVisible(true);
  };

  const handleCancel = () => {
    setIsModalVisible(false);
    setIsGeoModalVisible(false);
  };

  return (
    <>
      <MapBaseComponent
        initialCenter={initialCenter}
        initialZoom={initialZoom}
        onCaveClicked={handleCaveClick}
        onNonCaveClicked={handleNonCaveClick}
        onMoveEnd={onMoveEnd}
        showFullScreenControl={showFullScreenControl}
        showGeolocateControl={showGeolocateControl}
        showSearchBar={showSearchBar}
        onShapefileUploaded={onShapefileUploaded}
      />
      <MapClickCaveModal
        isModalVisible={isModalVisible}
        isModalLoading={isModalLoading}
        cave={cave}
        handleCancel={handleCancel}
      />
      {geoCoordinates && (
        <MapClickPointModal
          isModalVisible={isGeoModalVisible}
          lat={geoCoordinates[0]}
          lng={geoCoordinates[1]}
          handleCancel={handleCancel}
        />
      )}
    </>
  );
};

export { MapComponent };
