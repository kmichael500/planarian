import { useContext, useEffect } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { MapComponent } from "../Components/MapComponent";

const MapPage = () => {
  const {
    setHeaderTitle,
    setHeaderButtons,
    setContentStyle,
    defaultContentStyle,
  } = useContext(AppContext);

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
      <MapComponent />
    </>
  );
};

export { MapPage };
