import { useState } from "react";
import { MapContainer, Marker, TileLayer, useMap } from "react-leaflet";
import "leaflet/dist/leaflet.css";
import { useEffect } from "react";

export interface MapPointComponentProps {
  lat: number;
  long: number;
  zoom: number;
}

const MapPointComponent = ({ lat, long, zoom }: MapPointComponentProps) => {
  const [currentPosition, setCurrentPosition] = useState<[number, number]>([
    lat,
    long,
  ]);

  const MapWrapper = () => {
    const map = useMap();
    useEffect(() => {
      map.setView([lat, long], zoom);
    }, [map, lat, long, zoom]);

    return null;
  };
  return (
    <MapContainer
      style={{ height: "400px", width: "100%" }}
      center={currentPosition}
      zoom={zoom}
    >
      <MapWrapper />

      <TileLayer url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
      <Marker position={currentPosition}></Marker>
    </MapContainer>
  );
};

export default MapPointComponent;
