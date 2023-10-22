import "leaflet.locatecontrol"; // Import plugin
import "leaflet.locatecontrol/dist/L.Control.Locate.min.css"; // Import styles
import L from "leaflet"; // Import L from leaflet to start using the plugin
import { useEffect } from "react";
import { useMap } from "react-leaflet";

const LocateControl = () => {
  const map = useMap();
  useEffect(() => {
    const locateControl = new L.Control.Locate({
      position: "topleft",
      flyTo: true,
    }).addTo(map);
    return () => {
      // Clean up the control to avoid memory leaks
      if (map && map.off()) {
        map.removeControl(locateControl);
      }
    };
  }, [map]);
  return null;
};

export { LocateControl };
