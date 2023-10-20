import { useContext, useEffect } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { MapComponent } from "../Components/MapComponent";

const MapPage = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderTitle([`Map`]);
  }, []);

  return (
    <>
      <MapComponent />
    </>
  );
};

export { MapPage };
