import React from "react";
import { Marker, useMap, Polygon } from "react-leaflet";
import L from "leaflet";
import { Cluster, Coordinate } from "./MapComponent";

export const PolygonWithText: React.FC<{
  idx: number;
  item: Cluster;
}> = ({ idx, item }) => {
  var coords = item.hullCoordinates.map(
    (coord) => [coord.latitude, coord.longitude] as [number, number]
  );
  const center = L.polygon(coords).getBounds().getCenter();
  const textIcon = L.divIcon({
    className: "",
    html: `<div style="font-size:15px">${item.count}</div>`,
  });

  const map = useMap();

  const handlePolygonClick = (coordinates: Coordinate[]) => {
    const latLngs = coordinates.map(
      (coord) => [coord.latitude, coord.longitude] as [number, number]
    );
    const bounds = L.latLngBounds(latLngs);
    map.fitBounds(bounds);
  };

  return (
    <Polygon
      key={idx}
      positions={coords}
      eventHandlers={{
        click: () => handlePolygonClick(item.hullCoordinates),
      }}
      color="blue"
    >
      <Marker position={center} icon={textIcon} />
    </Polygon>
  );
};
