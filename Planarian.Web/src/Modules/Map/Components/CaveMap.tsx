import React, { useCallback, useContext, useRef, useState } from "react";
import { ArrowsAltOutlined } from "@ant-design/icons";
import { useNavigate } from "react-router-dom";
import type { MapLayerMouseEvent, MapRef } from "react-map-gl/maplibre";
import { message } from "antd";
import { AppContext } from "../../../Configuration/Context/AppContext";
import type { CaveVm } from "../../Caves/Models/CaveVm";
import { CaveService } from "../../Caves/Service/CaveService";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { NavigationService } from "../../../Shared/Services/NavigationService";
import { findNearbyEntranceCaveId } from "../Helpers/MapHitTesting";
import { MapClickCaveModal } from "./MapClickCaveModal";
import { MAP_ENTRANCE_LAYER_ID, MapEntranceTileLayer } from "./MapEntranceTileLayer";
import { MapLayerControl } from "./MapLayerControl";
import { MapLinePlotLayer } from "./MapLinePlotLayer";
import { PlanarianBaseMap } from "./PlanarianBaseMap";
import { MapLayerProvider } from "./MapLayerContext";
import { MapLayers } from "./MapLayers";

interface CaveMapProps {
  cave: CaveVm;
  lineworkRefreshToken?: number;
}

export const CaveMap: React.FC<CaveMapProps> = ({ cave, lineworkRefreshToken = 0 }) => {
  const { currentAccountName } = useContext(AppContext);
  const navigate = useNavigate();
  const mapRef = useRef<MapRef>(null);
  const [viewportRevision, setViewportRevision] = useState(0);
  const [selectedCave, setSelectedCave] = useState<CaveVm>();
  const [caveModalOpen, setCaveModalOpen] = useState(false);
  const entrance = cave.primaryEntrance;

  const handleMoveEnd = useCallback(() => {
    setViewportRevision((revision) => revision + 1);
  }, []);

  const openCave = useCallback(async (caveId: string) => {
    try {
      const loadedCave = await CaveService.GetCave(caveId);
      setSelectedCave(loadedCave);
      setCaveModalOpen(true);
    } catch (error) {
      console.error("Unable to load cave from cave map", error);
      message.error("Unable to load cave.");
    }
  }, []);

  const handleClick = useCallback((event: MapLayerMouseEvent) => {
    const target = event.originalEvent?.target as HTMLElement | null;
    if (target?.closest(".planarian-map-control")) return;

    const exactCaveId = event.features?.find(
      (feature) => feature.layer.id === MAP_ENTRANCE_LAYER_ID
    )?.properties?.CaveId;
    if (typeof exactCaveId === "string" && exactCaveId) {
      void openCave(exactCaveId);
      return;
    }

    const nearbyCaveId = findNearbyEntranceCaveId(mapRef.current, event.point);
    if (nearbyCaveId) {
      void openCave(nearbyCaveId);
    }
  }, [openCave]);

  if (!entrance) return null;

  const openInMap = () => {
    const map = mapRef.current;
    const center = map?.getCenter();
    NavigationService.NavigateToMap(
      center?.lat ?? entrance.latitude,
      center?.lng ?? entrance.longitude,
      map?.getZoom() ?? 15,
      navigate
    );
  };

  return (
    <>
      <MapLayerProvider>
        <PlanarianBaseMap
          ref={mapRef}
          initialCenter={[entrance.latitude, entrance.longitude]}
          initialZoom={15}
          onMoveEnd={handleMoveEnd}
          onClick={handleClick}
          interactiveLayerIds={[MAP_ENTRANCE_LAYER_ID]}
        >
        <MapLayers />
        <MapEntranceTileLayer />
        <MapLinePlotLayer
          key={`${cave.id}-${lineworkRefreshToken}`}
          viewportRevision={viewportRevision}
          attribution={currentAccountName ? `© ${currentAccountName}` : undefined}
        />
        <MapLayerControl position={{ top: "0", right: "0" }} />
        <div className="planarian-map-control" style={{ position: "absolute", top: "110px", left: "10px" }}>
          <PlanarianButton
            icon={<ArrowsAltOutlined />}
            onClick={openInMap}
            tooltip="Open in Map"
            neverShowChildren
          />
        </div>
        </PlanarianBaseMap>
      </MapLayerProvider>
      <MapClickCaveModal
        isModalVisible={caveModalOpen}
        isModalLoading={false}
        cave={selectedCave}
        handleCancel={() => setCaveModalOpen(false)}
      />
    </>
  );
};
