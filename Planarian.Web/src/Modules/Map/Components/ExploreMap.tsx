import React, { useCallback, useContext, useEffect, useRef, useState } from "react";
import { message, Spin } from "antd";
import { Popup } from "react-map-gl/maplibre";
import type { MapLayerMouseEvent, MapRef, ViewStateChangeEvent } from "react-map-gl/maplibre";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AppOptions } from "../../../Shared/Services/AppService";
import type { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { ApiExceptionType } from "../../../Shared/Models/ApiErrorResponse";
import type { CaveVm } from "../../Caves/Models/CaveVm";
import { CaveService } from "../../Caves/Service/CaveService";
import { MapService } from "../Services/MapService";
import { findNearbyEntranceCaveId } from "../Helpers/MapHitTesting";
import { CaveSearchMapControl } from "./CaveSearchMapControl";
import { MapLinePlotLayer } from "./MapLinePlotLayer";
import { MapEntranceTileLayer, MAP_ENTRANCE_LAYER_ID } from "./MapEntranceTileLayer";
import { ExploreMapFilters } from "./ExploreMapFilters";
import { MapLayerControl } from "./MapLayerControl";
import { PlanarianBaseMap } from "./PlanarianBaseMap";
import { MapClickCaveModal } from "./MapClickCaveModal";
import { MapClickPointModal } from "./MapClickPointModal";
import { MapLayerProvider } from "./MapLayerContext";
import { MapLayers } from "./MapLayers";

interface ExploreMapProps {
  initialCenter?: [number, number];
  initialZoom?: number;
  onMoveEnd?: (event: ViewStateChangeEvent) => void;
}

export const ExploreMap: React.FC<ExploreMapProps> = ({ initialCenter, initialZoom = 7, onMoveEnd }) => {
  const {
    currentAccountName,
    setHideBodyPadding,
    hideBodyPadding,
  } = useContext(AppContext);
  const mapRef = useRef<MapRef>(null);
  const [center, setCenter] = useState<[number, number] | null>(initialCenter ?? null);
  const [viewportRevision, setViewportRevision] = useState(0);
  const [filterQuery, setFilterQuery] = useState("");
  const [selectedCave, setSelectedCave] = useState<CaveVm>();
  const [caveModalOpen, setCaveModalOpen] = useState(false);
  const [caveLoading, setCaveLoading] = useState(false);
  const [point, setPoint] = useState<[number, number] | null>(null);
  const [lineworkPopup, setLineworkPopup] = useState<{
    longitude: number;
    latitude: number;
    properties: Record<string, unknown>;
  } | null>(null);

  useEffect(() => {
    setHideBodyPadding(true);
    return () => setHideBodyPadding(false);
  }, [setHideBodyPadding]);

  useEffect(() => {
    if (initialCenter) {
      setCenter(initialCenter);
      return;
    }
    let cancelled = false;
    MapService.getMapCenter()
      .then((result) => !cancelled && setCenter([result.latitude, result.longitude]))
      .catch((error: ApiErrorResponse) => {
        if (!cancelled && error?.errorCode !== ApiExceptionType.TooManyRequests) {
          message.error(error?.message || "Unable to load the initial map center.");
        }
      });
    return () => { cancelled = true; };
  }, [initialCenter]);


  const openCave = useCallback(async (caveId: string) => {
    setCaveLoading(true);
    setCaveModalOpen(true);
    try {
      setSelectedCave(await CaveService.GetCave(caveId));
    } catch (error) {
      console.error("Unable to load cave from map", error);
      message.error("Unable to load cave.");
      setCaveModalOpen(false);
    } finally {
      setCaveLoading(false);
    }
  }, []);

  const handleClick = useCallback((event: MapLayerMouseEvent) => {
    const target = event.originalEvent?.target as HTMLElement | null;
    if (target?.closest(".planarian-map-control")) return;

    const exactEntrance = event.features?.find(
      (feature) => feature.layer.id === MAP_ENTRANCE_LAYER_ID
    );
    const exactCaveId = exactEntrance?.properties?.CaveId;
    if (typeof exactCaveId === "string" && exactCaveId) {
      setLineworkPopup(null);
      setPoint(null);
      void openCave(exactCaveId);
      return;
    }

    const lineworkFeature = mapRef.current
      ?.getMap()
      .queryRenderedFeatures(event.point)
      .find((feature) => feature.layer.id.startsWith("linework-"));
    if (lineworkFeature) {
      setPoint(null);
      setLineworkPopup({
        longitude: event.lngLat.lng,
        latitude: event.lngLat.lat,
        properties: lineworkFeature.properties ?? {},
      });
      return;
    }

    const nearbyCaveId = findNearbyEntranceCaveId(mapRef.current, event.point);
    if (nearbyCaveId) {
      setLineworkPopup(null);
      setPoint(null);
      void openCave(nearbyCaveId);
      return;
    }

    setLineworkPopup(null);
    setPoint([event.lngLat.lat, event.lngLat.lng]);
  }, [openCave]);

  const handleMoveEnd = useCallback((event: ViewStateChangeEvent) => {
    setViewportRevision((revision) => revision + 1);
    onMoveEnd?.(event);
  }, [onMoveEnd]);

  if (!center || !AppOptions.apiBaseUrl || !hideBodyPadding) {
    return <div className="planarian-map-page planarian-map-loading"><Spin /></div>;
  }

  return (
    <div className="planarian-map-page">
      <MapLayerProvider>
        <PlanarianBaseMap
          ref={mapRef}
          initialCenter={center}
          initialZoom={initialZoom}
          onMoveEnd={handleMoveEnd}
          onClick={handleClick}
          interactiveLayerIds={[MAP_ENTRANCE_LAYER_ID]}
          showGeolocate
        >
          <MapLayers />
          <MapEntranceTileLayer filterQuery={filterQuery} />
          <MapLinePlotLayer
            viewportRevision={viewportRevision}
            attribution={currentAccountName ? `© ${currentAccountName}` : undefined}
          />
          <div className="planarian-map-control"><CaveSearchMapControl /></div>
          <MapLayerControl position={{ top: "50px", right: "0" }} />
          <ExploreMapFilters onQueryChange={setFilterQuery} />
          {lineworkPopup && (
            <Popup
              longitude={lineworkPopup.longitude}
              latitude={lineworkPopup.latitude}
              onClose={() => setLineworkPopup(null)}
              closeOnClick={false}
              anchor="top"
            >
              <pre style={{ whiteSpace: "pre-wrap", wordWrap: "break-word", maxHeight: 200, overflow: "auto" }}>
                {JSON.stringify(lineworkPopup.properties, null, 2)}
              </pre>
            </Popup>
          )}
        </PlanarianBaseMap>
      </MapLayerProvider>

      <MapClickCaveModal
        isModalVisible={caveModalOpen}
        isModalLoading={caveLoading}
        cave={selectedCave}
        handleCancel={() => setCaveModalOpen(false)}
      />
      {point && (
        <MapClickPointModal
          isModalVisible
          lat={point[0]}
          lng={point[1]}
          handleCancel={() => setPoint(null)}
        />
      )}
    </div>
  );
};
