import { useContext, useEffect } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { MapComponent } from "../Components/MapComponent";
import { useLocation } from "react-router-dom";

const MapPage = () => {
  const {
    setHeaderTitle,
    setHeaderButtons,
    setContentStyle,
    defaultContentStyle,
  } = useContext(AppContext);

  const location = useLocation();
  const params = new URLSearchParams(location.search);
  const lat = params.get("lat");
  const lng = params.get("lng");
  const zoomParam = params.get("zoom");

  const initialCenter =
    lat && lng
      ? ([parseFloat(lat), parseFloat(lng)] as [number, number])
      : undefined;
  const initialZoom = zoomParam ? parseFloat(zoomParam) : undefined;

  useEffect(() => {
    setHeaderTitle([`Map`]);
    setHeaderButtons([]);
    setContentStyle({});

    return () => {
      setContentStyle(defaultContentStyle);
    };
  }, []);

  return (
    <>
      <MapComponent
        initialCenter={initialCenter}
        initialZoom={initialZoom}
        onMoveEnd={(event) => {
          const { latitude, longitude, zoom } = event.viewState;

          const searchParams = new URLSearchParams(window.location.search);
          searchParams.set("lat", latitude.toString());
          searchParams.set("lng", longitude.toString());
          searchParams.set("zoom", zoom.toString());
          window.history.replaceState(
            null,
            "",
            window.location.pathname + "?" + searchParams.toString()
          );
        }}
      />
    </>
  );
};

export { MapPage };
