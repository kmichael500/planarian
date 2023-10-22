import React, { useEffect, useState } from "react";
import {
  MapContainer,
  Marker,
  Popup,
  useMap,
  useMapEvents,
} from "react-leaflet";
import "leaflet/dist/leaflet.css";
import { MapService } from "../Services/MapService";

import { MapLayers } from "./MapLayers";
import L from "leaflet";
import { Spin } from "antd";
import MarkerClusterGroup from "react-leaflet-cluster";

import { LocateControl } from "./LocateControll";
import { MapData } from "./MapData";
import { PolygonWithText } from "./PolygonWithText";

L.Icon.Default.mergeOptions({
  iconRetinaUrl: "/marker-icon-2x.png",
  iconUrl: "/marker-icon.png",
  shadowUrl: "/marker-shadow.png",
});

export interface Cluster {
  latitude: number;
  longitude: number;
  count: number;
  isCluster: true;
  hullCoordinates: Coordinate[];
}

const PlanarianMap = () => {
  const map = useMap();

  useEffect(() => {
    const timer = setTimeout(() => {
      map.invalidateSize();
    }, 100);

    return () => clearTimeout(timer); // Clean up timeout if component is unmounted
  }, [map]);

  return null;
};

interface Bounds {
  getNorthEast: () => Coordinate;
  getSouthWest: () => Coordinate;
}

export interface Coordinate {
  latitude: number;
  longitude: number;
}

interface ExpandedViewBox {
  northEast: Coordinate;
  southWest: Coordinate;
}

const MapMarkers = ({ onDataLoaded }: { onDataLoaded?: () => void }) => {
  const [data, setData] = useState<MapData[]>([]);
  const [expandedViewBox, setExpandedViewBox] =
    useState<ExpandedViewBox | null>(null);
  const [currentZoom, setCurrentZoom] = useState<number | null>(null); // New state to track the zoom level

  const convertToBounds = (latLngBounds: L.LatLngBounds): Bounds => {
    return {
      getNorthEast: () => ({
        latitude: latLngBounds.getNorthEast().lat,
        longitude: latLngBounds.getNorthEast().lng,
      }),
      getSouthWest: () => ({
        latitude: latLngBounds.getSouthWest().lat,
        longitude: latLngBounds.getSouthWest().lng,
      }),
    };
  };

  const getBufferValue = (zoom: number) => {
    console.log(zoom);
    switch (zoom) {
      case 1:
      case 2:
      case 3:
      case 4:
      case 5:
      case 6:
      case 7:
      case 8:
      case 9:
        return 2;
      case 10:
        return 1.5;
      case 11:
        return 1;
      case 14:
        return 1;
      default:
        return 0.5;
    }
  };

  const calculateExpandedViewBox = (bounds: Bounds): ExpandedViewBox => {
    const northEast = bounds.getNorthEast();
    const southWest = bounds.getSouthWest();

    const latDiff = northEast.latitude - southWest.latitude;
    const lngDiff = northEast.longitude - southWest.longitude;

    const bufferValue = getBufferValue(map.getZoom());

    return {
      northEast: {
        latitude: northEast.latitude + latDiff * bufferValue,
        longitude: northEast.longitude + lngDiff * bufferValue,
      },
      southWest: {
        latitude: southWest.latitude - latDiff * bufferValue,
        longitude: southWest.longitude - lngDiff * bufferValue,
      },
    };
  };

  const loadData = async () => {
    const latLngBounds = map.getBounds();
    const bounds = convertToBounds(latLngBounds);

    const zoom = map.getZoom();

    if (
      zoom !== currentZoom || // Check if the zoom level has changed
      !expandedViewBox ||
      bounds.getNorthEast().latitude > expandedViewBox.northEast.latitude ||
      bounds.getSouthWest().latitude < expandedViewBox.southWest.latitude ||
      bounds.getNorthEast().longitude > expandedViewBox.northEast.longitude ||
      bounds.getSouthWest().longitude < expandedViewBox.southWest.longitude
    ) {
      setCurrentZoom(zoom); // Update the current zoom level
      const newExpandedViewBox = calculateExpandedViewBox(bounds);
      setExpandedViewBox(newExpandedViewBox);

      try {
        const newData = await MapService.getMapData(
          newExpandedViewBox.northEast.latitude,
          newExpandedViewBox.southWest.latitude,
          newExpandedViewBox.northEast.longitude,
          newExpandedViewBox.southWest.longitude,
          zoom
        );
        setData(newData);
        if (onDataLoaded) {
          onDataLoaded();
        }
      } catch (error) {
        console.error("An error occurred while fetching data", error);
      }
    }
  };

  const map = useMapEvents({
    // moveend: loadData,
    // zoomend: loadData,
  });

  useEffect(() => {
    const timer = setTimeout(() => {
      loadData();
    }, 300);
    return () => clearTimeout(timer); // Clean up timeout if component is unmounted
  }, [map]);

  return (
    <MarkerClusterGroup>
      <>
        {data.map((item, idx) =>
          item.isCluster ? (
            <PolygonWithText item={item} idx={idx} />
          ) : (
            <Marker key={idx} position={[item.latitude, item.longitude]}>
              <Popup>{item.name}</Popup>
            </Marker>
          )
        )}
      </>
    </MarkerClusterGroup>
  );
};

interface MapComponentProps {
  initialCenter?: [number, number];
  initialZoom?: number;
}

const MapComponent: React.FC<MapComponentProps> = ({
  initialCenter,
  initialZoom,
}) => {
  const [isLoading, setIsLoading] = useState(true);
  const [isDataLoading, setIsDataLoading] = useState(true);
  const [mapCenter, setMapCenter] = useState<[number, number]>(
    initialCenter || [0, 0]
  );
  const [zoom, setZoom] = useState(initialZoom || 7);
  useEffect(() => {
    if (!initialCenter) {
      const fetchData = async () => {
        try {
          const data = await MapService.getMapCenter();
          setMapCenter([data.latitude, data.longitude]);
          setIsLoading(false);
        } catch (error) {
          console.error("An error occurred while fetching data", error);
        } finally {
        }
      };
      fetchData();
    } else {
      setIsLoading(false);
    }
  }, [initialCenter]);

  return (
    <Spin spinning={isDataLoading}>
      {isLoading && <div style={{ height: "100%", width: "100%" }}></div>}
      {!isLoading && (
        <MapContainer
          style={{ height: "100%", width: "100%" }}
          center={mapCenter}
          zoom={zoom}
          scrollWheelZoom={true}
        >
          <PlanarianMap />
          <MapLayers />

          <MapMarkers
            onDataLoaded={() => {
              setIsDataLoading(false);
            }}
          />
          <LocateControl />
        </MapContainer>
      )}
    </Spin>
  );
};

export { MapComponent };
